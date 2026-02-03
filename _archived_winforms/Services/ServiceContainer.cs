using System;
using System.Collections.Generic;

namespace SlskDown.Services
{
    /// <summary>
    /// Contenedor simple de Dependency Injection
    /// </summary>
    public class ServiceContainer
    {
        private readonly Dictionary<Type, object> _services = new();
        private static ServiceContainer? _instance;

        public static ServiceContainer Instance => _instance ??= new ServiceContainer();

        private ServiceContainer()
        {
            // Registrar servicios por defecto
            RegisterDefaults();
        }

        private void RegisterDefaults()
        {
            // Servicios singleton
            Register<ISecurityService>(new SecurityService());
            Register<ILoggingService>(new LoggingService());
            Register<ICacheService>(new CacheService());

            var securityService = Resolve<ISecurityService>();
            Register<IConfigService>(new ConfigService(securityService));
            
            // DownloadTrackingService necesita LoggingService
            var loggingService = Resolve<ILoggingService>();
            Register<IDownloadTrackingService>(new DownloadTrackingService(loggingService));
            
            // StatsService necesita LoggingService
            Register<IStatsService>(new StatsService(loggingService));
        }

        /// <summary>
        /// Registra un servicio
        /// </summary>
        public void Register<T>(T implementation) where T : class
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            _services[typeof(T)] = implementation;
        }

        /// <summary>
        /// Resuelve un servicio
        /// </summary>
        public T Resolve<T>() where T : class
        {
            var type = typeof(T);
            
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            throw new InvalidOperationException($"Servicio no registrado: {type.Name}");
        }

        /// <summary>
        /// Intenta resolver un servicio
        /// </summary>
        public bool TryResolve<T>(out T? service) where T : class
        {
            service = null;
            var type = typeof(T);

            if (_services.TryGetValue(type, out var obj))
            {
                service = (T)obj;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Limpia todos los servicios
        /// </summary>
        public void Clear()
        {
            // Dispose de servicios que lo implementen
            foreach (var service in _services.Values)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _services.Clear();
        }
    }
}

