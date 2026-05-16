using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfButton = System.Windows.Controls.Button;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace ScoreDown.Infrastructure;

public static class DarkDialogService
{
    public static MessageBoxResult ShowMessage(
        Window? owner,
        string message,
        string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None)
    {
        var dlg = new DarkMessageWindow(message, title, buttons, image);
        if (owner is not null)
        {
            dlg.Owner = owner;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        _ = dlg.ShowDialog();
        return dlg.Result;
    }

    public static string? PromptFolder(Window? owner, string title, string initialPath)
    {
        var dlg = new DarkPathWindow(
            title,
            "Carpeta:",
            initialPath,
            "Escribe o pega ruta de carpeta",
            "Aceptar");

        if (owner is not null)
        {
            dlg.Owner = owner;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (dlg.ShowDialog() != true) return null;
        var path = (dlg.PathValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path)) return null;
        return path;
    }

    public static string? PromptSaveFile(Window? owner, string title, string initialPath, string requiredExtension)
    {
        var dlg = new DarkPathWindow(
            title,
            "Archivo:",
            initialPath,
            "Escribe o pega ruta completa de archivo",
            "Guardar");

        if (owner is not null)
        {
            dlg.Owner = owner;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (dlg.ShowDialog() != true) return null;
        var path = (dlg.PathValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path)) return null;

        if (!string.IsNullOrWhiteSpace(requiredExtension) &&
            !path.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
        {
            path += requiredExtension;
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return path;
    }

    public static string? PromptOpenFile(Window? owner, string title, string initialPath, string requiredExtension)
    {
        var dlg = new DarkPathWindow(
            title,
            "Archivo:",
            initialPath,
            "Escribe o pega ruta completa de archivo",
            "Abrir");

        if (owner is not null)
        {
            dlg.Owner = owner;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (dlg.ShowDialog() != true) return null;
        var path = (dlg.PathValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path)) return null;

        if (!string.IsNullOrWhiteSpace(requiredExtension) &&
            !path.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage(owner, $"Debe ser archivo {requiredExtension}", "Formato no valido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        if (!File.Exists(path))
        {
            ShowMessage(owner, "El archivo no existe.", "No encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        return path;
    }

    public static string? PromptText(
        Window? owner,
        string title,
        string caption,
        string initialValue,
        string hint,
        string acceptText = "Aceptar")
    {
        var dlg = new DarkPathWindow(
            title,
            caption,
            initialValue,
            hint,
            acceptText);

        if (owner is not null)
        {
            dlg.Owner = owner;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (dlg.ShowDialog() != true) return null;
        return (dlg.PathValue ?? string.Empty).Trim();
    }

    private static WpfSolidColorBrush Hex(string hex)
        => new((WpfColor)WpfColorConverter.ConvertFromString(hex)!);

    private sealed class DarkMessageWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public DarkMessageWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            Title = title;
            Width = 520;
            MinWidth = 420;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            Background = Hex("#1E1E2E");
            Foreground = WpfBrushes.White;

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconText = new TextBlock
            {
                Text = GetIcon(image),
                FontSize = 24,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(iconText, 0);

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(messageText, 1);

            body.Children.Add(iconText);
            body.Children.Add(messageText);
            Grid.SetRow(body, 0);

            var buttonsPanel = new StackPanel
            {
                Orientation = WpfOrientation.Horizontal,
                HorizontalAlignment = WpfHorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            AddButtons(buttonsPanel, buttons);
            Grid.SetRow(buttonsPanel, 1);

            root.Children.Add(body);
            root.Children.Add(buttonsPanel);
            Content = root;

            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Result = MessageBoxResult.Cancel;
                    DialogResult = false;
                }
            };
        }

        private static string GetIcon(MessageBoxImage image) => image switch
        {
            MessageBoxImage.Error => "[X]",
            MessageBoxImage.Warning => "[!]",
            MessageBoxImage.Information => "[i]",
            MessageBoxImage.Question => "[?]",
            _ => ""
        };

        private void AddButtons(WpfPanel panel, MessageBoxButton buttons)
        {
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    panel.Children.Add(CreateButton("OK", MessageBoxResult.OK, true));
                    break;
                case MessageBoxButton.OKCancel:
                    panel.Children.Add(CreateButton("Cancelar", MessageBoxResult.Cancel));
                    panel.Children.Add(CreateButton("OK", MessageBoxResult.OK, true));
                    break;
                case MessageBoxButton.YesNo:
                    panel.Children.Add(CreateButton("No", MessageBoxResult.No));
                    panel.Children.Add(CreateButton("Si", MessageBoxResult.Yes, true));
                    break;
                case MessageBoxButton.YesNoCancel:
                    panel.Children.Add(CreateButton("Cancelar", MessageBoxResult.Cancel));
                    panel.Children.Add(CreateButton("No", MessageBoxResult.No));
                    panel.Children.Add(CreateButton("Si", MessageBoxResult.Yes, true));
                    break;
            }
        }

        private WpfButton CreateButton(string text, MessageBoxResult result, bool isDefault = false)
        {
            var button = new WpfButton
            {
                Content = text,
                MinWidth = 84,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Background = Hex("#2D2D44"),
                Foreground = WpfBrushes.White,
                BorderBrush = Hex("#555"),
                BorderThickness = new Thickness(1),
                IsDefault = isDefault
            };

            button.Click += (_, _) =>
            {
                Result = result;
                DialogResult = result is MessageBoxResult.OK or MessageBoxResult.Yes;
                Close();
            };

            return button;
        }
    }

    private sealed class DarkPathWindow : Window
    {
        private readonly WpfTextBox _pathText;
        public string PathValue => _pathText.Text;

        public DarkPathWindow(string title, string caption, string initialPath, string hint, string acceptText)
        {
            Title = title;
            Width = 740;
            MinWidth = 540;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            Background = Hex("#1E1E2E");
            Foreground = WpfBrushes.White;

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var captionText = new TextBlock
            {
                Text = caption,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = Hex("#B0B0C0")
            };
            Grid.SetRow(captionText, 0);

            _pathText = new WpfTextBox
            {
                Text = initialPath ?? string.Empty,
                Padding = new Thickness(10, 6, 10, 6),
                Background = Hex("#2D2D44"),
                Foreground = WpfBrushes.White,
                BorderBrush = Hex("#555"),
                BorderThickness = new Thickness(1),
                ToolTip = hint
            };
            Grid.SetRow(_pathText, 1);

            var buttons = new StackPanel
            {
                Orientation = WpfOrientation.Horizontal,
                HorizontalAlignment = WpfHorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var btnCancel = new WpfButton
            {
                Content = "Cancelar",
                MinWidth = 92,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Background = Hex("#374151"),
                Foreground = WpfBrushes.White,
                BorderBrush = Hex("#555"),
                BorderThickness = new Thickness(1)
            };
            btnCancel.Click += (_, _) =>
            {
                DialogResult = false;
                Close();
            };

            var btnAccept = new WpfButton
            {
                Content = acceptText,
                MinWidth = 92,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Background = Hex("#7C3AED"),
                Foreground = WpfBrushes.White,
                BorderBrush = Hex("#555"),
                BorderThickness = new Thickness(1),
                IsDefault = true
            };
            btnAccept.Click += (_, _) =>
            {
                DialogResult = true;
                Close();
            };

            buttons.Children.Add(btnCancel);
            buttons.Children.Add(btnAccept);
            Grid.SetRow(buttons, 2);

            root.Children.Add(captionText);
            root.Children.Add(_pathText);
            root.Children.Add(buttons);
            Content = root;

            Loaded += (_, _) =>
            {
                _pathText.Focus();
                _pathText.SelectAll();
            };

            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
            };
        }
    }
}
