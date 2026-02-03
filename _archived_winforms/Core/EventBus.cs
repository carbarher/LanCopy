// <copyright file="EventBus.cs" company="SlskDown">
//     Sistema de eventos centralizado inspirado en Nicotine+
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Sistema de eventos centralizado para desacoplar componentes.
    /// Inspirado en el sistema de eventos de Nicotine+.
    /// </summary>
    public class EventBus
    {
        private readonly Dictionary<string, List<Action<object>>> _handlers = new();
        private readonly Dictionary<string, ScheduledTask> _scheduledTasks = new();
        private readonly object _lock = new object();
        private int _nextTaskId = 0;

        /// <summary>
        /// Número total de suscriptores activos
        /// </summary>
        public int SubscriberCount
        {
            get
            {
                lock (_lock)
                {
                    return _handlers.Values.Sum(list => list.Count);
                }
            }
        }

        /// <summary>
        /// Suscribe un handler a un evento.
        /// </summary>
        public void Subscribe(string eventName, Action<object> handler)
        {
            lock (_lock)
            {
                if (!_handlers.ContainsKey(eventName))
                    _handlers[eventName] = new List<Action<object>>();
                
                _handlers[eventName].Add(handler);
            }
        }

        /// <summary>
        /// Desuscribe un handler de un evento.
        /// </summary>
        public void Unsubscribe(string eventName, Action<object> handler)
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(eventName, out var handlers))
                    handlers.Remove(handler);
            }
        }

        /// <summary>
        /// Publica un evento a todos los suscriptores.
        /// </summary>
        public void Publish(string eventName, object data = null)
        {
            List<Action<object>> handlers;
            
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventName, out handlers))
                    return;
                
                // Copia para evitar modificación durante iteración
                handlers = new List<Action<object>>(handlers);
            }

            foreach (var handler in handlers)
            {
                try
                {
                    handler(data);
                }
                catch (Exception ex)
                {
                    // Log error pero continúa con otros handlers
                    Console.WriteLine($"Error en handler de evento '{eventName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Programa una tarea para ejecutarse después de un delay.
        /// Similar a events.schedule() de Nicotine+.
        /// </summary>
        public string Schedule(int delayMs, Action callback, bool repeat = false)
        {
            var taskId = $"task_{Interlocked.Increment(ref _nextTaskId)}";
            var cts = new CancellationTokenSource();
            
            var task = new ScheduledTask
            {
                Id = taskId,
                Callback = callback,
                DelayMs = delayMs,
                Repeat = repeat,
                CancellationTokenSource = cts
            };

            lock (_lock)
            {
                _scheduledTasks[taskId] = task;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        await Task.Delay(delayMs, cts.Token);
                        
                        if (cts.Token.IsCancellationRequested)
                            break;

                        try
                        {
                            callback();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error en tarea programada '{taskId}': {ex.Message}");
                        }
                    }
                    while (repeat && !cts.Token.IsCancellationRequested);
                }
                catch (TaskCanceledException)
                {
                    // Normal cuando se cancela
                }
                finally
                {
                    lock (_lock)
                    {
                        _scheduledTasks.Remove(taskId);
                    }
                }
            }, cts.Token);

            return taskId;
        }

        /// <summary>
        /// Cancela una tarea programada.
        /// </summary>
        public void CancelScheduled(string taskId)
        {
            lock (_lock)
            {
                if (_scheduledTasks.TryGetValue(taskId, out var task))
                {
                    task.CancellationTokenSource.Cancel();
                    _scheduledTasks.Remove(taskId);
                }
            }
        }

        /// <summary>
        /// Cancela todas las tareas programadas.
        /// </summary>
        public void CancelAllScheduled()
        {
            lock (_lock)
            {
                foreach (var task in _scheduledTasks.Values)
                {
                    task.CancellationTokenSource.Cancel();
                }
                _scheduledTasks.Clear();
            }
        }

        /// <summary>
        /// Limpia todos los handlers y tareas.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                CancelAllScheduled();
                _handlers.Clear();
            }
        }

        private class ScheduledTask
        {
            public string Id { get; set; }
            public Action Callback { get; set; }
            public int DelayMs { get; set; }
            public bool Repeat { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
        }
    }

    /// <summary>
    /// Nombres de eventos estándar del sistema.
    /// </summary>
    public static class SystemEvents
    {
        // Conexión
        public const string ServerLogin = "server-login";
        public const string ServerDisconnect = "server-disconnect";
        
        // Descargas
        public const string DownloadStarted = "download-started";
        public const string DownloadCompleted = "download-completed";
        public const string DownloadFailed = "download-failed";
        public const string DownloadPaused = "download-paused";
        public const string DownloadResumed = "download-resumed";
        public const string DownloadCancelled = "download-cancelled";
        public const string QueueSizeChanged = "queue-size-changed";
        
        // Búsquedas
        public const string SearchStarted = "search-started";
        public const string SearchCompleted = "search-completed";
        public const string SearchFailed = "search-failed";
        
        // Autores
        public const string AuthorAdded = "author-added";
        public const string AuthorRemoved = "author-removed";
        public const string AuthorsLoaded = "authors-loaded";
        
        // Purga
        public const string PurgeStarted = "purge-started";
        public const string PurgeCompleted = "purge-completed";
        
        // Sistema
        public const string ConfigChanged = "config-changed";
        public const string MemoryWarning = "memory-warning";
        public const string Quit = "quit";
        
        // Observabilidad adicional
        public const string DownloadQueued = "download-queued";
        public const string FileQualityWarning = "file-quality-warning";
        public const string UserLimitReached = "user-limit-reached";
        public const string CacheHit = "cache-hit";
        public const string CacheMiss = "cache-miss";
        public const string ShareIndexRebuilt = "share-index-rebuilt";
        public const string FileSystemChange = "filesystem-change";
    }
}
