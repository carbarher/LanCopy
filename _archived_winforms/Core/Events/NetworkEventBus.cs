using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core.Events
{
    /// <summary>
    /// Sistema de eventos desacoplado inspirado en Nicotine+
    /// Permite comunicación entre componentes sin dependencias directas
    /// </summary>
    public class NetworkEventBus : IDisposable
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers;
        private readonly ConcurrentDictionary<Type, List<Delegate>> _asyncHandlers;
        private readonly SemaphoreSlim _subscriptionLock;
        private bool _disposed;

        public NetworkEventBus()
        {
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
            _asyncHandlers = new ConcurrentDictionary<Type, List<Delegate>>();
            _subscriptionLock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Suscribe un handler síncrono a un tipo de mensaje
        /// </summary>
        public void Subscribe<TMessage>(Action<TMessage> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _subscriptionLock.Wait();
            try
            {
                var messageType = typeof(TMessage);
                var handlers = _handlers.GetOrAdd(messageType, _ => new List<Delegate>());
                
                lock (handlers)
                {
                    handlers.Add(handler);
                }
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        /// <summary>
        /// Suscribe un handler asíncrono a un tipo de mensaje
        /// </summary>
        public void SubscribeAsync<TMessage>(Func<TMessage, Task> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _subscriptionLock.Wait();
            try
            {
                var messageType = typeof(TMessage);
                var handlers = _asyncHandlers.GetOrAdd(messageType, _ => new List<Delegate>());
                
                lock (handlers)
                {
                    handlers.Add(handler);
                }
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        /// <summary>
        /// Desuscribe un handler de un tipo de mensaje
        /// </summary>
        public void Unsubscribe<TMessage>(Action<TMessage> handler)
        {
            if (handler == null)
                return;

            _subscriptionLock.Wait();
            try
            {
                var messageType = typeof(TMessage);
                
                if (_handlers.TryGetValue(messageType, out var handlers))
                {
                    lock (handlers)
                    {
                        handlers.Remove(handler);
                    }
                }
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        /// <summary>
        /// Desuscribe un handler asíncrono de un tipo de mensaje
        /// </summary>
        public void UnsubscribeAsync<TMessage>(Func<TMessage, Task> handler)
        {
            if (handler == null)
                return;

            _subscriptionLock.Wait();
            try
            {
                var messageType = typeof(TMessage);
                
                if (_asyncHandlers.TryGetValue(messageType, out var handlers))
                {
                    lock (handlers)
                    {
                        handlers.Remove(handler);
                    }
                }
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        /// <summary>
        /// Publica un mensaje de forma síncrona
        /// </summary>
        public void Publish<TMessage>(TMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messageType = typeof(TMessage);

            // Ejecutar handlers síncronos
            if (_handlers.TryGetValue(messageType, out var handlers))
            {
                List<Delegate> handlersCopy;
                lock (handlers)
                {
                    handlersCopy = new List<Delegate>(handlers);
                }

                foreach (var handler in handlersCopy)
                {
                    try
                    {
                        ((Action<TMessage>)handler)(message);
                    }
                    catch (Exception ex)
                    {
                        OnHandlerError(messageType, ex);
                    }
                }
            }

            // Ejecutar handlers asíncronos de forma fire-and-forget
            if (_asyncHandlers.TryGetValue(messageType, out var asyncHandlers))
            {
                List<Delegate> asyncHandlersCopy;
                lock (asyncHandlers)
                {
                    asyncHandlersCopy = new List<Delegate>(asyncHandlers);
                }

                foreach (var handler in asyncHandlersCopy)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ((Func<TMessage, Task>)handler)(message);
                        }
                        catch (Exception ex)
                        {
                            OnHandlerError(messageType, ex);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Publica un mensaje de forma asíncrona esperando todos los handlers
        /// </summary>
        public async Task PublishAsync<TMessage>(TMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messageType = typeof(TMessage);
            var tasks = new List<Task>();

            // Ejecutar handlers síncronos
            if (_handlers.TryGetValue(messageType, out var handlers))
            {
                List<Delegate> handlersCopy;
                lock (handlers)
                {
                    handlersCopy = new List<Delegate>(handlers);
                }

                foreach (var handler in handlersCopy)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            ((Action<TMessage>)handler)(message);
                        }
                        catch (Exception ex)
                        {
                            OnHandlerError(messageType, ex);
                        }
                    }));
                }
            }

            // Ejecutar handlers asíncronos
            if (_asyncHandlers.TryGetValue(messageType, out var asyncHandlers))
            {
                List<Delegate> asyncHandlersCopy;
                lock (asyncHandlers)
                {
                    asyncHandlersCopy = new List<Delegate>(asyncHandlers);
                }

                foreach (var handler in asyncHandlersCopy)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await ((Func<TMessage, Task>)handler)(message);
                        }
                        catch (Exception ex)
                        {
                            OnHandlerError(messageType, ex);
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Obtiene el número de suscriptores para un tipo de mensaje
        /// </summary>
        public int GetSubscriberCount<TMessage>()
        {
            var messageType = typeof(TMessage);
            var count = 0;

            if (_handlers.TryGetValue(messageType, out var handlers))
            {
                lock (handlers)
                {
                    count += handlers.Count;
                }
            }

            if (_asyncHandlers.TryGetValue(messageType, out var asyncHandlers))
            {
                lock (asyncHandlers)
                {
                    count += asyncHandlers.Count;
                }
            }

            return count;
        }

        /// <summary>
        /// Limpia todas las suscripciones
        /// </summary>
        public void Clear()
        {
            _subscriptionLock.Wait();
            try
            {
                _handlers.Clear();
                _asyncHandlers.Clear();
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        /// <summary>
        /// Evento que se dispara cuando un handler lanza una excepción
        /// </summary>
        public event EventHandler<HandlerErrorEventArgs> HandlerError;

        private void OnHandlerError(Type messageType, Exception exception)
        {
            HandlerError?.Invoke(this, new HandlerErrorEventArgs(messageType, exception));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Clear();
            _subscriptionLock?.Dispose();
        }
    }

    public class HandlerErrorEventArgs : EventArgs
    {
        public Type MessageType { get; }
        public Exception Exception { get; }

        public HandlerErrorEventArgs(Type messageType, Exception exception)
        {
            MessageType = messageType;
            Exception = exception;
        }
    }
}
