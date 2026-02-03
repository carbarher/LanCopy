using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SlskDown.Core
{
    /// <summary>
    /// Detector de deadlocks y operaciones bloqueadas
    /// </summary>
    public class DeadlockDetector
    {
        private readonly Dictionary<string, OperationTracker> operations;
        private readonly object lockObj = new object();
        private System.Threading.Timer monitorTimer;
        private readonly Action<string> logFunc;
        
        public DeadlockDetector(Action<string> log = null)
        {
            operations = new Dictionary<string, OperationTracker>();
            logFunc = log;
        }
        
        private class OperationTracker
        {
            public string Name { get; set; }
            public DateTime StartTime { get; set; }
            public int ThreadId { get; set; }
            public string StackTrace { get; set; }
        }
        
        /// <summary>
        /// Inicia monitoreo de deadlocks
        /// </summary>
        public void StartMonitoring(int checkIntervalSeconds = 60)
        {
            monitorTimer = new System.Threading.Timer(_ =>
            {
                CheckForDeadlocks();
            }, null, TimeSpan.FromSeconds(checkIntervalSeconds), TimeSpan.FromSeconds(checkIntervalSeconds));
        }
        
        /// <summary>
        /// Detiene monitoreo
        /// </summary>
        public void StopMonitoring()
        {
            monitorTimer?.Dispose();
            monitorTimer = null;
        }
        
        /// <summary>
        /// Registra inicio de operación
        /// </summary>
        public IDisposable TrackOperation(string operationName)
        {
            return new OperationScope(this, operationName);
        }
        
        private void RegisterOperation(string name)
        {
            lock (lockObj)
            {
                operations[name] = new OperationTracker
                {
                    Name = name,
                    StartTime = DateTime.Now,
                    ThreadId = Thread.CurrentThread.ManagedThreadId,
                    StackTrace = Environment.StackTrace
                };
            }
        }
        
        private void UnregisterOperation(string name)
        {
            lock (lockObj)
            {
                operations.Remove(name);
            }
        }
        
        private void CheckForDeadlocks()
        {
            List<OperationTracker> suspicious;
            
            lock (lockObj)
            {
                var now = DateTime.Now;
                suspicious = operations.Values
                    .Where(op => (now - op.StartTime).TotalMinutes > 5)
                    .ToList();
            }
            
            if (suspicious.Any())
            {
                logFunc?.Invoke($"⚠️ ADVERTENCIA: {suspicious.Count} operaciones bloqueadas detectadas:");
                
                foreach (var op in suspicious)
                {
                    var duration = DateTime.Now - op.StartTime;
                    logFunc?.Invoke($"  - {op.Name}: {duration.TotalMinutes:F1} minutos (Thread {op.ThreadId})");
                }
            }
        }
        
        /// <summary>
        /// Obtiene reporte de operaciones activas
        /// </summary>
        public string GetReport()
        {
            lock (lockObj)
            {
                if (operations.Count == 0)
                    return "No hay operaciones activas";
                
                var report = $"Operaciones activas: {operations.Count}\n";
                
                foreach (var op in operations.Values.OrderBy(o => o.StartTime))
                {
                    var duration = DateTime.Now - op.StartTime;
                    report += $"  - {op.Name}: {duration.TotalSeconds:F1}s (Thread {op.ThreadId})\n";
                }
                
                return report;
            }
        }
        
        private class OperationScope : IDisposable
        {
            private readonly DeadlockDetector detector;
            private readonly string operationName;
            
            public OperationScope(DeadlockDetector detector, string operationName)
            {
                this.detector = detector;
                this.operationName = operationName;
                detector.RegisterOperation(operationName);
            }
            
            public void Dispose()
            {
                detector.UnregisterOperation(operationName);
            }
        }
    }
}
