// Abstractions/IQueueImplementation.cs
using System.Threading;
using System.Threading.Tasks;

namespace PotatoQueueConnector.QueueRouting;

/// <summary>
/// Lower-level interface that queue-specific implementations must provide
/// </summary>
public interface IQueueImplementation
{
    /// <summary>
    /// Publishes a message to the specified topic
    /// </summary>
    Task PublishAsync<T>(string topic, T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes a message from the specified topic
    /// </summary>
    Task<T?> ConsumeAsync<T>(string topic, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Checks if the implementation can handle the current configuration
    /// </summary>
    bool CanHandle(string queueType);
}