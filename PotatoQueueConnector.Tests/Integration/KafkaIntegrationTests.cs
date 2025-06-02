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
using FluentAssertions;
using PotatoQueueConnector.Messages;
using PotatoQueueConnector.QueueRouting;

namespace PotatoQueueConnector.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public class KafkaIntegrationTests : KafkaIntegrationTestBase
{
    private IServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public async Task Setup()
    {
        // Base class handles container setup
        await BaseSetup();
        
        // Log URLs for demo purposes
        LogContainerUrls();

        // Set up DI container with test configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["QueueConfiguration:QueueType"] = "Kafka",
                ["QueueConfiguration:ConnectionSettings:BootstrapServers"] = KafkaBootstrapServers,
                ["QueueConfiguration:ConnectionSettings:SecurityProtocol"] = "PLAINTEXT",
                ["QueueConfiguration:Topics:ProducerQueues:PotatoQueue"] = "test.potatoes",
                ["QueueConfiguration:Topics:ProducerQueues:PotatoFarmQueue"] = "test.potato-farms",
                ["QueueConfiguration:Topics:ConsumerQueues:PotatoFarmQueue"] = "test.potato-farms",
                ["QueueConfiguration:Topics:ConsumerQueues:QueuesByPotatoType:FrenchFry"] = "test.frenchfry-potatoes"
            })
            .Build();

        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug));
        
        // Add configuration
        services.AddSingleton<IConfiguration>(configuration);
        
        // Add Kafka components
        services.AddSingleton<IKafkaConfigBuilder, KafkaConfigBuilder>();
        services.AddSingleton<IKafkaProducerFactory, KafkaProducerFactory>();
        
        // Use the real schema registry from container
        services.AddSingleton<ISchemaRegistryClient>(new CachedSchemaRegistryClient(
            new SchemaRegistryConfig { Url = SchemaRegistryUrl }));
        
        // Add queue implementation
        services.AddSingleton<IQueueImplementation, KafkaQueueImplementation>();
        
        // Add main connector
        services.AddPotatoQueueConnector(configuration, "FrenchFry");
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        // Base class handles container cleanup
        await BaseCleanup();
    }

    [TestMethod]
    public async Task Should_ConnectToKafka()
    {
        // Arrange
        var connector = _serviceProvider.GetRequiredService<IPotatoQueueConnector>();
    
        // Use the protobuf-generated Potato class
        var potato = new Potato 
        { 
            Id = "test-123",
            Type = "FrenchFry",
            Status = "Raw",
            FarmId = "farm-1",
            Priority = 1,
            CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    
        // Act & Assert - should not throw
        await connector.PublishPotatoAsync(potato);
    
        await Task.Delay(1000);
        Console.WriteLine($"Check Control Center at {ControlCenterUrl} to see the message!");
    }

    [TestMethod]
    public async Task Should_PublishAndConsumePotato()
    {
        // Arrange
        var connector = _serviceProvider.GetRequiredService<IPotatoQueueConnector>();
        var potato = new Potato 
        { 
            Id = "test-potato-1",
            Type = "FrenchFry",
            Status = "Raw"
        };
        
        // Act
        await connector.PublishPotatoAsync(potato);
        
        // Log for demo
        Console.WriteLine($"Published potato {potato.Id} to Kafka");
        Console.WriteLine($"View in Control Center: {ControlCenterUrl}");
        Console.WriteLine("Navigate to Topics -> test.potatoes to see the message");
        
        // Assert - for now just verify no exceptions
        // Once consumer is implemented:
        // var consumed = await connector.GetPotatoAsync();
        // consumed.Should().NotBeNull();
        // consumed.Id.Should().Be(potato.Id);
    }

    [TestMethod]
    public async Task Should_UseSchemaRegistry()
    {
        // This test verifies Schema Registry is working
        var schemaRegistry = _serviceProvider.GetRequiredService<ISchemaRegistryClient>();
        
        // Act - Schema Registry should be accessible
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        
        // Assert
        subjects.Should().NotBeNull();
        Console.WriteLine($"Schema Registry is running at {SchemaRegistryUrl}");
        Console.WriteLine($"Found {subjects.Count} registered schemas");
    }
}