using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using LanCopy.Localization;

namespace LanCopy;

public class SplashScreen : Window
{
    private readonly ProgressBar _bar;
    private readonly TextBlock _status;

    public SplashScreen()
    {
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        Width  = 480;
        Height = 280;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent };

        var bg = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = new LinearGradientBrush
            {
                StartPoint = RelativePoint.TopLeft,
                EndPoint   = RelativePoint.BottomRight,
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(13, 17, 27),  0.0),
                    new GradientStop(Color.FromRgb(22, 30, 48),  0.5),
                    new GradientStop(Color.FromRgb(10, 20, 38),  1.0),
                }
            },
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 40, Color = Color.FromArgb(160,0,0,0), OffsetY = 8 })
        };

        var border = new Border
        {
            CornerRadius    = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(60, 100, 160, 255)),
            Padding         = new Thickness(40, 36),
        };

        var icon = new TextBlock
        {
            Text                = "\u27F7",
            FontSize            = 52,
            Foreground          = new LinearGradientBrush
            {
                StartPoint = RelativePoint.TopLeft,
                EndPoint   = RelativePoint.BottomRight,
                GradientStops = { new GradientStop(Color.FromRgb(0,180,255),0.0), new GradientStop(Color.FromRgb(80,100,255),1.0) }
            },
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0,0,0,8),
        };

        var title = new TextBlock
        {
            Text                = "LanCopy",
            FontSize            = 30,
            FontWeight          = FontWeight.Bold,
            Foreground          = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            LetterSpacing       = 2,
        };

        var subtitle = new TextBlock
        {
            Text                = "Transferencia LAN  \u00B7  Cifrada  \u00B7  Sin nube",
            FontSize            = 11,
            Foreground          = new SolidColorBrush(Color.FromArgb(150, 180, 200, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0,4,0,24),
        };

        _bar = new ProgressBar
        {
            Minimum    = 0,
            Maximum    = 100,
            Value      = 0,
            Height     = 3,
            Background = new SolidColorBrush(Color.FromArgb(40,255,255,255)),
            Foreground = new LinearGradientBrush
            {
                StartPoint = RelativePoint.TopLeft,
                EndPoint   = new RelativePoint(1,0,RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromRgb(0,180,255),0.0), new GradientStop(Color.FromRgb(80,100,255),1.0) }
            },
            Margin = new Thickness(0,0,0,10),
        };

        _status = new TextBlock
        {
            Text                = LanCopy.Localization.Loc.Instance["splash.loading"], // U3: usa key ya existente
            FontSize            = 11,
            Foreground          = new SolidColorBrush(Color.FromArgb(100,180,200,255)),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var version = new TextBlock
        {
            Text                = $"v{typeof(SplashScreen).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}",
            FontSize            = 10,
            Foreground          = new SolidColorBrush(Color.FromArgb(60,150,170,220)),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0,14,0,0),
        };

        var stack = new StackPanel();
        stack.Children.Add(icon);
        stack.Children.Add(title);
        stack.Children.Add(subtitle);
        stack.Children.Add(_bar);
        stack.Children.Add(_status);
        stack.Children.Add(version);

        border.Child = stack;
        var grid = new Grid();
        grid.Children.Add(bg);
        grid.Children.Add(border);
        Content = grid;

        Opacity = 0;
        Opened += async (_, _) => await FadeInAsync();
    }

    public void SetProgress(double value, string text) =>
        Dispatcher.UIThread.Post(() => { _bar.Value = value; _status.Text = text; });

    private async Task FadeInAsync()
    {
        for (double i = 0; i <= 1.0; i += 0.07) { Opacity = i; await Task.Delay(16); }
        Opacity = 1;
    }

    public async Task FadeOutAsync()
    {
        for (double i = 1.0; i >= 0; i -= 0.07) { Opacity = i; await Task.Delay(16); }
        Opacity = 0;
    }

    // U1: mostrar error de arranque en la splash (puerto ocupado, etc.) antes de cerrar
    public void ShowError(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _bar.Value = 100;
            _bar.Foreground = new SolidColorBrush(Color.FromRgb(220, 50, 50));
            _status.Text = message;
            _status.Foreground = new SolidColorBrush(Color.FromRgb(255, 120, 120));
        });
    }
}

