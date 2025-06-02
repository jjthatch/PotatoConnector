// Abstractions/IPotatoQueueConnector.cs
using System.Threading;
using System.Threading.Tasks;
using PotatoQueueConnector.Messages;

namespace PotatoQueueConnector.QueueRouting;
/// <summary>
/// Main interface that consuming applications use to interact with potato queues
/// </summary>
public interface IPotatoQueueConnector
{
    /// <summary>
    /// Gets the next available potato for this worker to process
    /// </summary>
    Task<Potato?> GetPotatoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a potato status update
    /// </summary>
    Task PublishPotatoAsync(Potato potato, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a potato farm (batch)
    /// </summary>
    Task PublishPotatoFarmAsync(PotatoFarm farm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next potato farm update
    /// </summary>
    Task<PotatoFarm?> GetPotatoFarmAsync(CancellationToken cancellationToken = default);
}