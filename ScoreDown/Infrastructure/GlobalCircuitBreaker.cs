using System;
using System.Threading;

namespace ScoreDown.Infrastructure;

/// <summary>
/// Global circuit breaker for catalog operations.
/// Prevents hammering servers when rate limits detected.
/// Transitions: Closed → Open (on consecutive errors) → HalfOpen (after cooldown).
/// </summary>
public class GlobalCircuitBreaker
{
    private int _consecutiveErrors = 0;
    private DateTime _lastErrorTime = DateTime.MinValue;
    private CircuitState _state = CircuitState.Closed;
    private readonly object _lockObj = new();

    // Configuration
    public int ErrorThreshold { get; set; } = 10;  // Open after 10 consecutive errors
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);  // Wait 5 min before retry
    public TimeSpan ErrorResetWindow { get; set; } = TimeSpan.FromMinutes(2);  // Reset counter if 2 min passed

    public CircuitState State
    {
        get
        {
            lock (_lockObj)
                return _state;
        }
    }

    public void RecordSuccess()
    {
        lock (_lockObj)
        {
            _consecutiveErrors = 0;
            _state = CircuitState.Closed;
        }
    }

    public void RecordError()
    {
        lock (_lockObj)
        {
            var now = DateTime.Now;

            // Reset counter if enough time has passed since last error
            if ((now - _lastErrorTime) > ErrorResetWindow)
                _consecutiveErrors = 0;

            _consecutiveErrors++;
            _lastErrorTime = now;

            if (_consecutiveErrors >= ErrorThreshold)
            {
                _state = CircuitState.Open;
            }
        }
    }

    public bool AllowRequest()
    {
        lock (_lockObj)
        {
            if (_state == CircuitState.Closed)
                return true;

            if (_state == CircuitState.Open)
            {
                // Check if cooldown period has passed
                if ((DateTime.Now - _lastErrorTime) > CooldownPeriod)
                {
                    _state = CircuitState.HalfOpen;
                    _consecutiveErrors = 0;
                    return true;
                }
                return false;  // Still in cooldown
            }

            // HalfOpen: allow request to test if service recovered
            return true;
        }
    }

    public TimeSpan TimeUntilRetry()
    {
        lock (_lockObj)
        {
            if (_state != CircuitState.Open)
                return TimeSpan.Zero;

            var elapsed = DateTime.Now - _lastErrorTime;
            var remaining = CooldownPeriod - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public void Reset()
    {
        lock (_lockObj)
        {
            _consecutiveErrors = 0;
            _lastErrorTime = DateTime.MinValue;
            _state = CircuitState.Closed;
        }
    }

    public string GetStatus()
    {
        lock (_lockObj)
        {
            return _state switch
            {
                CircuitState.Closed => $"OK ({_consecutiveErrors}/{ErrorThreshold} errors)",
                CircuitState.Open => $"BLOCKED - retry in {TimeUntilRetry().TotalSeconds:F0}s",
                CircuitState.HalfOpen => "TESTING recovery",
                _ => "UNKNOWN"
            };
        }
    }
}

public enum CircuitState
{
    Closed,      // Normal operation
    Open,        // Too many errors, reject requests
    HalfOpen     // Testing if service recovered
}
