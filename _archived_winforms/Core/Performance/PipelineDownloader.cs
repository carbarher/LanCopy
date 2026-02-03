using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core.Performance
{
    public class PipelineDownloader
    {
        private const int BUFFER_SIZE = 65536; // 64KB

        public async Task DownloadWithPipelineAsync(Stream networkStream, string filePath, CancellationToken cancellationToken)
        {
            var pipe = new Pipe();
            
            Task writingTask = FillPipeAsync(networkStream, pipe.Writer, cancellationToken);
            Task readingTask = ReadFromPipeAsync(filePath, pipe.Reader, cancellationToken);

            await Task.WhenAll(writingTask, readingTask);
        }

        private async Task FillPipeAsync(Stream source, PipeWriter writer, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Memory<byte> memory = writer.GetMemory(BUFFER_SIZE);
                int bytesRead = await source.ReadAsync(memory, cancellationToken);
                
                if (bytesRead == 0) break;

                writer.Advance(bytesRead);
                FlushResult result = await writer.FlushAsync(cancellationToken);

                if (result.IsCompleted) break;
            }

            await writer.CompleteAsync();
        }

        private async Task ReadFromPipeAsync(string filePath, PipeReader reader, CancellationToken cancellationToken)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, true);

            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                foreach (var segment in buffer)
                {
                    await fileStream.WriteAsync(segment, cancellationToken);
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted) break;
            }

            await reader.CompleteAsync();
        }
    }
}
