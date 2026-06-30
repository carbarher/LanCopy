using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Buffers;

namespace LanCopy.Services;

// Protocolo: JSON-line ('\n') + datos binarios opcionales inline.
// ReadLineAsync acumula bytes crudos y decodifica UTF-8 al final -> sin perdidas multibyte.
internal static class Protocol
{
    // Version del protocolo de aplicacion. Se anuncia en 'caps' para negociar compatibilidad.
    internal const int Version = 1;
    internal const int MinSupportedVersion = 1;

    internal const int BufferSize = 512 * 1024; // 512 KB -> ~20-30% mas throughput LAN 1Gbps
    internal const int MinBufferSize = 64 * 1024;
    internal const int MaxBufferSize = 2 * 1024 * 1024;

    // Limite anti-DoS: una linea de cabecera JSON nunca deberia superar esto.
    // Sin tope, un peer malicioso podria enviar bytes sin '\n' hasta agotar la memoria (OOM).
    internal const int MaxLineBytes = 1024 * 1024; // 1 MB

    // Extensiones cuyo contenido ya esta comprimido: deflate no aporta y solo gasta CPU.
    private static readonly HashSet<string> CompressedExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".gz", ".7z", ".rar", ".bz2", ".xz", ".zst", ".lz4", ".cab",
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif",
        ".mp3", ".aac", ".ogg", ".opus", ".flac", ".m4a", ".wma",
        ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v", ".wmv",
        ".docx", ".xlsx", ".pptx", ".odt", ".ods", ".odp", ".epub", ".apk", ".jar"
    };

    // True si el fichero ya esta (probablemente) comprimido y conviene saltar deflate.
    internal static bool IsCompressedExtension(string path)
        => CompressedExt.Contains(System.IO.Path.GetExtension(path));

    internal static int SelectCopyBufferSize(long totalBytes)
    {
        if (totalBytes <= 0) return BufferSize;
        if (totalBytes <= 256L * 1024) return MinBufferSize;
        if (totalBytes <= 8L * 1024 * 1024) return 256 * 1024;
        if (totalBytes <= 512L * 1024 * 1024) return BufferSize;
        return 1024 * 1024;
    }

    // Lee una linea terminada en '\n'. Si el stream es un BufferedLineStream (lo habitual en
    // servidor/cliente), usa su lectura con buffer (sin 1 syscall por byte). Para cualquier otro
    // stream se usa el fallback byte a byte (seguro: no consume el payload binario que sigue).
    internal static Task<string> ReadLineAsync(Stream stream, CancellationToken ct)
        => stream is BufferedLineStream bls
            ? bls.ReadLineAsync(ct)
            : ReadLineSlowAsync(stream, ct);

    private static async Task<string> ReadLineSlowAsync(Stream stream, CancellationToken ct)
    {
        var bytes = new List<byte>(256);
        var buf = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(buf.AsMemory(0, 1), ct);
            if (n == 0) throw new EndOfStreamException("svc.connClosed");
            if (buf[0] == (byte)'\n') break;
            if (buf[0] != (byte)'\r')
            {
                // BUG-005: Check buffer size before adding to prevent overflow
                if (bytes.Count >= MaxLineBytes)
                    throw new InvalidDataException("svc.lineTooLong");
                bytes.Add(buf[0]);
            }
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    internal static async Task WriteLineAsync(Stream stream, string json, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    internal static async Task CopyExactAsync(
        Stream src, Stream dst, long size,
        IProgress<(long done, long total)>? progress,
        CancellationToken ct)
    {
        var rented = ArrayPool<byte>.Shared.Rent(Math.Clamp(SelectCopyBufferSize(size), MinBufferSize, MaxBufferSize));
        try
        {
            var buf = rented;
            long remaining = size, done = 0;
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(remaining, buf.Length);
                var read = await src.ReadAsync(buf.AsMemory(0, toRead), ct);
                if (read == 0) throw new EndOfStreamException("svc.connCut");
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                await RateLimiter.Global.ThrottleAsync(read, ct);
                remaining -= read;
                done += read;
                progress?.Report((done, size));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // Lee y descarta exactamente 'size' bytes del stream (para drenar un cuerpo rechazado
    // sin escribirlo a disco; evita el RST que perderia el ack de error en el cliente).
    internal static async Task DrainAsync(Stream src, long size, CancellationToken ct)
    {
        var rented = ArrayPool<byte>.Shared.Rent(Math.Clamp(SelectCopyBufferSize(size), MinBufferSize, MaxBufferSize));
        try
        {
            var buf = rented;
            long remaining = size;
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(remaining, buf.Length);
                var read = await src.ReadAsync(buf.AsMemory(0, toRead), ct);
                if (read == 0) break;
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // Copia exacta calculando el hash en streaming (1 sola pasada de I/O).
    // Devuelve el hash en hex minuscula. Evita re-leer el fichero para hashear.
    internal static async Task<string> CopyExactToHashAsync(
        Stream src, Stream dst, long size,
        IProgress<(long done, long total)>? progress,
        CancellationToken ct)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Clamp(SelectCopyBufferSize(size), MinBufferSize, MaxBufferSize));
        try
        {
            var buf = rented;
            long remaining = size, done = 0;
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(remaining, buf.Length);
                var read = await src.ReadAsync(buf.AsMemory(0, toRead), ct);
                if (read == 0) throw new EndOfStreamException("svc.connCut");
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                hasher.AppendData(buf, 0, read);
                await RateLimiter.Global.ThrottleAsync(read, ct);
                remaining -= read;
                done += read;
                progress?.Report((done, size));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
        return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    }
}

// Envoltura con buffer de lectura. Lee la cabecera JSON con un buffer interno (no 1 byte por
// syscall) y, lo critico: cualquier byte sobrante del buffer (que ya pertenece al payload binario)
// se devuelve por Read/ReadAsync antes de tocar el stream subyacente. Asi CopyExactAsync sigue
// funcionando sin perder bytes. Las escrituras pasan directas al stream interno.
internal sealed class BufferedLineStream(Stream inner, bool leaveOpen = true, int bufferSize = 16 * 1024) : Stream
{
    private readonly byte[] _buf = new byte[bufferSize];
    private int _pos;
    private int _len;

    private async ValueTask<bool> FillAsync(CancellationToken ct)
    {
        _len = await inner.ReadAsync(_buf, ct);
        _pos = 0;
        return _len > 0;
    }

    public async Task<string> ReadLineAsync(CancellationToken ct)
    {
        var acc = new List<byte>(256);
        while (true)
        {
            if (_pos >= _len && !await FillAsync(ct))
                throw new EndOfStreamException("svc.connClosed");
            while (_pos < _len)
            {
                var b = _buf[_pos++];
                if (b == (byte)'\n') return Encoding.UTF8.GetString(acc.ToArray());
                if (b != (byte)'\r')
                {
                    acc.Add(b);
                    if (acc.Count > Protocol.MaxLineBytes)
                        throw new InvalidDataException("svc.lineTooLong");
                }
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_pos < _len)
        {
            var n = Math.Min(count, _len - _pos);
            Array.Copy(_buf, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }
        return inner.Read(buffer, offset, count);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_pos < _len)
        {
            var n = Math.Min(buffer.Length, _len - _pos);
            _buf.AsSpan(_pos, n).CopyTo(buffer.Span);
            _pos += n;
            return n;
        }
        return await inner.ReadAsync(buffer, ct);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => inner.WriteAsync(buffer, ct);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => inner.WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);

    public override bool CanRead => true;
    public override bool CanWrite => inner.CanWrite;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen) inner.Dispose();
        base.Dispose(disposing);
    }
}