using System.Threading;
using System.Threading.Tasks;

namespace LanCopy.Services;

// Limitador de ancho de banda global (token-bucket). BytesPerSecond = 0 => ilimitado.
// Usado por Protocol en los bucles de copia para no saturar la red.
public sealed class RateLimiter
{
    public static readonly RateLimiter Global = new();

    private long _bytesPerSec; // 0 = ilimitado
    private readonly object _lock = new();
    private double _allowance;
    private long _lastTicks;

    public long BytesPerSecond
    {
        get => Interlocked.Read(ref _bytesPerSec);
        set => Interlocked.Exchange(ref _bytesPerSec, value < 0 ? 0 : value);
    }

    // Bloquea (sin spin) el tiempo necesario para no exceder la tasa configurada.
    public async Task ThrottleAsync(int bytes, CancellationToken ct)
    {
        var rate = BytesPerSecond;
        if (rate <= 0 || bytes <= 0) return;

        int delayMs = 0;
        lock (_lock)
        {
            var now = Environment.TickCount64;
            if (_lastTicks == 0) { _lastTicks = now; _allowance = rate; }
            var elapsed = (now - _lastTicks) / 1000.0;
            _lastTicks = now;
            _allowance += elapsed * rate;
            if (_allowance > rate) _allowance = rate; // tope de rafaga ~1s
            _allowance -= bytes;
            if (_allowance < 0)
                delayMs = (int)(-_allowance / rate * 1000.0);
        }
        if (delayMs > 0)
            await Task.Delay(delayMs, ct);
    }
}
