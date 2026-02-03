// <copyright file="AutoSaveManager.cs" company="SlskDown">
//     Gestión de auto-save periódico
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestiona el guardado automático periódico de datos.
    /// Inspirado en el auto-save de Nicotine+ (cada 3 minutos).
    /// </summary>
    public class AutoSaveManager
    {
        private readonly EventBus _eventBus;
        private readonly List<Func<Task>> _saveCallbacks = new();
        private string _scheduledTaskId;
        private bool _allowSaving = false;
        private DateTime _lastSaveTime = DateTime.MinValue;
        private readonly object _lock = new object();

        public int IntervalMs { get; set; } = 180000; // 3 minutos por defecto
        public bool IsEnabled => _allowSaving;
        public DateTime LastSaveTime => _lastSaveTime;

        public AutoSaveManager(EventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// Registra un callback para ser llamado en cada auto-save.
        /// </summary>
        public void RegisterSaveCallback(Func<Task> callback)
        {
            lock (_lock)
            {
                _saveCallbacks.Add(callback);
            }
        }

        /// <summary>
        /// Inicia el auto-save periódico.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_allowSaving)
                    return; // Ya está iniciado

                _allowSaving = true;
                
                _scheduledTaskId = _eventBus.Schedule(
                    delayMs: IntervalMs,
                    callback: () => _ = SaveAllAsync(),
                    repeat: true
                );

                Console.WriteLine($"💾 Auto-save iniciado (cada {IntervalMs / 1000}s)");
            }
        }

        /// <summary>
        /// Detiene el auto-save periódico.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_allowSaving)
                    return;

                _allowSaving = false;

                if (_scheduledTaskId != null)
                {
                    _eventBus.CancelScheduled(_scheduledTaskId);
                    _scheduledTaskId = null;
                }

                Console.WriteLine("💾 Auto-save detenido");
            }
        }

        /// <summary>
        /// Ejecuta un guardado manual inmediato.
        /// </summary>
        public async Task SaveNowAsync()
        {
            await SaveAllAsync();
        }

        /// <summary>
        /// Ejecuta todos los callbacks de guardado.
        /// </summary>
        private async Task SaveAllAsync()
        {
            if (!_allowSaving)
                return;

            List<Func<Task>> callbacks;
            lock (_lock)
            {
                callbacks = new List<Func<Task>>(_saveCallbacks);
            }

            if (callbacks.Count == 0)
                return;

            var startTime = DateTime.UtcNow;
            var successCount = 0;
            var errorCount = 0;

            foreach (var callback in callbacks)
            {
                try
                {
                    await callback();
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Console.WriteLine($"⚠️ Error en auto-save callback: {ex.Message}");
                }
            }

            _lastSaveTime = DateTime.UtcNow;
            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

            Console.WriteLine($"💾 Auto-save completado: {successCount} OK, {errorCount} errores ({elapsed:F0}ms)");
            
            // Publicar evento
            _eventBus.Publish("auto-save-completed", new AutoSaveEventData
            {
                SuccessCount = successCount,
                ErrorCount = errorCount,
                ElapsedMs = elapsed
            });
        }

        public class AutoSaveEventData
        {
            public int SuccessCount { get; set; }
            public int ErrorCount { get; set; }
            public double ElapsedMs { get; set; }
        }
    }
}
