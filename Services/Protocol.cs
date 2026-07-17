using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Buffers;
using LanCopy.Models;

namespace LanCopy.Services;

// Protocolo: JSON-line ('\n') + datos binarios opcionales inline.
// ReadLineAsync acumula bytes crudos y decodifica UTF-8 al final -> sin perdidas multibyte.
internal static class Protocol
{
    // Version del protocolo de aplicacion. Se anuncia en 'caps' para negociar compatibilidad.
    internal const int Version = 3;
    internal const int MinSupportedVersion = 1;

    internal const int BufferSize = 512 * 1024; // 512 KB -> ~20-30% mas throughput LAN 1Gbps
    internal const int MinBufferSize = 64 * 1024;
    internal const int MaxBufferSize = 2 * 1024 * 1024;

    // Q1: _readLock estático eliminado — serializaba TODAS las conexiones bajo un lock global.
    // TCP garantiza ordering por stream; ReadLineSlowAsync es per-stream y no necesita sincronización externa.

    // Limite anti-DoS: una linea de cabecera JSON nunca deberia superar esto.
    // Sin tope, un peer malicioso podria enviar bytes sin '\n' hasta agotar la memoria (OOM).
    internal const int MaxLineBytes = 16 * 1024 * 1024; // 16 MB: permite listados grandes sin eliminar el limite anti-DoS

