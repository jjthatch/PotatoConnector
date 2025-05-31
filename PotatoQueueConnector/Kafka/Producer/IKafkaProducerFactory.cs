// Kafka/Producer/IKafkaProducerFactory.cs
using Google.Protobuf;

namespace PotatoQueueConnector.Kafka.Producer;

/// <summary>
/// Factory for creating generic Kafka producers
/// </summary>
public interface IKafkaProducerFactory
{
    /// <summary>
    /// Creates a producer for the specified protobuf message type
    /// </summary>
    IKafkaProducer<T> CreateProducer<T>() where T : class, IMessage<T>, new();
}