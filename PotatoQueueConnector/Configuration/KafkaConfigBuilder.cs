// Kafka/Configuration/KafkaConfigBuilder.cs
using Confluent.Kafka;
using PotatoQueueConnector.Configuration;

namespace PotatoQueueConnector.Kafka.Configuration;

public class KafkaConfigBuilder : IKafkaConfigBuilder
{
    public ProducerConfig BuildProducerConfig(QueueConfiguration config, string clientIdSuffix)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config.ConnectionSettings.BootstrapServers,
            SecurityProtocol = ParseSecurityProtocol(config.ConnectionSettings.SecurityProtocol),
            Acks = Acks.All,
            MaxInFlight = 5,
            CompressionType = CompressionType.Snappy,
            LingerMs = 10,
            BatchSize = 16384,
            ClientId = $"potato-producer-{clientIdSuffix}"
        };

        // Apply any additional settings
        foreach (var setting in config.ConnectionSettings.AdditionalSettings)
        {
            producerConfig.Set(setting.Key, setting.Value);
        }

        return producerConfig;
    }

    public ConsumerConfig BuildConsumerConfig(QueueConfiguration config, string topic, string consumerGroup)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config.ConnectionSettings.BootstrapServers,
            SecurityProtocol = ParseSecurityProtocol(config.ConnectionSettings.SecurityProtocol),
            GroupId = consumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            ClientId = $"potato-consumer-{topic}"
        };

        // Apply any additional settings
        foreach (var setting in config.ConnectionSettings.AdditionalSettings)
        {
            consumerConfig.Set(setting.Key, setting.Value);
        }

        return consumerConfig;
    }

    private static SecurityProtocol ParseSecurityProtocol(string protocol)
    {
        return protocol?.ToUpperInvariant() switch
        {
            "PLAINTEXT" => SecurityProtocol.Plaintext,
            "SSL" => SecurityProtocol.Ssl,
            "SASL_PLAINTEXT" => SecurityProtocol.SaslPlaintext,
            "SASL_SSL" => SecurityProtocol.SaslSsl,
            _ => SecurityProtocol.Plaintext
        };
    }
}