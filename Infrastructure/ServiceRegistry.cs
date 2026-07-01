using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using LanCopy.Services;

namespace LanCopy.Infrastructure;

/// <summary>
/// Service registry for dependency injection.
/// Manages the lifecycle of all services used throughout the application.
/// </summary>
public static class ServiceRegistry
{
    /// <summary>
    /// Registers all application services into the service collection.
    /// </summary>
    public static IServiceCollection AddLanCopyServices(this IServiceCollection services)
    {
        // Register singleton services (shared instance across the application)
        services.AddSingleton<FileServer>();
                                return services;
    }

    /// <summary>
    /// Creates a configured service provider for the application.
    /// </summary>
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLanCopyServices();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Factory method to create a LanClient with proper dependency injection.
    /// </summary>
    public static LanClient CreateLanClient(string host, int port)
    {
        return new LanClient(host, port);
    }
}