    // True si el fichero ya esta (probablemente) comprimido y conviene saltar deflate.
    internal static bool IsCompressedExtension(string path)
        => FileEntry.IsAlreadyCompressed(path);

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
        // M15: ArrayPool en lugar de new List<byte>(256) + new byte[1] + ToArray().
        // ReadLineSlowAsync es el fallback para streams que no son BufferedLineStream.
        // En producción es raro (casi siempre se usa BufferedLineStream), pero la hygiene importa.
        var accumulator = ArrayPool<byte>.Shared.Rent(512); // capacidad típica de línea JSON
        var oneBuf      = ArrayPool<byte>.Shared.Rent(1);
        int len = 0;
        try
        {
            while (true)
            {
                var n = await stream.ReadAsync(oneBuf.AsMemory(0, 1), ct);
                if (n == 0) throw new EndOfStreamException("svc.connClosed");
                var b = oneBuf[0];
                if (b == (byte)'\n') break;
                if (b != (byte)'\r')
                {
                    if (len >= MaxLineBytes) throw new InvalidDataException("svc.lineTooLong");
                    if (len == accumulator.Length)
                    {
                        // Expandir: rentar un buffer mayor y copiar
                        var bigger = ArrayPool<byte>.Shared.Rent(accumulator.Length * 2);
                        accumulator.AsSpan(0, len).CopyTo(bigger);
                        ArrayPool<byte>.Shared.Return(accumulator);
                        accumulator = bigger;
                    }
                    accumulator[len++] = b;
                }
            }
            return Encoding.UTF8.GetString(accumulator, 0, len);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(accumulator);
            ArrayPool<byte>.Shared.Return(oneBuf);
        }
    }

    // ─── P1: Respuestas comunes pre-serializadas ───────────────────────────────────────
    // JsonSerializer.Serialize(new { status = "ok" }) en FileServer.cs se llama 9 veces
    // y JsonSerializer.Serialize(new { status = "error", error = "svc.*" }) ~57 veces.
    // Cada llamada aloca: 1 anonymous object + 1 string JSON + 1 encoding UTF-8 en WriteLineAsync.
    // Con bytes estáticos: 0 allocs, 1 WriteAsync directo (misma ruta que WriteLineAsync ya tiene).

    // Respuesta {"status":"ok"}\n — pre-calculada una sola vez al inicio de la app
    private static readonly ReadOnlyMemory<byte> OkResponseBytes =
        System.Text.Encoding.UTF8.GetBytes("{\"status\":\"ok\"}\n");

    // Cache de respuestas de error estáticas: "svc.xxx" -> bytes pre-calculados
    private static readonly System.Collections.Generic.Dictionary<string, ReadOnlyMemory<byte>> ErrorResponseCache
        = new(System.StringComparer.Ordinal);

    private static ReadOnlyMemory<byte> GetErrorBytes(string errorCode)
    {
        if (!ErrorResponseCache.TryGetValue(errorCode, out var cached))
        {
            cached = System.Text.Encoding.UTF8.GetBytes($"{{\"status\":\"error\",\"error\":\"{errorCode}\"}}\n");
            // Sin lock: la race condition es benigna (sobrescribe con el mismo valor)
            ErrorResponseCache[errorCode] = cached;
        }
        return cached;
    }

    /// <summary>
    /// P1: Envía {"status":"ok"}\n con bytes pre-calculados — 0 allocs vs JsonSerializer.Serialize(new{status="ok"}).
    /// </summary>
    internal static async Task WriteOkAsync(Stream stream, CancellationToken ct)
    {
        await stream.WriteAsync(OkResponseBytes, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>
    /// P1: Envía {"status":"error","error":"svc.xxx"}\n con bytes cacheados — 0 allocs.
    /// Solo para errores estáticos con código de error fijo (no para mensajes dinámicos).
    /// </summary>
    internal static async Task WriteErrorAsync(Stream stream, string errorCode, CancellationToken ct)
    {
        await stream.WriteAsync(GetErrorBytes(errorCode), ct);
        await stream.FlushAsync(ct);
    }

    internal static async Task WriteLineAsync(Stream stream, string json, CancellationToken ct)
    {
        // P6: evitar aloc por línea — ArrayPool<byte> + sin concatenar "\n"
        var byteCount = Encoding.UTF8.GetByteCount(json) + 1; // +1 para '\n'
        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(json, rented);
            rented[written] = (byte)'\n';
            await stream.WriteAsync(rented.AsMemory(0, written + 1), ct);
            await stream.FlushAsync(ct);
        }
        finally { ArrayPool<byte>.Shared.Return(rented); }
    }

    // N2: static NewLineBytes evita "\n"u8.ToArray() que aloca new byte[1] en cada llamada
    private static readonly ReadOnlyMemory<byte> NewLineBytes = new byte[] { (byte)'\n' };

    /// <summary>
    /// M2: Versión optimizada para objetos grandes (ej: listas de FileEntry).
    /// SerializeToUtf8Bytes serializa directamente a bytes, eliminando la representación
    /// UTF-16 intermedia de JsonSerializer.Serialize(string).
    /// N2: JSON + '\n' se combinan en un solo buffer del pool — 1 escritura al stream (antes 2),
    /// un solo FlushAsync, y 0 allocs en el terminador de línea.
    /// </summary>
    internal static async Task WriteLineJsonAsync<T>(Stream stream, T obj, CancellationToken ct)
    {
        var utf8Json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj);
        // N2: copiar json + '\n' en un buffer del pool para 1 sola escritura (menos syscalls / menos TLS records)
        var total  = utf8Json.Length + 1;
        var rented = ArrayPool<byte>.Shared.Rent(total);
        try
        {
            utf8Json.CopyTo(rented, 0);
            rented[utf8Json.Length] = (byte)'\n';
            await stream.WriteAsync(rented.AsMemory(0, total), ct);
            await stream.FlushAsync(ct);
        }
        finally { ArrayPool<byte>.Shared.Return(rented); }
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
        // P2: usar ArrayPool para el acumulador — evita List<byte> + ToArray() (2 alocs por línea)
        var rent = System.Buffers.ArrayPool<byte>.Shared.Rent(512);
        var used = 0;
        try
        {
            while (true)
            {
                if (_pos >= _len && !await FillAsync(ct))
                    throw new EndOfStreamException("svc.connClosed");
                while (_pos < _len)
                {
                    var b = _buf[_pos++];
                    if (b == (byte)'\n')
                        return Encoding.UTF8.GetString(rent, 0, used);
                    if (b != (byte)'\r')
                    {
                        if (used >= Protocol.MaxLineBytes)
                            throw new InvalidDataException("svc.lineTooLong");
                        if (used >= rent.Length)
                        {
                            var bigger = System.Buffers.ArrayPool<byte>.Shared.Rent(rent.Length * 2);
                            rent.AsSpan(0, used).CopyTo(bigger);
                            System.Buffers.ArrayPool<byte>.Shared.Return(rent);
                            rent = bigger;
                        }
                        rent[used++] = b;
                    }
                }
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rent);
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