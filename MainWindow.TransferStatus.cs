using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using LanCopy.Models;

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
            _transferPulseOn = !_transferPulseOn;
            var snapshot = CreateTransferUiSnapshot();
            if (snapshot is TransferUiSnapshot current) ApplyTransferUiSnapshot(current);
            if (snapshot is null || snapshot.Value.IsCompleted || snapshot.Value.IsTerminal)
                _transferUiTimer?.Stop();
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
        });
    }

    private void SuspendTransferUi()
    {
        lock (_transferUiLock)
        {
            _transferUiState = null;
        }

        StopTransferUiTimer();
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
            var elapsedSeconds = Math.Max(0.5, Math.Min(2.0, (now - state.StartedAt).TotalSeconds));
            var recentBytes = state.SpeedSamples.Sum(x => x.Bytes);
            var speed = recentBytes > 0 ? recentBytes / elapsedSeconds : 0;
            var stalledSeconds = Math.Max(0, (int)Math.Floor((now - state.LastByteAt).TotalSeconds));
            var isStalled = !state.IsCompleted && !state.IsTerminal && state.DoneBytes < state.TotalBytes && stalledSeconds >= 10;
            var directionText = state.Receiving ? "Recibiendo…" : "Enviando…";
            var percentText = $"{FormatPercent(pct)}%";

            var statusText = directionText;
            var detailText = "";
            var brush = TransferCompletedBrush;
            IBrush? statusBrush = null;

            if (state.IsCompleted)
            {
                statusText = $"Completado  {FormatTransferSize(state.TotalBytes)} en {FormatElapsed(state.CompletedElapsed)}";
                detailText = FormatElapsed(state.CompletedElapsed);
                pct = 100;
                brush = TransferCompletedBrush;
            }
            else if (state.IsTerminal)
            {
                statusText = $"Completado parcialmente  {FormatTransferSize(state.DoneBytes)} / {FormatTransferSize(state.TotalBytes)}";
                detailText = percentText;
                brush = TransferStalledBrush;
                statusBrush = TransferStalledTextBrush;
            }
            else if (isStalled)
            {
                statusText = $"Estancado – sin datos en {stalledSeconds}s";
                detailText = $"sin datos {stalledSeconds}s";
                brush = TransferStalledBrush;
                statusBrush = TransferStalledTextBrush;
            }
            else
            {
                statusText = $"{directionText} {FormatTransferSize(state.DoneBytes)} / {FormatTransferSize(state.TotalBytes)} ({percentText})  –  {FormatTransferSpeed(speed)}";
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

        UpdateSparkline(snapshot.SpeedBytesPerSecond);
        _progressWin?.SetProgress(snapshot.ProgressPercent, snapshot.DetailText);
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
        bool IsCompleted,
        bool IsTerminal);
}
