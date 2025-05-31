// Kafka/Producer/KafkaProducer.cs
using Confluent.Kafka;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PotatoQueueConnector.Kafka.Producer;

/// <summary>
/// Kafka producer for protobuf messages with optional string keys
/// </summary>
public class KafkaProducer<T> : IKafkaProducer<T>, IDisposable 
    where T : class, IMessage<T>, new()
{
    private readonly IProducer<string, T> _producer;
    private readonly ILogger<KafkaProducer<T>> _logger;

    public KafkaProducer(IProducer<string, T> producer, ILogger<KafkaProducer<T>> logger)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(string topic, T message, string? key = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var kafkaMessage = new Message<string, T> 
            { 
                Key = key,  // null is valid for Kafka
                Value = message 
            };
            
            _logger.LogDebug("Publishing {MessageType} to topic {Topic} with key {Key}", 
                typeof(T).Name, topic, key ?? "(null)");
            
            var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            
            _logger.LogDebug("Message delivered to {Topic}[{Partition}]@{Offset}", 
                result.Topic, result.Partition, result.Offset);
        }
        catch (ProduceException<string, T> ex)
        {
            _logger.LogError(ex, "Error publishing to Kafka topic {Topic}: {Reason}", 
                topic, ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}