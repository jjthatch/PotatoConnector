// Configuration/QueueConfiguration.cs
using System.Collections.Generic;

namespace PotatoQueueConnector.Configuration;

public class QueueConfiguration
{
    public string QueueType { get; set; } = "Kafka";
    public ConnectionSettings ConnectionSettings { get; set; } = new();
    public Topics Topics { get; set; } = new();
}

public class ConnectionSettings
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string SecurityProtocol { get; set; } = "PLAINTEXT";
    public Dictionary<string, string> AdditionalSettings { get; set; } = new();
}

public class Topics
{
    public ProducerQueues ProducerQueues { get; set; } = new();
    public ConsumerQueues ConsumerQueues { get; set; } = new();
}

public class ProducerQueues
{
    public string PotatoQueue { get; set; } = string.Empty;
    public string PotatoFarmQueue { get; set; } = string.Empty;
}

public class ConsumerQueues
{
    public string PotatoFarmQueue { get; set; } = string.Empty;
    public Dictionary<string, string> QueuesByPotatoType { get; set; } = new();
}