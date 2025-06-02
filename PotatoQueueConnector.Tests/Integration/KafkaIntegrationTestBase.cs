// PotatoQueueConnector.Tests/Integration/TestContainersSetup.cs
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using Testcontainers.Kafka;

namespace PotatoQueueConnector.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public class KafkaIntegrationTestBase
{
    protected INetwork _network = null!;
    protected IContainer _kafkaContainer = null!;
    protected IContainer _controlCenterContainer = null!;
    protected IContainer _schemaRegistryContainer = null!;
    
    protected string KafkaBootstrapServers => "localhost:9092";
    protected string SchemaRegistryUrl => "http://localhost:8081";
    protected string ControlCenterUrl => "http://localhost:9021";

    private readonly CancellationToken _timeoutToken = new CancellationTokenSource(TimeSpan.FromMinutes(3)).Token;

    [TestInitialize]
    public async Task BaseSetup()
    {
        // Create network for inter-container communication
        _network = new NetworkBuilder()
            .WithName($"test-network-{Guid.NewGuid():N}")
            .Build();
        
        await _network.CreateAsync();
        
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.5.0")
            .Build();
        _kafkaContainer.StartAsync().GetAwaiter().GetResult();

        // Start containers in parallel for faster startup
        //var kafkaTask = StartKafkaAsync();
        //var schemaRegistryTask = StartSchemaRegistryAsync();

        //await Task.WhenAll(kafkaTask, schemaRegistryTask);
        //await Task.WhenAll(kafkaTask);

        // Control Center depends on both Kafka and Schema Registry
        //await StartControlCenterAsync();
    }

    private async Task StartKafkaAsync()
    {
        _kafkaContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-kafka:7.5.0")
            .WithName($"kafka-{Guid.NewGuid():N}")  // Unique name to avoid conflicts
            .WithNetwork(_network)
            .WithNetworkAliases("kafka")
            .WithPortBinding(9092, 9092)
            .WithPortBinding(9101, 9101)
            // Kraft mode configuration (no Zookeeper)
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@kafka:9101")
            .WithEnvironment("KAFKA_LISTENERS", "PLAINTEXT://kafka:9092,CONTROLLER://kafka:9101")
            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", "PLAINTEXT://localhost:9092")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:PLAINTEXT,CONTROLLER:PLAINTEXT")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT")
            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "true")
            // Performance optimizations for testing
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(9092)
                .UntilMessageIsLogged("Kafka Server started"))
            .Build();

        await _kafkaContainer.StartAsync(_timeoutToken);
    }

    private async Task StartSchemaRegistryAsync()
    {
        _schemaRegistryContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-schema-registry:7.5.0")
            .WithName($"schema-registry-{Guid.NewGuid():N}")  // Unique name
            .WithNetwork(_network)
            .WithNetworkAliases("schema-registry")
            .WithPortBinding(8081, 8081)
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", "schema-registry")
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", "http://0.0.0.0:8081")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", "kafka:9092")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_TOPIC_REPLICATION_FACTOR", "1")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(8081)
                .UntilHttpRequestIsSucceeded(r => r.ForPath("/subjects")))
            .Build();

        await _schemaRegistryContainer.StartAsync(_timeoutToken);
    }

    private async Task StartControlCenterAsync()
    {
        _controlCenterContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-enterprise-control-center:7.5.0")
            .WithName($"control-center-{Guid.NewGuid():N}")  // Unique name
            .WithNetwork(_network)
            .WithNetworkAliases("control-center")
            .WithPortBinding(9021, 9021)
            .DependsOn(_kafkaContainer)
            .DependsOn(_schemaRegistryContainer)
            // Basic Control Center config
            .WithEnvironment("CONTROL_CENTER_BOOTSTRAP_SERVERS", "kafka:9092")
            .WithEnvironment("CONTROL_CENTER_SCHEMA_REGISTRY_URL", "http://schema-registry:8081")
            .WithEnvironment("CONTROL_CENTER_REPLICATION_FACTOR", "1")
            .WithEnvironment("CONTROL_CENTER_INTERNAL_TOPICS_PARTITIONS", "1")
            .WithEnvironment("CONTROL_CENTER_MONITORING_INTERCEPTOR_TOPIC_PARTITIONS", "1")
            .WithEnvironment("CONTROL_CENTER_METRICS_TOPIC_REPLICATION", "1")
            .WithEnvironment("CONTROL_CENTER_COMMAND_TOPIC_REPLICATION", "1")
            .WithEnvironment("CONTROL_CENTER_STREAMS_NUM_STREAM_THREADS", "1")
            // Disable features we don't need for testing
            .WithEnvironment("CONTROL_CENTER_CONNECT_CLUSTER", "")
            .WithEnvironment("CONTROL_CENTER_KSQL_ENABLE", "false")
            .WithEnvironment("CONTROL_CENTER_KSQL_URL", "")
            .WithEnvironment("PORT", "9021")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(9021)
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(9021)
                    .ForPath("/")
                    .ForStatusCode(System.Net.HttpStatusCode.OK)))
            .Build();

        await _controlCenterContainer.StartAsync(_timeoutToken);
    }

    [TestCleanup]
    public async Task BaseCleanup()
    {
        // Dispose in reverse order of dependencies
        if (_controlCenterContainer != null)
            await _controlCenterContainer.DisposeAsync();
        
        if (_schemaRegistryContainer != null)
            await _schemaRegistryContainer.DisposeAsync();
        
        if (_kafkaContainer != null)
            await _kafkaContainer.DisposeAsync();
        
        if (_network != null)
            await _network.DeleteAsync();
    }

    protected void LogContainerUrls()
    {
        Console.WriteLine("=== Test Container URLs ===");
        Console.WriteLine($"Kafka Bootstrap: {KafkaBootstrapServers}");
        Console.WriteLine($"Schema Registry: {SchemaRegistryUrl}");
        Console.WriteLine($"Control Center: {ControlCenterUrl}");
        Console.WriteLine("==========================");
    }
}

// Example test class using the base
[TestClass]
public class PotatoQueueIntegrationTests : KafkaIntegrationTestBase
{
    [TestMethod]
    public async Task Should_PublishAndConsumePotato()
    {
        // Arrange
        LogContainerUrls(); // For demo purposes
        
        // Your test code here
        await Task.CompletedTask;
    }
}