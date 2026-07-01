using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LanCopy;

public partial class QueueResumeDialog : Window
{
    public enum QueueResumeAction
    {
        Discard,
        Resume,
        ResumeSkipSameSize
    }

    public QueueResumeAction Result { get; private set; } = QueueResumeAction.Discard;

    public QueueResumeDialog(string message, string resumeLabel, string resumeSkipSameSizeLabel, string discardLabel)
    {
        InitializeComponent();
        var msg = this.FindControl<TextBlock>("txtMessage");
        if (msg != null) msg.Text = message;
        var btnR = this.FindControl<Button>("btnResume");
        if (btnR != null) btnR.Content = resumeLabel;
        var btnRSS = this.FindControl<Button>("btnResumeSkipSameSize");
        if (btnRSS != null) btnRSS.Content = resumeSkipSameSizeLabel;
        var btnD = this.FindControl<Button>("btnDiscard");
        if (btnD != null) btnD.Content = discardLabel;
    }

    private void BtnResume_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Result = QueueResumeAction.Resume;
        Close();
    }

    private void BtnResumeSkipSameSize_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Result = QueueResumeAction.ResumeSkipSameSize;
        Close();
    }

    private void BtnDiscard_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Result = QueueResumeAction.Discard;
        Close();
    }
}
