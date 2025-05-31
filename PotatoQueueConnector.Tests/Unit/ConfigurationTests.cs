// PotatoQueueConnector.Tests/Unit/ConfigurationTests.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using PotatoQueueConnector.Configuration;
using System.Collections.Generic;

namespace PotatoQueueConnector.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public class ConfigurationTests
{
    [TestMethod]
    public void Configuration_ShouldBindCorrectly()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string>
        {
            ["QueueConfiguration:QueueType"] = "Kafka",
            ["QueueConfiguration:ConnectionSettings:BootstrapServers"] = "localhost:9092",
            ["QueueConfiguration:ConnectionSettings:SecurityProtocol"] = "SASL_SSL",
            ["QueueConfiguration:Topics:ProducerQueues:PotatoQueue"] = "test.potatoes",
            ["QueueConfiguration:Topics:ProducerQueues:PotatoFarmQueue"] = "test.farms",
            ["QueueConfiguration:Topics:ConsumerQueues:PotatoFarmQueue"] = "test.farms",
            ["QueueConfiguration:Topics:ConsumerQueues:QueuesByPotatoType:Yellow"] = "test.yellow",
            ["QueueConfiguration:Topics:ConsumerQueues:QueuesByPotatoType:Red"] = "test.red"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.Configure<QueueConfiguration>(configuration.GetSection("QueueConfiguration"));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<QueueConfiguration>>();
        var config = options.Value;

        // Assert
        config.Should().NotBeNull();
        config.QueueType.Should().Be("Kafka");
        config.ConnectionSettings.BootstrapServers.Should().Be("localhost:9092");
        config.ConnectionSettings.SecurityProtocol.Should().Be("SASL_SSL");
        
        config.Topics.ProducerQueues.PotatoQueue.Should().Be("test.potatoes");
        config.Topics.ProducerQueues.PotatoFarmQueue.Should().Be("test.farms");
        
        config.Topics.ConsumerQueues.PotatoFarmQueue.Should().Be("test.farms");
        config.Topics.ConsumerQueues.QueuesByPotatoType.Should().ContainKey("Yellow");
        config.Topics.ConsumerQueues.QueuesByPotatoType["Yellow"].Should().Be("test.yellow");
        config.Topics.ConsumerQueues.QueuesByPotatoType["Red"].Should().Be("test.red");
    }

    [TestMethod]
    public void Configuration_WithIOptionsMonitor_ShouldDetectChanges()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<QueueConfiguration>(configuration.GetSection("QueueConfiguration"));
        var serviceProvider = services.BuildServiceProvider();

        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<QueueConfiguration>>();
        var changeDetected = false;
        string? newQueueType = null;

        monitor.OnChange(config =>
        {
            changeDetected = true;
            newQueueType = config.QueueType;
        });

        // Act - simulate configuration change
        configuration["QueueConfiguration:QueueType"] = "PubSub";
        configuration.Reload();

        // Assert
        changeDetected.Should().BeTrue();
        newQueueType.Should().Be("PubSub");
        monitor.CurrentValue.QueueType.Should().Be("PubSub");
    }
}
