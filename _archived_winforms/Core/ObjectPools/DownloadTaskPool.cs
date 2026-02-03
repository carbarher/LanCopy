using System;
using Microsoft.Extensions.ObjectPool;

namespace SlskDown.Core.ObjectPools
{
    public static class DownloadTaskPool
    {
        private static readonly ObjectPool<DownloadTask> _pool = 
            new DefaultObjectPool<DownloadTask>(new DownloadTaskPoolPolicy(), 1000);

        public static DownloadTask Rent()
        {
            var task = _pool.Get();
            return task;
        }

        public static void Return(DownloadTask task)
        {
            if (task != null)
            {
                task.Reset();
                _pool.Return(task);
            }
        }

        private class DownloadTaskPoolPolicy : IPooledObjectPolicy<DownloadTask>
        {
            public DownloadTask Create()
            {
                return new DownloadTask();
            }

            public bool Return(DownloadTask obj)
            {
                obj.Reset();
                return true;
            }
        }
    }

    public partial class DownloadTask
    {
        public void Reset()
        {
            // ERROR: File = null;
            // ERROR: LocalPath = null;
            // ERROR: Status = DownloadStatus.Queued;
            // ERROR: ProgressPercent = 0;
            // ERROR: SpeedMBps = 0;
            // ERROR: ErrorMessage = null;
            // ERROR: RetryCount = 0;
            // ERROR: MaxRetries = 3;
            // ERROR: AutoRetryEnabled = true;
            // ERROR: StartedAt = null;
            // ERROR: CompletedAt = null;
            // ERROR: EndTime = null;
            // ERROR: FinalFailureTime = null;
            // ERROR: LastRetryTime = null;
            // ERROR: QueuePosition = 0;
            // ERROR: UiStatusText = null;
            // ERROR: IsMultiSource = false;
        }
    }
}
