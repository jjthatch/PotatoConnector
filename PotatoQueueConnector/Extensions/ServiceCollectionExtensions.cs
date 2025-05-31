// Extensions/ServiceCollectionExtensions.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PotatoQueueConnector.Configuration;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PotatoQueueConnector.QueueRouting;

namespace PotatoQueueConnector.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the PotatoQueueConnector and related services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration root</param>
    /// <param name="workerType">The type of worker (e.g., "FrenchFry", "HashBrown")</param>
    /// <param name="configSectionName">The configuration section name (defaults to "QueueConfiguration")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPotatoQueueConnector(
        this IServiceCollection services,
        IConfiguration configuration,
        string workerType,
        string configSectionName = "QueueConfiguration")
    {
        if (string.IsNullOrEmpty(workerType))
        {
            throw new ArgumentException("Worker type must be specified", nameof(workerType));
        }

        // Register configuration
        services.Configure<QueueConfiguration>(configuration.GetSection(configSectionName));

        // Register the main connector as singleton
        services.TryAddSingleton<IPotatoQueueConnector>(serviceProvider =>
        {
            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<QueueConfiguration>>();
            var implementations = serviceProvider.GetServices<IQueueImplementation>();
            var logger = serviceProvider.GetRequiredService<ILogger<PotatoQueueConnector>>();

            return new PotatoQueueConnector(optionsMonitor, implementations, logger, workerType);
        });

        return services;
    }

    /// <summary>
    /// Adds a queue implementation to the service collection
    /// </summary>
    /// <typeparam name="TImplementation">The implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="lifetime">The service lifetime (defaults to Singleton)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddQueueImplementation<TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TImplementation : class, IQueueImplementation
    {
        services.Add(new ServiceDescriptor(
            typeof(IQueueImplementation),
            typeof(TImplementation),
            lifetime));

        return services;
    }

    /// <summary>
    /// Adds a queue implementation using a factory to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="implementationFactory">The factory to create the implementation</param>
    /// <param name="lifetime">The service lifetime (defaults to Singleton)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddQueueImplementation(
        this IServiceCollection services,
        Func<IServiceProvider, IQueueImplementation> implementationFactory,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        services.Add(new ServiceDescriptor(
            typeof(IQueueImplementation),
            implementationFactory,
            lifetime));

        return services;
    }
}