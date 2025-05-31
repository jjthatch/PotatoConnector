// Kafka/KafkaQueueImplementation.cs
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PotatoQueueConnector.Configuration;
using PotatoQueueConnector.Kafka.Producer;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using PotatoQueueConnector.QueueRouting;

namespace PotatoQueueConnector.Kafka;

public class KafkaQueueImplementation : IQueueImplementation, IDisposable
{
    private readonly IKafkaProducerFactory _producerFactory;
    private readonly IOptionsMonitor<QueueConfiguration> _optionsMonitor;
    private readonly ILogger<KafkaQueueImplementation> _logger;
    
    // Cache producers by message type
    private readonly ConcurrentDictionary<Type, object> _producers = new();

    public KafkaQueueImplementation(
        IKafkaProducerFactory producerFactory,
        IOptionsMonitor<QueueConfiguration> optionsMonitor,
        ILogger<KafkaQueueImplementation> logger)
    {
        _producerFactory = producerFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        
        // On config change, clear cached producers
        _optionsMonitor.OnChange(_ =>
        {
            _logger.LogInformation("Configuration changed, clearing producer cache");
            ClearProducers();
        });
    }

    public bool CanHandle(string queueType)
    {
        return queueType.Equals("Kafka", StringComparison.OrdinalIgnoreCase);
    }

    public async Task PublishAsync<T>(string topic, T message, CancellationToken cancellationToken = default)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        
        // Check if T implements IMessage<T> (protobuf)
        var messageType = typeof(T);
        var isProtobuf = IsProtobufMessage(messageType);
        
        if (!isProtobuf)
        {
            throw new NotSupportedException($"Message type {messageType.Name} is not a protobuf message. Only protobuf messages are supported.");
        }

        // We need to use reflection because we can't constrain T at compile time
        var publishMethod = GetType().GetMethod(nameof(PublishProtobufAsync), 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var genericMethod = publishMethod!.MakeGenericMethod(messageType);
        
        await (Task)genericMethod.Invoke(this, new object[] { topic, message, cancellationToken })!;
    }

    private async Task PublishProtobufAsync<T>(string topic, T message, CancellationToken cancellationToken) 
        where T : class, IMessage<T>, new()
    {
        var producer = GetOrCreateProducer<T>();
        
        // Extract key if the message has an Id property
        string? key = ExtractMessageKey(message);
        
        await producer.PublishAsync(topic, message, key, cancellationToken);
    }

    private IKafkaProducer<T> GetOrCreateProducer<T>() where T : class, IMessage<T>, new()
    {
        return (IKafkaProducer<T>)_producers.GetOrAdd(typeof(T), type => 
        {
            _logger.LogInformation("Creating producer for message type {MessageType}", type.Name);
            return _producerFactory.CreateProducer<T>();
        });
    }

    public Task<T?> ConsumeAsync<T>(string topic, CancellationToken cancellationToken = default) 
        where T : class
    {
        // TODO: Implement consumer
        throw new NotImplementedException("Consumer not implemented yet");
    }

    private bool IsProtobufMessage(Type type)
    {
        // Check if type implements IMessage<T> where T is the type itself
        var messageInterface = type.GetInterface("Google.Protobuf.IMessage`1");
        return messageInterface != null && messageInterface.GetGenericArguments()[0] == type;
    }

    private string? ExtractMessageKey<T>(T message)
    {
        var type = typeof(T);
        var idProperty = type.GetProperty("Id") ?? type.GetProperty("ID");
        
        if (idProperty != null)
        {
            var idValue = idProperty.GetValue(message);
            return idValue?.ToString();
        }

        return null;
    }

    private void ClearProducers()
    {
        foreach (var producer in _producers.Values)
        {
            if (producer is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _producers.Clear();
    }

    public void Dispose()
    {
        ClearProducers();
    }
}