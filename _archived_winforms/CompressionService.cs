using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace SlskDown
{
    public class CompressionService
    {
        private Action<string> logAction;
        
        public CompressionService(Action<string> logger)
        {
            logAction = logger;
        }
        
        public async Task<byte[]> CompressAsync(byte[] data)
        {
            try
            {
                using (var output = new MemoryStream())
                {
                    using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
                    {
                        await gzip.WriteAsync(data, 0, data.Length);
                    }
                    
                    var compressed = output.ToArray();
                    var ratio = (1 - (compressed.Length / (double)data.Length)) * 100;
                    
                    logAction?.Invoke($"📦 Comprimido: {data.Length} → {compressed.Length} bytes ({ratio:F1}% reducción)");
                    
                    return compressed;
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error comprimiendo: {ex.Message}");
                return data;
            }
        }
        
        public async Task<byte[]> DecompressAsync(byte[] data)
        {
            try
            {
                using (var input = new MemoryStream(data))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    await gzip.CopyToAsync(output);
                    return output.ToArray();
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error descomprimiendo: {ex.Message}");
                return data;
            }
        }
        
        public async Task CompressFileAsync(string inputFile, string outputFile)
        {
            try
            {
                using (var input = File.OpenRead(inputFile))
                using (var output = File.Create(outputFile))
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                {
                    await input.CopyToAsync(gzip);
                }
                
                var inputSize = new FileInfo(inputFile).Length;
                var outputSize = new FileInfo(outputFile).Length;
                var ratio = (1 - (outputSize / (double)inputSize)) * 100;
                
                logAction?.Invoke($"📦 Archivo comprimido: {inputSize} → {outputSize} bytes ({ratio:F1}% reducción)");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error comprimiendo archivo: {ex.Message}");
            }
        }
        
        public async Task DecompressFileAsync(string inputFile, string outputFile)
        {
            try
            {
                using (var input = File.OpenRead(inputFile))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = File.Create(outputFile))
                {
                    await gzip.CopyToAsync(output);
                }
                
                logAction?.Invoke($"📦 Archivo descomprimido: {outputFile}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error descomprimiendo archivo: {ex.Message}");
            }
        }
    }
}
