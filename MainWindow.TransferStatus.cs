using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using LanCopy.Models;
using LanCopy.Services;

namespace LanCopy;

public partial class MainWindow
{
    private static readonly SolidColorBrush TransferActiveBrushA = SolidColorBrush.Parse("#28A745");
    private static readonly SolidColorBrush TransferActiveBrushB = SolidColorBrush.Parse("#41C96A");
    private static readonly SolidColorBrush TransferCompletedBrush = SolidColorBrush.Parse("#28A745");
    private static readonly SolidColorBrush TransferStalledBrush = SolidColorBrush.Parse("#F0AD4E");
    private static readonly SolidColorBrush TransferStalledTextBrush = SolidColorBrush.Parse("#F0AD4E");

    private readonly object _transferUiLock = new();
    private DispatcherTimer? _transferUiTimer;
    private TransferUiState? _transferUiState;
    private bool _transferPulseOn;

    private void InitializeTransferUi()
    {
        _transferUiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _transferUiTimer.Tick += (_, _) =>
        {
            try
            {
                _transferPulseOn = !_transferPulseOn;
                var snapshot = CreateTransferUiSnapshot();
                if (snapshot is TransferUiSnapshot current)
                {
                    ApplyTransferUiSnapshot(current);
                    TryAutoRecoverFromStall(current);
                }
                if (snapshot is null || snapshot.Value.IsCompleted || snapshot.Value.IsTerminal)
                    _transferUiTimer?.Stop();
            }
            catch (Exception ex)
            {
                Log.Debug("transfer", "transfer-ui-timer-tick-failed", new { error = ex.Message });
                _transferUiTimer?.Stop();
            }
        };


        Dispatcher.UIThread.Post(() =>
        {
            if (_progressBar != null)
            {
                _progressBar.Value = 0;
                _progressBar.Foreground = TransferActiveBrushA;
            }

            if (_txtProgressPercent != null) _txtProgressPercent.Text = "0%";
            if (_txtSpeed != null) _txtSpeed.Text = "";
        });
    }

    private void BeginTransferUi(bool receiving, long totalBytes)
    {
        lock (_transferUiLock)
        {
            _transferUiState = new TransferUiState(receiving, Math.Max(0, totalBytes), DateTimeOffset.UtcNow);
        }

        StartTransferUiTimer();
        RenderTransferUi();
    }

    private void ReportTransferUi(bool receiving, long doneBytes, long totalBytes)
    {
        lock (_transferUiLock)
        {
            var now = DateTimeOffset.UtcNow;
            var state = EnsureTransferUiState(receiving, totalBytes, now);
            var clampedDone = Math.Max(0, doneBytes);
            if (state.TotalBytes > 0)
                clampedDone = Math.Min(clampedDone, state.TotalBytes);

            if (clampedDone < state.DoneBytes)
            {
                state = _transferUiState = new TransferUiState(receiving, Math.Max(0, totalBytes), now);
            }

            var delta = Math.Max(0, clampedDone - state.DoneBytes);
            state.DoneBytes = clampedDone;
            state.TotalBytes = Math.Max(state.TotalBytes, Math.Max(0, totalBytes));
            state.LastUpdateAt = now;

            if (delta > 0)
            {
                state.LastByteAt = now;
                state.SpeedSamples.Enqueue(new TransferDeltaSample(now, delta));
            }

            TrimSpeedSamples(state, now);

            if (state.TotalBytes > 0 && state.DoneBytes >= state.TotalBytes)
            {
                state.CompletedElapsed = now - state.StartedAt;
                state.IsCompleted = true;
            }
        }

        StartTransferUiTimer();
        RenderTransferUi();
    }

