using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LanCopy.Localization;
using LanCopy.Models;

namespace LanCopy.Dialogs;

internal sealed class ChatWindow : Window
{
    private readonly Func<string, Task<bool>> _sendAsync;
    private readonly ObservableCollection<ChatMessage> _messages;
    private readonly ListBox _list;
    private readonly TextBox _input;
    private readonly Button _sendButton;

    public ChatWindow(ObservableCollection<ChatMessage> messages, Func<string, Task<bool>> sendAsync)
    {
        _messages = messages;
        _sendAsync = sendAsync;

        Title = Loc.Instance["chat.title"];
        Width = 410;
        Height = 620;
        MinWidth = 340;
        MinHeight = 460;
        Background = SolidColorBrush.Parse("#111B21");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Background = SolidColorBrush.Parse("#111B21")
        };

        var header = new Border
        {
            Background = SolidColorBrush.Parse("#075E54"),
            Padding = new Thickness(14, 12),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = Loc.Instance["chat.title"], Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = Loc.Instance["chat.subtitle"], Foreground = SolidColorBrush.Parse("#D7F7EF"), FontSize = 12 }
                }
            }
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _list = new ListBox
        {
            ItemsSource = _messages,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 12),
            ItemTemplate = new FuncDataTemplate<ChatMessage>((message, _) => BuildBubble(message), true)
        };
        Grid.SetRow(_list, 1);
        root.Children.Add(_list);

        var inputBar = new Border { Background = SolidColorBrush.Parse("#202C33"), Padding = new Thickness(10, 8) };
        var inputGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,44"), ColumnSpacing = 8 };
        _input = new TextBox
        {
            Background = SolidColorBrush.Parse("#2A3942"),
            Foreground = Brushes.White,
            BorderBrush = Brushes.Transparent,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14, 8),
            PlaceholderText = Loc.Instance["chat.placeholder"],
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 42,
            MaxHeight = 120,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_input, 0);
        inputGrid.Children.Add(_input);

        _sendButton = new Button
        {
            Content = "➤",
            Background = SolidColorBrush.Parse("#25D366"),
            Foreground = SolidColorBrush.Parse("#071B12"),
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(21),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 18,
            FontWeight = FontWeight.Bold
        };
        ToolTip.SetTip(_sendButton, Loc.Instance["tip.sendtext"]);
        _sendButton.Click += async (_, _) => await SendCurrentAsync();
        Grid.SetColumn(_sendButton, 1);
        inputGrid.Children.Add(_sendButton);
        inputBar.Child = inputGrid;
        Grid.SetRow(inputBar, 2);
        root.Children.Add(inputBar);

        Content = root;
        _messages.CollectionChanged += (_, _) => ScrollToBottom();
        Opened += (_, _) => { _input.Focus(); ScrollToBottom(); };
    }

    private static Control BuildBubble(ChatMessage message)
    {
        var outer = new StackPanel
        {
            HorizontalAlignment = message.IsOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(0, 3),
            MaxWidth = 330
        };

        var bubble = new Border
        {
            Background = message.IsOwn ? SolidColorBrush.Parse("#005C4B") : SolidColorBrush.Parse("#202C33"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10, 7),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock { Text = message.Sender, Foreground = message.IsOwn ? SolidColorBrush.Parse("#B9FBCB") : SolidColorBrush.Parse("#7FD8FF"), FontSize = 11, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = message.Text, Foreground = Brushes.White, FontSize = 14, TextWrapping = TextWrapping.Wrap, MaxWidth = 285 },
                    new TextBlock { Text = message.Timestamp.ToLocalTime().ToString("HH:mm"), Foreground = SolidColorBrush.Parse("#B8C1C6"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right }
                }
            }
        };

        outer.Children.Add(bubble);
        return outer;
    }

    private async Task SendCurrentAsync()
    {
        var text = _input.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text)) return;

        _sendButton.IsEnabled = false;
        try
        {
            if (await _sendAsync(text))
                _input.Text = "";
        }
        finally
        {
            _sendButton.IsEnabled = true;
            _input.Focus();
        }
    }

    private void ScrollToBottom()
    {
        if (_messages.Count == 0) return;
        Dispatcher.UIThread.Post(() => _list.ScrollIntoView(_messages[^1]), DispatcherPriority.Background);
    }
}



