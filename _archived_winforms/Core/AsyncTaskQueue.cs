using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Cola de tareas asíncronas con control de concurrencia (estilo Nicotine+)
    /// </summary>
    public class AsyncTaskQueue
    {
        private readonly Queue<Func<Task>> taskQueue = new Queue<Func<Task>>();
        private readonly SemaphoreSlim semaphore;
        private readonly int maxConcurrency;
        private bool isRunning = false;
        private readonly object lockObj = new object();
        
        public int QueuedTasks => taskQueue.Count;
        public int MaxConcurrency => maxConcurrency;
        
        public AsyncTaskQueue(int maxConcurrency = 5)
        {
            this.maxConcurrency = maxConcurrency;
            this.semaphore = new SemaphoreSlim(maxConcurrency);
        }
        
        public void Enqueue(Func<Task> task)
        {
            lock (lockObj)
            {
                taskQueue.Enqueue(task);
                
                if (!isRunning)
                {
                    isRunning = true;
                    _ = ProcessQueueAsync();
                }
            }
        }
        
        private async Task ProcessQueueAsync()
        {
            while (true)
            {
                Func<Task> task;
                
                lock (lockObj)
                {
                    if (taskQueue.Count == 0)
                    {
                        isRunning = false;
                        return;
                    }
                    
                    task = taskQueue.Dequeue();
                }
                
                await semaphore.WaitAsync();
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await task();
                    }
                    catch (Exception ex)
                    {
                        // Log error silently
                        System.Diagnostics.Debug.WriteLine($"Task error: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
            }
        }
        
        public void Clear()
        {
            lock (lockObj)
            {
                taskQueue.Clear();
            }
        }
    }
}
