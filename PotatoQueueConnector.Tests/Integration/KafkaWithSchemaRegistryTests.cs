// PotatoQueueConnector.Tests/Integration/KafkaWithSchemaRegistryTests.cs
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Testcontainers.Kafka;

namespace PotatoQueueConnector.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public class KafkaWithSchemaRegistryTests
{
    private KafkaContainer _kafkaContainer = null!;
    private IContainer _schemaRegistryContainer = null!;
    private string _bootstrapServers = null!;
    private string _schemaRegistryUrl = null!;

    [TestInitialize]
    public async Task Setup()
    {
        // Start Kafka
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.5.0")
            .Build();

        await _kafkaContainer.StartAsync();
        _bootstrapServers = _kafkaContainer.GetBootstrapAddress();

        // Start Schema Registry
        _schemaRegistryContainer = new ContainerBuilder()
            .WithImage("confluentinc/cp-schema-registry:7.5.0")
            .WithPortBinding(8081, true)
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", "schema-registry")
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", "http://0.0.0.0:8081")
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", 
                $"PLAINTEXT://{_bootstrapServers}")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8081))
            .Build();

        await _schemaRegistryContainer.StartAsync();
        
        var schemaRegistryPort = _schemaRegistryContainer.GetMappedPublicPort(8081);
        _schemaRegistryUrl = $"http://localhost:{schemaRegistryPort}";
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _schemaRegistryContainer.DisposeAsync();
        await _kafkaContainer.DisposeAsync();
    }

    [TestMethod]
    public async Task Should_RegisterProtobufSchema()
    {
        // Your test with full schema registry support
        // Now _schemaRegistryUrl points to the test container
    }
}