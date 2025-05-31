// Kafka/Producer/IKafkaProducer.cs
using System.Threading;
using System.Threading.Tasks;

namespace PotatoQueueConnector.Kafka.Producer;

/// <summary>
/// Generic Kafka producer for strongly-typed protobuf messages
/// </summary>
public interface IKafkaProducer<T> where T : class
{
    /// <summary>
    /// Publishes a message to the specified topic with an optional key
    /// </summary>
    Task PublishAsync(string topic, T message, string? key = null, CancellationToken cancellationToken = default);
}