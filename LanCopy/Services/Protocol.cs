using System.Net.Sockets;
using System.Text;

namespace LanCopy.Services;

// Protocolo: JSON-line ('\n') + datos binarios opcionales inline.
// ReadLineAsync acumula bytes crudos y decodifica UTF-8 al final → sin pérdidas multibyte.
internal static class Protocol
{
    internal const int BufferSize = 512 * 1024; // 512 KB — ~20-30% más throughput LAN 1Gbps

    internal static async Task<string> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var bytes = new List<byte>(256);
        var buf = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(buf.AsMemory(0, 1), ct);
            if (n == 0) throw new EndOfStreamException("Conexión cerrada");
            if (buf[0] == (byte)'\n') break;
            if (buf[0] != (byte)'\r') bytes.Add(buf[0]);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    internal static async Task WriteLineAsync(Stream stream, string json, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await stream.WriteAsync(bytes, ct);
    }

    internal static async Task CopyExactAsync(
        Stream src, Stream dst, long size,
        IProgress<(long done, long total)>? progress,
        CancellationToken ct)
    {
        var buf = new byte[BufferSize];
        long remaining = size, done = 0;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(remaining, buf.Length);
            var read = await src.ReadAsync(buf.AsMemory(0, toRead), ct);
            if (read == 0) throw new EndOfStreamException("Conexión cortada durante la transferencia");
            await dst.WriteAsync(buf.AsMemory(0, read), ct);
            remaining -= read;
            done += read;
            progress?.Report((done, size));
        }
    }
}
