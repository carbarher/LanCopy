using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using LanCopy.Services.UI;

namespace LanCopy;

public partial class MainWindow
{
    private void InitializeTransferUi() => _transferUiService.Initialize();

    private void BeginTransferUi(bool receiving, long totalBytes) =>
        _transferUiService.BeginTransferUi(receiving, totalBytes);

    private void ReportTransferUi(bool receiving, long doneBytes, long totalBytes) =>
        _transferUiService.ReportTransferUi(receiving, doneBytes, totalBytes);

    private void CompleteTransferUi(bool receiving, long doneBytes, long totalBytes, TimeSpan elapsed) =>
        _transferUiService.CompleteTransferUi(receiving, doneBytes, totalBytes, elapsed);

    private void ResetTransferUi() => _transferUiService.ResetTransferUi();

    private void SuspendTransferUi() => _transferUiService.SuspendTransferUi();

    private void SetTransferCurrentItem(string text) => _transferUiService.SetCurrentItem(text);

    void ITransferUiHost.SetTransferCurrentItem(string text) => _progressWin?.SetLine(text);
    void ITransferUiHost.StopStatusBlink() => StopStatusBlink();

    void ITransferUiHost.SetTransferProgress(double progressPercent, IBrush brush)
    {
        if (_progressBar != null)
        {
            _progressBar.Value = progressPercent;
            _progressBar.Foreground = brush;
        }
    }

    void ITransferUiHost.SetTransferPercentText(string text)
    {
        if (_txtProgressPercent != null)
            _txtProgressPercent.Text = text;
    }

    void ITransferUiHost.SetTransferSpeedText(string text)
    {
        if (_txtSpeed != null)
            _txtSpeed.Text = text;
    }

    void ITransferUiHost.SetTransferStatusText(string text, IBrush? brush)
    {
        if (_txtStatus == null) return;
        _txtStatus.Text = text;
        if (brush is null) _txtStatus.ClearValue(TextBlock.ForegroundProperty);
        else _txtStatus.Foreground = brush;
    }

    void ITransferUiHost.UpdateSparkline(double bytesPerSecond) => UpdateSparkline(bytesPerSecond);

    void ITransferUiHost.SetTransferProgressWindow(double progressPercent, string detailText) =>
        _progressWin?.SetProgress(progressPercent, detailText);

    void ITransferUiHost.SetTransferTrayTooltip(string text)
    {
        if (_tray != null)
            _tray.ToolTipText = text;
    }

    Task ITransferUiHost.RecoverFromTransferStallAsync() =>
        _connectionUiService.RecoverFromTransferStallAsync();
}
