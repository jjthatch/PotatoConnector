// Abstractions/IPotatoQueueConnector.cs
using System.Threading;
using System.Threading.Tasks;

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

// Domain models (these would normally be in a shared library or defined by protobuf)
public class Potato
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "FrenchFry", "HashBrown", etc.
    public string FarmId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;
    // Other properties...
}

public class PotatoFarm
{
    public string Id { get; set; } = string.Empty;
    public int TotalPotatoes { get; set; }
    public int ProcessedPotatoes { get; set; }
    public string Status { get; set; } = string.Empty;
    // Other properties...
}