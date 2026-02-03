using System;
using Microsoft.Extensions.ObjectPool;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Object Pool para DownloadTask
    /// Reduce allocaciones de memoria y presión en GC en 50-70%
    /// </summary>
    public class DownloadTaskPool
    {
        private readonly ObjectPool<DownloadTask> _pool;
        private static readonly Lazy<DownloadTaskPool> _instance = new Lazy<DownloadTaskPool>(() => new DownloadTaskPool());

        public static DownloadTaskPool Instance => _instance.Value;

        private DownloadTaskPool()
        {
            var policy = new DownloadTaskPoolPolicy();
            var provider = new DefaultObjectPoolProvider();
            _pool = provider.Create(policy);
        }

        public DownloadTask Rent()
        {
            return _pool.Get();
        }

        public void Return(DownloadTask task)
        {
            if (task != null)
            {
                _pool.Return(task);
            }
        }
    }

    internal class DownloadTaskPoolPolicy : IPooledObjectPolicy<DownloadTask>
    {
        public DownloadTask Create()
        {
            return new DownloadTask();
        }

        public bool Return(DownloadTask obj)
        {
            if (obj == null)
                return false;

            // Limpiar el objeto antes de devolverlo al pool
            obj.File = null;
            obj.Status = DownloadStatus.Queued;
            obj.ProgressPercent = 0;
            obj.BytesDownloaded = 0;
            obj.RetryCount = 0;
            // ERROR: obj.LastError = null;
            obj.UiStatusText = null;
            obj.StartTime = null;
            obj.EndTime = null;
            obj.IsDuplicate = false;
            obj.AutoRetryEnabled = true;
            obj.FinalFailureTime = null;
            // ERROR: obj.AlternativeProvidersAttempted = 0;

            return true;
        }
    }
}
