// Kafka/Producer/KafkaProducerFactory.cs
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PotatoQueueConnector.Configuration;
using PotatoQueueConnector.Kafka.Configuration;
using System;

namespace PotatoQueueConnector.Kafka.Producer;

public class KafkaProducerFactory : IKafkaProducerFactory
{
    private readonly IKafkaConfigBuilder _configBuilder;
    private readonly IOptionsMonitor<QueueConfiguration> _optionsMonitor;
    private readonly ISchemaRegistryClient _schemaRegistryClient;
    private readonly ILoggerFactory _loggerFactory;

    public KafkaProducerFactory(
        IKafkaConfigBuilder configBuilder,
        IOptionsMonitor<QueueConfiguration> optionsMonitor,
        ISchemaRegistryClient schemaRegistryClient,
        ILoggerFactory loggerFactory)
    {
        _configBuilder = configBuilder;
        _optionsMonitor = optionsMonitor;
        _schemaRegistryClient = schemaRegistryClient;
        _loggerFactory = loggerFactory;
    }

    public IKafkaProducer<T> CreateProducer<T>() where T : class, IMessage<T>, new()
    {
        var config = _optionsMonitor.CurrentValue;
        var producerConfig = _configBuilder.BuildProducerConfig(config, typeof(T).Name);
        
        var protobufConfig = new ProtobufSerializerConfig
        {
            BufferBytes = 100,
            AutoRegisterSchemas = true,
            SubjectNameStrategy = SubjectNameStrategy.TopicRecord
        };
        
        var producer = new ProducerBuilder<string, T>(producerConfig)
            .SetValueSerializer(new ProtobufSerializer<T>(_schemaRegistryClient, protobufConfig))
            .SetErrorHandler((_, error) => 
            {
                var logger = _loggerFactory.CreateLogger<KafkaProducer<T>>();
                logger.LogError("Kafka producer error for {MessageType}: {Reason}", 
                    typeof(T).Name, error.Reason);
            })
            .Build();
        
        var logger = _loggerFactory.CreateLogger<KafkaProducer<T>>();
        return new KafkaProducer<T>(producer, logger);
    }
}