    private void CompleteTransferUi(bool receiving, long doneBytes, long totalBytes, TimeSpan elapsed)
    {
        lock (_transferUiLock)
        {
            var now = DateTimeOffset.UtcNow;
            var state = EnsureTransferUiState(receiving, totalBytes, now);
            state.DoneBytes = Math.Max(0, Math.Min(doneBytes, Math.Max(doneBytes, totalBytes)));
            state.TotalBytes = Math.Max(state.TotalBytes, Math.Max(doneBytes, totalBytes));
            state.LastUpdateAt = now;
            state.LastByteAt = now;
            state.CompletedElapsed = elapsed > TimeSpan.Zero ? elapsed : now - state.StartedAt;
            state.IsCompleted = true;
        }

        RenderTransferUi();
        StopTransferUiTimer();
    }

    private void ResetTransferUi()
    {
        lock (_transferUiLock)
        {
            _transferUiState = null;
        }

        StopTransferUiTimer();
        Dispatcher.UIThread.Post(() =>
        {
            if (_progressBar != null)
            {
                _progressBar.Value = 0;
                _progressBar.Foreground = TransferActiveBrushA;
            }

            if (_txtProgressPercent != null) _txtProgressPercent.Text = "0%";
            if (_txtSpeed != null) _txtSpeed.Text = "";
            UpdateSparkline(0);
            if (_tray != null) _tray.ToolTipText = "LanCopy";
        });
    }

    private void SuspendTransferUi()
    {
        lock (_transferUiLock)
        {
            _transferUiState = null;
        }

        StopTransferUiTimer();
        Dispatcher.UIThread.Post(() =>
        {
            if (_tray != null) _tray.ToolTipText = "LanCopy";
        });
    }

    private TransferUiState EnsureTransferUiState(bool receiving, long totalBytes, DateTimeOffset now)
    {
        var normalizedTotal = Math.Max(0, totalBytes);
        if (_transferUiState is null ||
            _transferUiState.IsCompleted ||
            _transferUiState.IsTerminal ||
            _transferUiState.Receiving != receiving ||
            (normalizedTotal > 0 && _transferUiState.TotalBytes > 0 && normalizedTotal != _transferUiState.TotalBytes && _transferUiState.DoneBytes == 0))
        {
            _transferUiState = new TransferUiState(receiving, normalizedTotal, now);
        }
        else if (normalizedTotal > 0)
        {
            _transferUiState.TotalBytes = normalizedTotal;
        }

        return _transferUiState;
    }

