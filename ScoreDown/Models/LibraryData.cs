namespace ScoreDown.Models;

public sealed class LibraryData
{
    public Dictionary<string, string> Tags { get; set; } = [];
    public List<PartituraItem> Items { get; set; } = [];
}
