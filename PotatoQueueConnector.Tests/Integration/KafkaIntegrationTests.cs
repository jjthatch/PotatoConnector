// PotatoQueueConnector.Tests/Integration/KafkaIntegrationTests.cs
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PotatoQueueConnector.Extensions;
using PotatoQueueConnector.Kafka;
using PotatoQueueConnector.Kafka.Configuration;
using PotatoQueueConnector.Kafka.Producer;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using FluentAssertions;
using PotatoQueueConnector.Messages;
using PotatoQueueConnector.QueueRouting;
using Testcontainers.Kafka;
using IContainer = DotNet.Testcontainers.Containers.IContainer;

namespace PotatoQueueConnector.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public class KafkaIntegrationTests
{
    private INetwork _network = null!;
    private KafkaContainer _kafkaContainer = null!;
    private IContainer _schemaRegistryContainer = null!;
    private IServiceProvider _serviceProvider = null!;
    private string _kafkaBootstrapServers = null!;
    private string _schemaRegistryUrl = null!;

    [TestInitialize]
    public async Task Setup()
    {
        // Create a network for containers to communicate
        _network = new NetworkBuilder()
            .WithName($"test-network-{Guid.NewGuid()}")
            .Build();
        
        await _network.CreateAsync();
        
        // Start Kafka with KRaft mode (no Zookeeper needed)
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.5.0")
            .WithNetwork(_network)
            .WithNetworkAliases("kafka")
            .WithEnvironment("KAFKA_LISTENERS", "PLAINTEXT://0.0.0.0:9092,BROKER://0.0.0.0:9093")
            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", "PLAINTEXT://kafka:9092,BROKER://kafka:9093")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:PLAINTEXT,BROKER:PLAINTEXT")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "BROKER")
            .Build();
        
        await _kafkaContainer.StartAsync();
        
        // Get the bootstrap servers from the container
        _kafkaBootstrapServers = _kafkaContainer.GetBootstrapAddress();
        
        Console.WriteLine($"Kafka started with bootstrap servers: {_kafkaBootstrapServers}");
        
        // Wait for Kafka to be fully ready
        await Task.Delay(5000);
        
        // Start Schema Registry with proper configuration
        _schemaRegistryContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-schema-registry:7.5.0")
            .WithNetwork(_network)
            .WithNetworkAliases("schema-registry")
            .WithPortBinding(8081, true)
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", "schema-registry")
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", "http://0.0.0.0:8081")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", "kafka:9093")
            .WithEnvironment("SCHEMA_REGISTRY_DEBUG", "true")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_SECURITY_PROTOCOL", "PLAINTEXT")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(8081)
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(8081)
                    .ForPath("/subjects")
                    .WithMethod(HttpMethod.Get)))
            .Build();
        
        await _schemaRegistryContainer.StartAsync();
        
        // Get the actual mapped port for Schema Registry
        var schemaRegistryPort = _schemaRegistryContainer.GetMappedPublicPort(8081);
        _schemaRegistryUrl = $"http://localhost:{schemaRegistryPort}";
        
        Console.WriteLine($"Schema Registry started on: {_schemaRegistryUrl}");
        
        // Give Schema Registry time to connect to Kafka
        await Task.Delay(5000);
        
        // Get container logs for debugging
        var kafkaLogs = await _kafkaContainer.GetLogsAsync();
        Console.WriteLine($"=== Kafka Container Logs (last 20 lines) ===");
        var kafkaLogLines = kafkaLogs.Stdout.Split('\n');
        foreach (var line in kafkaLogLines.TakeLast(20))
        {
            if (!string.IsNullOrWhiteSpace(line))
                Console.WriteLine(line);
        }
        
        var srLogs = await _schemaRegistryContainer.GetLogsAsync();
        Console.WriteLine($"\n=== Schema Registry Container Logs (last 20 lines) ===");
        var srLogLines = srLogs.Stdout.Split('\n');
        foreach (var line in srLogLines.TakeLast(20))
        {
            if (!string.IsNullOrWhiteSpace(line))
                Console.WriteLine(line);
        }
        
        Console.WriteLine($"\n=== Test Environment Ready ===");
        Console.WriteLine($"Kafka Bootstrap: {_kafkaBootstrapServers}");
        Console.WriteLine($"Schema Registry: {_schemaRegistryUrl}");
        
        // Verify Schema Registry is healthy
        try
        {
            var schemaRegistry = new CachedSchemaRegistryClient(new SchemaRegistryConfig { Url = _schemaRegistryUrl });
            var subjects = await schemaRegistry.GetAllSubjectsAsync();
            Console.WriteLine($"Schema Registry is healthy - found {subjects.Count} subjects");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Schema Registry health check failed: {ex.Message}");
            
            // Get more logs if health check fails
            var fullSrLogs = await _schemaRegistryContainer.GetLogsAsync();
            Console.WriteLine($"\n=== Full Schema Registry Logs on Failure ===");
            Console.WriteLine(fullSrLogs.Stdout);
        }
        
        Console.WriteLine($"==============================");
        
        // Set up DI container
        SetupServiceProvider();
    }

    private void SetupServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["QueueConfiguration:QueueType"] = "Kafka",
                ["QueueConfiguration:ConnectionSettings:BootstrapServers"] = _kafkaBootstrapServers,
                ["QueueConfiguration:ConnectionSettings:SecurityProtocol"] = "PLAINTEXT",
                ["QueueConfiguration:Topics:ProducerQueues:PotatoQueue"] = "test.potatoes",
                ["QueueConfiguration:Topics:ProducerQueues:PotatoFarmQueue"] = "test.potato-farms",
                ["QueueConfiguration:Topics:ConsumerQueues:PotatoFarmQueue"] = "test.potato-farms",
                ["QueueConfiguration:Topics:ConsumerQueues:QueuesByPotatoType:FrenchFry"] = "test.frenchfry-potatoes"
            })
            .Build();

        var services = new ServiceCollection();
        
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug));
        
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IKafkaConfigBuilder, KafkaConfigBuilder>();
        services.AddSingleton<IKafkaProducerFactory, KafkaProducerFactory>();
        
        // Use the actual schema registry URL
        services.AddSingleton<ISchemaRegistryClient>(new CachedSchemaRegistryClient(
            new SchemaRegistryConfig { Url = _schemaRegistryUrl }));
        
        services.AddSingleton<IQueueImplementation, KafkaQueueImplementation>();
        services.AddPotatoQueueConnector(configuration, "FrenchFry");
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        if (_schemaRegistryContainer != null)
        {
            await _schemaRegistryContainer.DisposeAsync();
        }
        
        if (_kafkaContainer != null)
        {
            await _kafkaContainer.DisposeAsync();
        }
        
        if (_network != null)
        {
            await _network.DeleteAsync();
        }
    }

    [TestMethod]
    public async Task Should_PublishToKafka_And_RegisterSchema()
    {
        // Arrange
        var connector = _serviceProvider.GetRequiredService<IPotatoQueueConnector>();
        var schemaRegistry = _serviceProvider.GetRequiredService<ISchemaRegistryClient>();
        
        var potato = new Potato 
        { 
            Id = "test-123",
            Type = "FrenchFry",
            Status = "Raw",
            FarmId = "farm-1",
            Priority = 1,
            CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        
        Console.WriteLine($"Publishing potato with ID: {potato.Id}");
        
        try
        {
            // Act
            await connector.PublishPotatoAsync(potato);
            
            Console.WriteLine("Message published successfully!");
            
            // Wait for message to be sent and schema to be registered
            await Task.Delay(3000);
            
            // Assert - verify schema was registered
            var subjects = await schemaRegistry.GetAllSubjectsAsync();
            subjects.Should().NotBeNull();
            
            Console.WriteLine($"Found {subjects.Count} registered schemas:");
            foreach (var subject in subjects)
            {
                Console.WriteLine($"  - {subject}");
            }
            
            // The subject name depends on the SubjectNameStrategy
            // With TopicRecord strategy, it should be: test.potatoes-potatoqueue.Potato
            var possibleSubjectNames = new[]
            {
                "test.potatoes-value",
                "test.potatoes-potatoqueue.Potato",
                "test.potatoes-Potato",
                "potatoqueue.Potato"
            };
            
            var foundSubject = subjects.FirstOrDefault(s => possibleSubjectNames.Any(ps => s.Contains(ps)));
            
            if (foundSubject == null)
            {
                Console.WriteLine($"Expected one of these subject names: {string.Join(", ", possibleSubjectNames)}");
                Console.WriteLine($"But found: {string.Join(", ", subjects)}");
            }
            
            foundSubject.Should().NotBeNull($"Should have registered schema with one of these patterns: {string.Join(", ", possibleSubjectNames)}");
            
            Console.WriteLine($"Successfully published message and found schema: {foundSubject}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed with exception: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Get container logs on failure
            var kafkaLogs = await _kafkaContainer.GetLogsAsync();
            Console.WriteLine($"\n=== Kafka Logs on Failure ===");
            var kafkaLogLines = kafkaLogs.Stdout.Split('\n');
            foreach (var line in kafkaLogLines.TakeLast(50))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    Console.WriteLine(line);
            }
            
            var srLogs = await _schemaRegistryContainer.GetLogsAsync();
            Console.WriteLine($"\n=== Schema Registry Logs on Failure ===");
            var srLogLines = srLogs.Stdout.Split('\n');
            foreach (var line in srLogLines.TakeLast(50))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    Console.WriteLine(line);
            }
            
            throw;
        }
    }
}