using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScoreDown.Models;

public class PartituraItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private string _title = string.Empty;
    private string _composer = string.Empty;
    private string _pageUrl = string.Empty;
    private bool _isSelected = true;
    private string _source = "Mutopia";
    private string _userTag = string.Empty;
    private int _sourcePageId;
    private string _genre = string.Empty;
    private string _instrument = string.Empty;
    private string _license = string.Empty;

    public string Title { get => _title; set => Set(ref _title, value); }
    public string Composer { get => _composer; set => Set(ref _composer, value); }
    public string PageUrl { get => _pageUrl; set => Set(ref _pageUrl, value); }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public string Source { get => _source; set => Set(ref _source, value); }
    public string UserTag { get => _userTag; set => Set(ref _userTag, value); }
    public int SourcePageId { get => _sourcePageId; set => Set(ref _sourcePageId, value); }
    public string Genre { get => _genre; set => Set(ref _genre, value); }
    public string Instrument { get => _instrument; set => Set(ref _instrument, value); }
    public string License { get => _license; set => Set(ref _license, value); }

    public List<PartituraFile> Files { get; set; } = new();

    public string DisplayName =>
        $"[{Source}] " + (string.IsNullOrEmpty(Composer) ? Title : $"{Composer} — {Title}");
}

public class PartituraFile
{
    public string Format { get; set; } = string.Empty;   // PDF, MIDI, XML
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    /// <summary>URL de la página de la obra de la que proviene este archivo. Usada como Referer en descargas.</summary>
    public string? SourcePageUrl { get; set; }

    public string SizeDisplay => SizeBytes > 0
        ? SizeBytes >= 1024 * 1024
            ? $"{SizeBytes / (1024.0 * 1024):F1} MB"
            : $"{SizeBytes / 1024.0:F0} KB"
        : string.Empty;

    public string Label => string.IsNullOrEmpty(SizeDisplay) ? Format : $"{Format} ({SizeDisplay})";
}
