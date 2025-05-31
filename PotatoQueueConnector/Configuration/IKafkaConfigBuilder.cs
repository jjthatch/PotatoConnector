// Kafka/Configuration/IKafkaConfigBuilder.cs
using Confluent.Kafka;
using PotatoQueueConnector.Configuration;

namespace PotatoQueueConnector.Kafka.Configuration;

/// <summary>
/// Builds Kafka configuration from our settings
/// </summary>
public interface IKafkaConfigBuilder
{
    /// <summary>
    /// Builds producer configuration
    /// </summary>
    ProducerConfig BuildProducerConfig(QueueConfiguration config, string clientIdSuffix);
    
    /// <summary>
    /// Builds consumer configuration
    /// </summary>
    ConsumerConfig BuildConsumerConfig(QueueConfiguration config, string topic, string consumerGroup);
}