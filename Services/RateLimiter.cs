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
        set
        {
            var clamped = value < 0 ? 0 : value;
            Interlocked.Exchange(ref _bytesPerSec, clamped);
            // B7: cap _allowance al nuevo límite para evitar crédito stale tras cambio de slider
            lock (_lock) { if (clamped <= 0) _allowance = 0; else if (_allowance > clamped) _allowance = clamped; }
        }
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
            if (_allowance > rate) _allowance = rate; // tope de ráfaga ~1s
            _allowance -= bytes;
            // B3: clamp al máximo déficit de 1s — sin esto, una ráfaga grande deja _allowance muy negativo
            // y los chunks siguientes se retrasan mucho más de lo esperado hasta recuperar el balance
            if (_allowance < -rate) _allowance = -rate;
            if (_allowance < 0)
                // B4: clamp a 60s — evita overflow a int negativo con rate muy baja + bytes grandes
                delayMs = (int)Math.Min(-_allowance / rate * 1000.0, 60_000.0);
        }
        if (delayMs > 0)
            await Task.Delay(delayMs, ct);
    }
}
