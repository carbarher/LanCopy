using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SlskDown.Core;

namespace SlskDown.Services
{
    /// <summary>
    /// Ejecuta cómputos BLAKE3 en lotes para reducir presión sobre el hilo de UI.
    /// Agrupa solicitudes y procesa en paralelo usando la librería Rust.
    /// </summary>
    public sealed class Blake3BatchHasher : IDisposable
    {
        private readonly Channel<HashRequest> channel;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly Task worker;
        private readonly int batchSize;
        private readonly TimeSpan flushInterval;
        private readonly int maxDegreeOfParallelism;
        private readonly Action<string>? log;

        private record HashRequest(string FilePath, TaskCompletionSource<string?> Completion);

        public Blake3BatchHasher(
            int batchSize = 8,
            TimeSpan? flushInterval = null,
            int? maxDegreeOfParallelism = null,
            Action<string>? log = null)
        {
            if (batchSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(batchSize));
            }

            this.batchSize = batchSize;
            this.flushInterval = flushInterval ?? TimeSpan.FromMilliseconds(250);
            this.maxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism ?? Math.Max(1, Environment.ProcessorCount / 2));
            this.log = log;

            channel = Channel.CreateUnbounded<HashRequest>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            worker = Task.Run(RunAsync);
        }

        public Task<string?> ComputeAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!channel.Writer.TryWrite(new HashRequest(filePath, completion)))
            {
                completion.TrySetException(new ObjectDisposedException(nameof(Blake3BatchHasher)));
            }

            return completion.Task;
        }

        private async Task RunAsync()
        {
            var token = cancellationTokenSource.Token;
            var buffer = new List<HashRequest>(batchSize);

            try
            {
                while (await channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out var request))
                    {
                        buffer.Add(request);
                        if (buffer.Count >= batchSize)
                        {
                            await ProcessBatchAsync(buffer).ConfigureAwait(false);
                            buffer.Clear();
                        }
                    }

                    if (buffer.Count == 0)
                    {
                        continue;
                    }

                    var waitTask = channel.Reader.WaitToReadAsync(token).AsTask();
                    var delayTask = Task.Delay(flushInterval, token);
                    var completed = await Task.WhenAny(waitTask, delayTask).ConfigureAwait(false);

                    if (completed == waitTask && await waitTask.ConfigureAwait(false))
                    {
                        while (buffer.Count < batchSize && channel.Reader.TryRead(out var more))
                        {
                            buffer.Add(more);
                            if (buffer.Count >= batchSize)
                            {
                                break;
                            }
                        }
                    }

                    await ProcessBatchAsync(buffer).ConfigureAwait(false);
                    buffer.Clear();
                }

                if (buffer.Count > 0)
                {
                    await ProcessBatchAsync(buffer).ConfigureAwait(false);
                    buffer.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                foreach (var pending in buffer)
                {
                    pending.Completion.TrySetCanceled(token);
                }

                while (channel.Reader.TryRead(out var leftover))
                {
                    leftover.Completion.TrySetCanceled(token);
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Error en lote BLAKE3: {ex.Message}");

                while (channel.Reader.TryRead(out var pending))
                {
                    pending.Completion.TrySetException(ex);
                }
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }

        private Task ProcessBatchAsync(List<HashRequest> batch)
        {
            return Task.Run(() =>
            {
                Parallel.ForEach(batch, new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism
                }, request =>
                {
                    string? hash = null;

                    try
                    {
                        if (File.Exists(request.FilePath))
                        {
                            // ERROR: hash = SlskDownCore.HashFileBlake3(request.FilePath);
                        }
                        else
                        {
                            log?.Invoke($"Archivo no encontrado para hash: {request.FilePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"Error calculando BLAKE3 ({Path.GetFileName(request.FilePath)}): {ex.Message}");
                    }
                    finally
                    {
                        request.Completion.TrySetResult(hash);
                    }
                });
            });
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            channel.Writer.TryComplete();

            try
            {
                worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Ignorar; el token probablemente haya cancelado la tarea
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }
    }
}
