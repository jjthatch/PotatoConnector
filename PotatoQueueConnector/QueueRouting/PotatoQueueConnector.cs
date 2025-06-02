// PotatoQueueConnector.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PotatoQueueConnector.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PotatoQueueConnector.Messages;
using PotatoQueueConnector.QueueRouting;

namespace PotatoQueueConnector
{
    /// <summary>
    /// Main implementation that consuming apps use. Handles configuration changes dynamically.
    /// </summary>
    public class PotatoQueueConnector : IPotatoQueueConnector
    {
        private readonly IOptionsMonitor<QueueConfiguration> _optionsMonitor;
        private readonly IEnumerable<IQueueImplementation> _implementations;
        private readonly ILogger<PotatoQueueConnector> _logger;
        private readonly string _workerType;

        public PotatoQueueConnector(
            IOptionsMonitor<QueueConfiguration> optionsMonitor,
            IEnumerable<IQueueImplementation> implementations,
            ILogger<PotatoQueueConnector> logger,
            string workerType)
        {
            _optionsMonitor = optionsMonitor;
            _implementations = implementations;
            _logger = logger;
            _workerType = workerType;

            if (string.IsNullOrEmpty(workerType))
            {
                throw new ArgumentException("Worker type must be specified", nameof(workerType));
            }

            // Log configuration changes
            _optionsMonitor.OnChange(config =>
            {
                _logger.LogInformation(
                    "Queue configuration changed. New queue type: {QueueType}", 
                    config.QueueType);
            });
        }

        public async Task<Potato?> GetPotatoAsync(CancellationToken cancellationToken = default)
        {
            var config = _optionsMonitor.CurrentValue;
            var implementation = GetCurrentImplementation(config.QueueType);

            // Get the topic for this worker type
            if (!config.Topics.ConsumerQueues.QueuesByPotatoType.TryGetValue(_workerType, out var topic))
            {
                _logger.LogWarning("No topic configured for worker type: {WorkerType}", _workerType);
                return null;
            }

            _logger.LogDebug("Consuming potato from topic: {Topic} for worker type: {WorkerType}", 
                topic, _workerType);

            try
            {
                return await implementation.ConsumeAsync<Potato>(topic, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming potato from topic: {Topic}", topic);
                throw;
            }
        }

        public async Task PublishPotatoAsync(Potato potato, CancellationToken cancellationToken = default)
        {
            var config = _optionsMonitor.CurrentValue;
            var implementation = GetCurrentImplementation(config.QueueType);
            var topic = config.Topics.ProducerQueues.PotatoQueue;

            _logger.LogDebug("Publishing potato {PotatoId} to topic: {Topic}", 
                potato.Id, topic);

            try
            {
                await implementation.PublishAsync(topic, potato, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing potato {PotatoId} to topic: {Topic}", 
                    potato.Id, topic);
                throw;
            }
        }

        public async Task PublishPotatoFarmAsync(PotatoFarm farm, CancellationToken cancellationToken = default)
        {
            var config = _optionsMonitor.CurrentValue;
            var implementation = GetCurrentImplementation(config.QueueType);
            var topic = config.Topics.ProducerQueues.PotatoFarmQueue;

            _logger.LogDebug("Publishing potato farm {FarmId} to topic: {Topic}", 
                farm.Id, topic);

            try
            {
                await implementation.PublishAsync(topic, farm, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing potato farm {FarmId} to topic: {Topic}", 
                    farm.Id, topic);
                throw;
            }
        }

        public async Task<PotatoFarm?> GetPotatoFarmAsync(CancellationToken cancellationToken = default)
        {
            var config = _optionsMonitor.CurrentValue;
            var implementation = GetCurrentImplementation(config.QueueType);
            var topic = config.Topics.ConsumerQueues.PotatoFarmQueue;

            _logger.LogDebug("Consuming potato farm from topic: {Topic}", topic);

            try
            {
                return await implementation.ConsumeAsync<PotatoFarm>(topic, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming potato farm from topic: {Topic}", topic);
                throw;
            }
        }

        private IQueueImplementation GetCurrentImplementation(string queueType)
        {
            var implementation = _implementations.FirstOrDefault(impl => impl.CanHandle(queueType));
            
            if (implementation == null)
            {
                throw new NotSupportedException($"No implementation found for queue type: {queueType}");
            }

            return implementation;
        }
    }
}