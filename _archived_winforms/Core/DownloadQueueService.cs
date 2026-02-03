using System;
using System.Collections.Generic;
using System.Linq;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Encapsula operaciones thread-safe sobre la cola de descargas compartida.
    /// </summary>
    public sealed class DownloadQueueService
    {
        private readonly List<DownloadTask> queue;
        private readonly object syncRoot;

        public DownloadQueueService(List<DownloadTask> queue, object syncRoot)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this.syncRoot = syncRoot ?? throw new ArgumentNullException(nameof(syncRoot));
        }

        public T WithQueueLock<T>(Func<IList<DownloadTask>, T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            lock (syncRoot)
            {
                return func(queue);
            }
        }

        public void WithQueueLock(Action<IList<DownloadTask>> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            lock (syncRoot)
            {
                action(queue);
            }
        }

        public bool TryAdd(
            DownloadTask task,
            Func<DownloadTask, bool> duplicatePredicate,
            Comparison<DownloadTask> comparison = null)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (duplicatePredicate == null) throw new ArgumentNullException(nameof(duplicatePredicate));

            lock (syncRoot)
            {
                if (queue.Any(duplicatePredicate))
                {
                    return false;
                }

                if (comparison != null)
                {
                    int insertIndex = queue.Count;
                    for (int i = 0; i < queue.Count; i++)
                    {
                        var existing = queue[i];
                        if (comparison(task, existing) < 0)
                        {
                            insertIndex = i;
                            break;
                        }
                    }

                    queue.Insert(insertIndex, task);
                }
                else
                {
                    queue.Add(task);
                }

                return true;
            }
        }

        public int Count(Func<DownloadTask, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            lock (syncRoot)
            {
                return queue.Count(predicate);
            }
        }

        public IReadOnlyList<DownloadTask> Snapshot(Func<DownloadTask, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            lock (syncRoot)
            {
                return queue.Where(predicate).ToList();
            }
        }

        public IReadOnlyList<DownloadTask> Snapshot()
        {
            lock (syncRoot)
            {
                return queue.ToList();
            }
        }

        public int RemoveWhere(Func<DownloadTask, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            lock (syncRoot)
            {
                return queue.RemoveAll(t => predicate(t));
            }
        }

        public void Update(Action<IList<DownloadTask>> updater)
        {
            if (updater == null) throw new ArgumentNullException(nameof(updater));

            lock (syncRoot)
            {
                updater(queue);
            }
        }

        public T Update<T>(Func<IList<DownloadTask>, T> updater)
        {
            if (updater == null) throw new ArgumentNullException(nameof(updater));

            lock (syncRoot)
            {
                return updater(queue);
            }
        }
    }
}
