using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Soulseek;
using SlskDown.Models;

namespace SlskDown.Services
{
    public class DownloadService
    {
        private readonly Channel<DownloadTask> _downloadChannel;
        private readonly Channel<DownloadResult> _resultsChannel;
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly int _maxConcurrentDownloads;
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<DownloadProgressEventArgs> DownloadProgress;
        public event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;
        public event EventHandler<DownloadErrorEventArgs> DownloadError;

        public DownloadService(int maxConcurrentDownloads = 5)
        {
            _maxConcurrentDownloads = maxConcurrentDownloads;
            _concurrencySemaphore = new SemaphoreSlim(maxConcurrentDownloads);
            
            _downloadChannel = Channel.CreateBounded<DownloadTask>(
                new BoundedChannelOptions(1000)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });

            _resultsChannel = Channel.CreateBounded<DownloadResult>(
                new BoundedChannelOptions(1000)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cancellationTokenSource.Token);

            var processingTask = Task.Run(() => ProcessDownloadsAsync(linkedCts.Token), linkedCts.Token);
            var resultsTask = Task.Run(() => ProcessResultsAsync(linkedCts.Token), linkedCts.Token);

            await Task.WhenAll(processingTask, resultsTask);
        }

        public async Task EnqueueDownloadAsync(DownloadTask task, CancellationToken cancellationToken = default)
        {
            await _downloadChannel.Writer.WriteAsync(task, cancellationToken);
        }

        private async Task ProcessDownloadsAsync(CancellationToken cancellationToken)
        {
            await foreach (var task in _downloadChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await _concurrencySemaphore.WaitAsync(cancellationToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await ExecuteDownloadAsync(task, cancellationToken);
                        await _resultsChannel.Writer.WriteAsync(result, cancellationToken);
                    }
                    finally
                    {
                        _concurrencySemaphore.Release();
                    }
                }, cancellationToken);
            }
        }

        private async Task ProcessResultsAsync(CancellationToken cancellationToken)
        {
            await foreach (var result in _resultsChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (result.IsSuccess)
                {
                    DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs(result));
                }
                else
                {
                    DownloadError?.Invoke(this, new DownloadErrorEventArgs(result));
                }
            }
        }

        private async Task<DownloadResult> ExecuteDownloadAsync(DownloadTask task, CancellationToken cancellationToken)
        {
            var result = new DownloadResult
            {
                Task = task,
                StartTime = DateTime.Now
            };

            try
            {
                task.Status = DownloadStatus.Downloading;
                task.StartedAt = DateTime.Now;

                var progress = new Progress<int>(percent =>
                {
                    task.ProgressPercent = percent;
                    DownloadProgress?.Invoke(this, new DownloadProgressEventArgs(task, percent));
                });

                await Task.Delay(100, cancellationToken);

                result.IsSuccess = true;
                result.EndTime = DateTime.Now;
                task.Status = DownloadStatus.Completed;
                task.CompletedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Error = ex;
                result.EndTime = DateTime.Now;
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task StopAsync()
        {
            _downloadChannel.Writer.Complete();
            _resultsChannel.Writer.Complete();
            _cancellationTokenSource.Cancel();
            await Task.Delay(100);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _concurrencySemaphore?.Dispose();
        }
    }

    public class DownloadProgressEventArgs : EventArgs
    {
        public DownloadTask Task { get; }
        public int ProgressPercent { get; }

        public DownloadProgressEventArgs(DownloadTask task, int progressPercent)
        {
            Task = task;
            ProgressPercent = progressPercent;
        }
    }

    public class DownloadCompletedEventArgs : EventArgs
    {
        public DownloadResult Result { get; }

        public DownloadCompletedEventArgs(DownloadResult result)
        {
            Result = result;
        }
    }

    public class DownloadErrorEventArgs : EventArgs
    {
        public DownloadResult Result { get; }

        public DownloadErrorEventArgs(DownloadResult result)
        {
            Result = result;
        }
    }

    public class DownloadResult
    {
        public DownloadTask Task { get; set; }
        public bool IsSuccess { get; set; }
        public Exception Error { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }
}
