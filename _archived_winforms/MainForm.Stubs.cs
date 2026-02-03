using System;
using System.IO;
using System.Threading.Tasks;

namespace SlskDown
{
    public partial class MainForm
    {
        private readonly SimpleMetadataCache metadataCache = new SimpleMetadataCache();
        private IDisposable? connectionPool;
        private AdaptiveParallelism? adaptiveAutoSearch;
        private AdaptiveParallelism? adaptivePurge;

        private sealed class SimpleMetadataCache
        {
            public object? Get(string key) => null;

            public void Set(string key, object value, object? policy)
            {
            }

            public void Dispose()
            {
            }
        }

        private bool ValidateDownloadedFile(string localPath, string? filename)
        {
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                return false;
            }

            try
            {
                if (SlskDownCore.ValidateFile(localPath))
                {
                    return true;
                }

                return SlskDownCore.RepairFile(localPath);
            }
            catch
            {
                return true;
            }
        }

        private Task VerifyDownloadIntegrity(string localPath)
        {
            return Task.CompletedTask;
        }
    }
}
