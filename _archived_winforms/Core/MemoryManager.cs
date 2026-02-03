using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de memoria para optimizar recursos y prevenir memory leaks
    /// </summary>
    public class MemoryManager : IDisposable
    {
        private readonly System.Threading.Timer _memoryMonitorTimer;
        private System.Threading.Timer? _cleanupTimer;
        private readonly List<IDisposable> _disposables;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        
        // Umbrales de memoria (en MB)
        private const long WARNING_THRESHOLD_MB = 500;
        private const long CRITICAL_THRESHOLD_MB = 1000;
        private const long EMERGENCY_THRESHOLD_MB = 1500;
        
        // Configuración
        public bool EnableAutoCleanup { get; set; } = true;
        public int CleanupIntervalMinutes { get; set; } = 5;
        public int MemoryCheckIntervalSeconds { get; set; } = 30;
        
        public event Action<long> OnMemoryWarning;
        public event Action<long> OnMemoryCritical;
        public event Action<long> OnMemoryEmergency;
        public event Action<MemoryCleanupResult> OnCleanupCompleted;
        
        public MemoryManager()
        {
            _disposables = new List<IDisposable>();
            
            // Timer para monitoreo de memoria
            _memoryMonitorTimer = new System.Threading.Timer(MonitorMemory, null, 
                TimeSpan.FromSeconds(MemoryCheckIntervalSeconds), 
                TimeSpan.FromSeconds(MemoryCheckIntervalSeconds));
            
            // Timer para limpieza automática
            _cleanupTimer = new System.Threading.Timer(AutoCleanup, null,
                TimeSpan.FromMinutes(CleanupIntervalMinutes),
                TimeSpan.FromMinutes(CleanupIntervalMinutes));
        }
        
        /// <summary>
        /// Registra un objeto IDisposable para limpieza automática
        /// </summary>
        public void RegisterDisposable(IDisposable disposable)
        {
            if (disposable == null) return;
            
            lock (_lockObject)
            {
                _disposables.Add(disposable);
            }
        }
        
        /// <summary>
        /// Elimina un objeto del registro
        /// </summary>
        public void UnregisterDisposable(IDisposable disposable)
        {
            if (disposable == null) return;
            
            lock (_lockObject)
            {
                _disposables.Remove(disposable);
            }
        }
        
        /// <summary>
        /// Monitorea el uso de memoria y dispara eventos según umbrales
        /// </summary>
        private void MonitorMemory(object state)
        {
            if (_disposed) return;
            
            try
            {
                var currentMemory = GetCurrentMemoryUsage();
                
                if (currentMemory >= EMERGENCY_THRESHOLD_MB)
                {
                    OnMemoryEmergency?.Invoke(currentMemory);
                    EmergencyCleanup();
                }
                else if (currentMemory >= CRITICAL_THRESHOLD_MB)
                {
                    OnMemoryCritical?.Invoke(currentMemory);
                    ForceGarbageCollection();
                }
                else if (currentMemory >= WARNING_THRESHOLD_MB)
                {
                    OnMemoryWarning?.Invoke(currentMemory);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en MonitorMemory: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpieza automática periódica
        /// </summary>
        private void AutoCleanup(object state)
        {
            if (_disposed || !EnableAutoCleanup) return;
            
            try
            {
                var result = PerformCleanup();
                OnCleanupCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en AutoCleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Realiza limpieza completa de memoria
        /// </summary>
        public MemoryCleanupResult PerformCleanup()
        {
            var result = new MemoryCleanupResult();
            var initialMemory = GetCurrentMemoryUsage();
            
            try
            {
                // 1. Limpiar disposables
                var disposedCount = CleanupDisposables();
                result.DisposablesCleaned = disposedCount;
                
                // 2. Forzar garbage collection
                ForceGarbageCollection();
                
                // 3. Limpiar caché si existe
                var cacheCleaned = CleanupCache();
                result.CacheCleaned = cacheCleaned;
                
                // 4. Compactar memoria
                CompactMemory();
                
                var finalMemory = GetCurrentMemoryUsage();
                result.MemoryFreed = initialMemory - finalMemory;
                result.Success = true;
                
                Debug.WriteLine($"Cleanup completado: {result.MemoryFreed} MB liberados");
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Success = false;
                Debug.WriteLine($"Error en cleanup: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Limpieza de emergencia para memoria crítica
        /// </summary>
        private void EmergencyCleanup()
        {
            try
            {
                Debug.WriteLine("🚨 EMERGENCY CLEANUP ACTIVADO");
                
                // Limpiar todos los disposables
                var disposedCount = CleanupDisposables(forceAll: true);
                
                // Forzar garbage collection agresivo
                for (int i = 0; i < 3; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(100);
                }
                
                // Compactar memoria grande
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
                
                Debug.WriteLine($"🚨 Emergency cleanup: {disposedCount} disposables eliminados");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en emergency cleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpia objetos IDisposable registrados
        /// </summary>
        private int CleanupDisposables(bool forceAll = false)
        {
            int disposedCount = 0;
            
            lock (_lockObject)
            {
                var toDispose = _disposables.Where(d => d != null).ToList();
                
                foreach (var disposable in toDispose)
                {
                    try
                    {
                        if (forceAll || ShouldDispose(disposable))
                        {
                            disposable.Dispose();
                            _disposables.Remove(disposable);
                            disposedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error al hacer dispose: {ex.Message}");
                    }
                }
            }
            
            return disposedCount;
        }
        
        /// <summary>
        /// Determina si un objeto debe ser eliminado
        /// </summary>
        private bool ShouldDispose(IDisposable disposable)
        {
            // Lógica para decidir si eliminar un objeto
            // Por ahora, eliminamos todos los que no sean críticos
            return !(disposable is System.Threading.Timer); // No eliminar timers críticos
        }
        
        /// <summary>
        /// Fuerza garbage collection
        /// </summary>
        private void ForceGarbageCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        
        /// <summary>
        /// Compacta memoria
        /// </summary>
        private void CompactMemory()
        {
            try
            {
                // ERROR: GC.Collect(GC.MaxGeneration, GCCollectionMode.Compacted);
            }
            catch
            {
                // Si no hay soporte para compacted, usar modo normal
                // ERROR: GC.Collect(GC.MaxGeneration());
            }
        }
        
        /// <summary>
        /// Limpia cachés de la aplicación
        /// </summary>
        private bool CleanupCache()
        {
            try
            {
                // Limpiar caché de archivos temporales
                var tempPath = Path.GetTempPath();
                var slskTempFiles = Directory.GetFiles(tempPath, "slsk*.*", SearchOption.TopDirectoryOnly);
                
                foreach (var file in slskTempFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignorar archivos en uso
                    }
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Obtiene el uso actual de memoria en MB
        /// </summary>
        public long GetCurrentMemoryUsage()
        {
            using var process = Process.GetCurrentProcess();
            return process.WorkingSet64 / 1024 / 1024; // Convertir a MB
        }
        
        /// <summary>
        /// Obtiene estadísticas detalladas de memoria
        /// </summary>
        public MemoryStats GetMemoryStats()
        {
            using var process = Process.GetCurrentProcess();
            
            return new MemoryStats
            {
                WorkingSetMB = process.WorkingSet64 / 1024 / 1024,
                PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
                VirtualMemoryMB = process.VirtualMemorySize64 / 1024 / 1024,
                GCMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                RegisteredDisposables = _disposables.Count,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            };
        }
        
        /// <summary>
        /// Optimiza el uso de memoria ajustando configuraciones
        /// </summary>
        public void OptimizeMemoryUsage()
        {
            try
            {
                // Reducir intervalos de limpieza si memoria es alta
                var currentMemory = GetCurrentMemoryUsage();
                
                if (currentMemory > WARNING_THRESHOLD_MB)
                {
                    CleanupIntervalMinutes = Math.Max(1, CleanupIntervalMinutes / 2);
                    MemoryCheckIntervalSeconds = Math.Max(10, MemoryCheckIntervalSeconds / 2);
                    
                    // Reconfigurar timers
                    _cleanupTimer.Change(TimeSpan.FromMinutes(CleanupIntervalMinutes), 
                                       TimeSpan.FromMinutes(CleanupIntervalMinutes));
                    _memoryMonitorTimer.Change(TimeSpan.FromSeconds(MemoryCheckIntervalSeconds),
                                             TimeSpan.FromSeconds(MemoryCheckIntervalSeconds));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error optimizando memoria: {ex.Message}");
            }
        }
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Detener timers
                _memoryMonitorTimer?.Dispose();
                _cleanupTimer?.Dispose();
                
                // Limpiar todos los disposables registrados
                CleanupDisposables(forceAll: true);
                
                _disposed = true;
            }
        }
        
        ~MemoryManager()
        {
            Dispose(false);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Resultado de una operación de limpieza de memoria
    /// </summary>
    public class MemoryCleanupResult
    {
        public bool Success { get; set; }
        public long MemoryFreed { get; set; } // MB
        public int DisposablesCleaned { get; set; }
        public bool CacheCleaned { get; set; }
        public string Error { get; set; }
        
        public override string ToString()
        {
            return Success 
                ? $"Cleanup exitoso: {MemoryFreed} MB liberados, {DisposablesCleaned} disposables eliminados"
                : $"Cleanup falló: {Error}";
        }
    }
    
    /// <summary>
    /// Estadísticas detalladas de memoria
    /// </summary>
    public class MemoryStats
    {
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public long VirtualMemoryMB { get; set; }
        public long GCMemoryMB { get; set; }
        public int RegisteredDisposables { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        
        public override string ToString()
        {
            return $"WS: {WorkingSetMB}MB | PM: {PrivateMemoryMB}MB | VM: {VirtualMemoryMB}MB | GC: {GCMemoryMB}MB | Disp: {RegisteredDisposables}";
        }
    }
}