    private void StartTransferUiTimer() =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_transferUiTimer != null && !_transferUiTimer.IsEnabled)
                _transferUiTimer.Start();
        });

    private void StopTransferUiTimer() =>
        Dispatcher.UIThread.Post(() => _transferUiTimer?.Stop());

    private void RenderTransferUi()
    {
        var snapshot = CreateTransferUiSnapshot();
        if (snapshot == null) return;
        var current = snapshot.Value;
        Dispatcher.UIThread.Post(() => ApplyTransferUiSnapshot(current));
    }

    private TransferUiSnapshot? CreateTransferUiSnapshot()
    {
        lock (_transferUiLock)
        {
            if (_transferUiState is null) return null;

            var state = _transferUiState;
            var now = DateTimeOffset.UtcNow;
            TrimSpeedSamples(state, now);

            var pct = state.TotalBytes > 0
                ? Math.Clamp(state.DoneBytes * 100.0 / state.TotalBytes, 0, 100)
                : 0;
            // B9-FIX: Evitar spike de velocidad al inicio cuando solo hay 1 sample y la
            // ventana es < 1s (causaba mostrar cientos de MB/s). Suprimir hasta 1s de ventana.
            var recentBytes = state.SpeedSamples.Sum(x => x.Bytes);
            var rawWindow = state.SpeedSamples.Count > 0
                // P4: SpeedSamples es una Queue FIFO — el más antiguo es siempre el primero; Peek() es O(1) vs Min() O(n)
                ? (now - state.SpeedSamples.Peek().Timestamp).TotalSeconds
                : 2.0;
            var sampleWindow = Math.Max(0.5, rawWindow);
            // Si la ventana real es < 1s y sólo hay 1 muestra, la velocidad es ruido: mostrar 0
            var speed = (recentBytes > 0 && (state.SpeedSamples.Count > 1 || rawWindow >= 1.0))
                ? recentBytes / sampleWindow
                : 0;
            var stalledSeconds = Math.Max(0, (int)Math.Floor((now - state.LastByteAt).TotalSeconds));
            var isStalled = !state.IsCompleted && !state.IsTerminal && state.DoneBytes < state.TotalBytes && stalledSeconds >= 10;
            var directionText = state.Receiving ? L["st.receiving"] : L["st.sending"];
            var percentText = $"{FormatPercent(pct)}%";

            var statusText = directionText;
            var detailText = "";
            var brush = TransferCompletedBrush;
            IBrush? statusBrush = null;

            if (state.IsCompleted)
            {
                statusText = L.Format("st.transferComplete", FormatTransferSize(state.TotalBytes), FormatElapsed(state.CompletedElapsed));
                detailText = FormatElapsed(state.CompletedElapsed);
                pct = 100;
                brush = TransferCompletedBrush;
            }
            else if (state.IsTerminal)
            {
                statusText = L.Format("st.transferPartial", FormatTransferSize(state.DoneBytes), FormatTransferSize(state.TotalBytes));
                detailText = percentText;
                brush = TransferStalledBrush;
                statusBrush = TransferStalledTextBrush;
            }
            else if (isStalled)
            {
                statusText = L.Format("st.stalled", stalledSeconds);
                detailText = L.Format("st.transferStalledDetail", stalledSeconds);
                brush = TransferStalledBrush;
                statusBrush = TransferStalledTextBrush;
            }
            else
            {
                // U4: mostrar ETA calculado con la velocidad actual de los samples
                var remainingBytes = state.TotalBytes - state.DoneBytes;
                var etaSeconds = speed > 0 ? remainingBytes / speed : 0;
                var etaStr = FormatEta(etaSeconds);
                var etaPart = string.IsNullOrEmpty(etaStr) ? "" : $"  ETA {etaStr}";
                statusText = $"{directionText} {FormatTransferSize(state.DoneBytes)} / {FormatTransferSize(state.TotalBytes)} ({percentText}){etaPart}  —  {FormatTransferSpeed(speed)}";
                detailText = FormatTransferSpeed(speed);
                brush = _transferPulseOn ? TransferActiveBrushA : TransferActiveBrushB;
            }

            return new TransferUiSnapshot(
                statusText,
                detailText,
                percentText,
                pct,
                brush,
                statusBrush,
                speed,
                isStalled,
                stalledSeconds,
                state.IsCompleted,
                state.IsTerminal);
        }
    }

    private void ApplyTransferUiSnapshot(TransferUiSnapshot snapshot)
    {
        StopStatusBlink();

        if (_progressBar != null)
        {
            _progressBar.Value = snapshot.ProgressPercent;
            _progressBar.Foreground = snapshot.BarBrush;
        }

        if (_txtProgressPercent != null)
            _txtProgressPercent.Text = snapshot.PercentText;

        if (_txtSpeed != null)
            _txtSpeed.Text = snapshot.DetailText;

        if (_txtStatus != null)
        {
            _txtStatus.Text = snapshot.StatusText;
            if (snapshot.StatusBrush is null) _txtStatus.ClearValue(TextBlock.ForegroundProperty);
            else _txtStatus.Foreground = snapshot.StatusBrush;
        }

        // P3: solo actualizar sparkline si hay velocidad real (o cero al detenerse)
        UpdateSparkline(snapshot.SpeedBytesPerSecond);
        _progressWin?.SetProgress(snapshot.ProgressPercent, snapshot.DetailText);

        // Tray tooltip: show progress during transfer, reset on completion
        if (_tray != null)
        {
            _tray.ToolTipText = snapshot.IsCompleted || snapshot.IsTerminal
                ? "LanCopy"
                : $"LanCopy \u2014 {snapshot.PercentText}";
        }
    }

    private void TryAutoRecoverFromStall(TransferUiSnapshot snapshot)
    {
        if (!snapshot.IsStalled || snapshot.StalledSeconds < 25) return;

        var now = DateTimeOffset.UtcNow;
        if (now - _lastStallRecoverAt < TimeSpan.FromSeconds(20)) return;
        if (Interlocked.CompareExchange(ref _stallRecoverInProgress, 1, 0) != 0) return;

        _lastStallRecoverAt = now;
        _ = Task.Run(async () =>
        {
            try
            {
                SetStatus(L["st.autoReconnecting"]);
                // U5: cancelar transferencias activas ANTES de disponer el cliente
                // Sin esto, el transfer loop sigue usando el cliente dispuesto y lanza excepciones confusas
                try { _uploadCts?.Cancel(); }
                catch (ObjectDisposedException ex) { Log.Debug("transfer", "upload-cts-disposed-during-stall-recovery", new { error = ex.Message }); }
                catch (Exception ex) { Log.Warn("transfer", "upload-cts-cancel-failed-during-stall-recovery", new { error = ex.Message }); }
                try { _downloadCts?.Cancel(); }
                catch (ObjectDisposedException ex) { Log.Debug("transfer", "download-cts-disposed-during-stall-recovery", new { error = ex.Message }); }
                catch (Exception ex) { Log.Warn("transfer", "download-cts-cancel-failed-during-stall-recovery", new { error = ex.Message }); }
                await _clientLock.WaitAsync();
                try { _client?.Dispose(); _client = null; }
                finally { _clientLock.Release(); }

                await _clientLockDown.WaitAsync();
                try { _clientDown?.Dispose(); _clientDown = null; }
                finally { _clientLockDown.Release(); }
            }
            catch (Exception ex)
            {
                Log.Warn("transfer", "stall-recover-failed", new { error = ex.Message });
            }
            finally
            {
                Interlocked.Exchange(ref _stallRecoverInProgress, 0);
            }
        });
    }

    private static void TrimSpeedSamples(TransferUiState state, DateTimeOffset now)
    {
        var cutoff = now.AddSeconds(-2);
        while (state.SpeedSamples.Count > 0 && state.SpeedSamples.Peek().Timestamp < cutoff)
            state.SpeedSamples.Dequeue();
    }

    private static string FormatTransferSize(long bytes) => FileEntry.FormatSize(Math.Max(0, bytes));

    private static string FormatTransferSpeed(double bytesPerSecond) =>
        bytesPerSecond <= 0 ? "0 B/s" : $"{FileEntry.FormatSize((long)Math.Round(bytesPerSecond))}/s";

    private static string FormatPercent(double pct) => pct.ToString("0.#");

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s";
        if (elapsed.TotalMinutes >= 1)
            return $"{elapsed.Minutes}m {elapsed.Seconds}s";
        return $"{Math.Max(0, elapsed.Seconds)}s";
    }


    // U7: formateador de ETA
    private static string FormatEta(double seconds)
    {
        if (seconds <= 0 || double.IsInfinity(seconds) || double.IsNaN(seconds)) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{Math.Max(1, (int)ts.TotalSeconds)}s";
    }
    private sealed class TransferUiState
    {
        public TransferUiState(bool receiving, long totalBytes, DateTimeOffset now)
        {
            Receiving = receiving;
            TotalBytes = totalBytes;
            StartedAt = now;
            LastUpdateAt = now;
            LastByteAt = now;
        }

        public bool Receiving { get; }
        public long DoneBytes { get; set; }
        public long TotalBytes { get; set; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset LastUpdateAt { get; set; }
        public DateTimeOffset LastByteAt { get; set; }
        public TimeSpan CompletedElapsed { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsTerminal { get; set; }
        public Queue<TransferDeltaSample> SpeedSamples { get; } = new();
    }

    private readonly record struct TransferDeltaSample(DateTimeOffset Timestamp, long Bytes);
    private readonly record struct TransferUiSnapshot(
        string StatusText,
        string DetailText,
        string PercentText,
        double ProgressPercent,
        IBrush BarBrush,
        IBrush? StatusBrush,
        double SpeedBytesPerSecond,
        bool IsStalled,
        int StalledSeconds,
        bool IsCompleted,
        bool IsTerminal);
}
