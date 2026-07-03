using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using LanCopy.Cli;
using Xunit;

namespace LanCopy.Tests;

public class TransferRuntimeTests
{
    [Fact]
    public async Task Retry_PreservesPinForRequeuedJob()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "lancopy-transfer-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var persistPath = Path.Combine(tempRoot, "cli-transfer-jobs.json");
        var localFile = Path.Combine(tempRoot, "payload.bin");
        await File.WriteAllBytesAsync(localFile, [1, 2, 3, 4]);

        try
        {
            using var runtime = new TransferRuntime(persistPath);
            var id = runtime.EnqueueSend(new SendRequest
            {
                LocalPath = localFile,
                To = "127.0.0.1:1",
                RemotePath = "payload.bin",
                Pin = "1234",
                UseTls = false,
                UseCompress = false
            });

            await WaitForTerminalStateAsync(runtime, id, TimeSpan.FromSeconds(8));

            var retry = runtime.Retry(id);
            Assert.Equal(RetryStatus.Enqueued, retry.Status);
            Assert.False(string.IsNullOrWhiteSpace(retry.RetryId));
            Assert.Equal("1234", GetJobPin(runtime, retry.RetryId!));
            await WaitForTerminalStateAsync(runtime, retry.RetryId!, TimeSpan.FromSeconds(8));
        }
        finally
        {
            DeleteDirectoryWithRetry(tempRoot);
        }
    }

    [Fact]
    public async Task Cancel_OnTerminalJob_ReturnsAlreadyTerminal()
    {
        // A job that fails immediately (port 1 is unreachable) should transition to "failed".
        // Calling Cancel() after it reaches terminal state must return AlreadyTerminal, not throw.
        var tempRoot = Path.Combine(Path.GetTempPath(), "lancopy-cancel-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var localFile = Path.Combine(tempRoot, "f.bin");
        await File.WriteAllBytesAsync(localFile, [1, 2, 3]);

        try
        {
            using var runtime = new TransferRuntime(Path.Combine(tempRoot, "jobs.json"));
            var id = runtime.EnqueueSend(new SendRequest
            {
                LocalPath = localFile,
                To = "127.0.0.1:1",
                RemotePath = "f.bin",
                UseTls = false,
                UseCompress = false
            });

            await WaitForTerminalStateAsync(runtime, id, TimeSpan.FromSeconds(8));

            // Should NOT throw ObjectDisposedException (CTS already disposed by finished job).
            var result = runtime.Cancel(id);
            Assert.Equal(CancelResult.AlreadyTerminal, result);
        }
        finally
        {
            DeleteDirectoryWithRetry(tempRoot);
        }
    }

    [Fact]
    public async Task Dispose_WithTerminalJobs_DoesNotThrow()
    {
        // Dispose() called after all jobs have completed must not throw even if PersistSnapshot
        // tries to write (regression test for the try/catch fix in Dispose).
        var tempRoot = Path.Combine(Path.GetTempPath(), "lancopy-dispose-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var localFile = Path.Combine(tempRoot, "x.bin");
        await File.WriteAllBytesAsync(localFile, [9, 8, 7]);

        try
        {
            var runtime = new TransferRuntime(Path.Combine(tempRoot, "jobs.json"));
            var id = runtime.EnqueueSend(new SendRequest
            {
                LocalPath = localFile,
                To = "127.0.0.1:1",
                UseTls = false,
                UseCompress = false
            });
            await WaitForTerminalStateAsync(runtime, id, TimeSpan.FromSeconds(8));

            var ex = Record.Exception(() => runtime.Dispose());
            Assert.Null(ex);
        }
        finally
        {
            DeleteDirectoryWithRetry(tempRoot);
        }
    }

    private static async Task WaitForTerminalStateAsync(TransferRuntime runtime, string id, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var status = runtime.GetStatus(id);
            if (status != null && (status.State is "completed" or "failed" or "canceled"))
                return;
            await Task.Delay(100);
        }

        throw new TimeoutException($"Transfer {id} did not reach terminal state in {timeout.TotalSeconds:0.##}s.");
    }

    private static string? GetJobPin(TransferRuntime runtime, string id)
    {
        var jobsField = typeof(TransferRuntime).GetField("_jobs", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(jobsField);
        var jobs = jobsField!.GetValue(runtime) as ConcurrentDictionary<string, TransferJob>;
        Assert.NotNull(jobs);
        Assert.True(jobs!.TryGetValue(id, out var job), $"Transfer job {id} not found.");
        return job!.Pin;
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        if (!Directory.Exists(path)) return;
        for (var i = 0; i < 10; i++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (i < 9)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (i < 9)
            {
                Thread.Sleep(100);
            }
        }
    }
}
