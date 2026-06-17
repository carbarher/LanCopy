using System.IO;
using System.Linq;
using LanCopy.Models;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class FileSorterTests
{
    private static FileEntry F(string name, bool dir = false, long size = 0, long ticks = 0)
        => new() { Name = name, IsDirectory = dir, Size = size, LastWriteUtcTicks = ticks };

    [Fact]
    public void DotDot_AlwaysFirst()
    {
        var items = new[] { F("zeta"), F(".."), F("alpha") };
        var sorted = FileSorter.Sort(items, "name", asc: true).ToList();
        Assert.Equal("..", sorted[0].Name);
    }

    [Fact]
    public void Directories_BeforeFiles()
    {
        var items = new[] { F("afile"), F("zdir", dir: true) };
        var sorted = FileSorter.Sort(items, "name", asc: true).ToList();
        Assert.True(sorted[0].IsDirectory);
        Assert.False(sorted[1].IsDirectory);
    }

    [Fact]
    public void Name_Ascending_IsCaseInsensitive()
    {
        var items = new[] { F("Banana"), F("apple"), F("Cherry") };
        var sorted = FileSorter.Sort(items, "name", asc: true).Select(f => f.Name).ToList();
        Assert.Equal(new[] { "apple", "Banana", "Cherry" }, sorted);
    }

    [Fact]
    public void Size_Descending_Works()
    {
        var items = new[] { F("a", size: 10), F("b", size: 100), F("c", size: 50) };
        var sorted = FileSorter.Sort(items, "size", asc: false).Select(f => f.Size).ToList();
        Assert.Equal(new long[] { 100, 50, 10 }, sorted);
    }

    [Fact]
    public void Date_Ascending_Works()
    {
        var items = new[] { F("a", ticks: 300), F("b", ticks: 100), F("c", ticks: 200) };
        var sorted = FileSorter.Sort(items, "date", asc: true).Select(f => f.LastWriteUtcTicks).ToList();
        Assert.Equal(new long[] { 100, 200, 300 }, sorted);
    }
}

public class PathSafetyTests
{
    [Fact]
    public void Combine_NormalSegment_StaysUnder()
    {
        var ok = PathSafety.TryCombineUnder(Path.GetTempPath(), out var dest, "sub", "file.txt");
        Assert.True(ok);
        Assert.EndsWith("file.txt", dest);
    }

    [Fact]
    public void Combine_Traversal_IsRejected()
    {
        var ok = PathSafety.TryCombineUnder(Path.GetTempPath(), out _, "..", "escape.txt");
        Assert.False(ok);
    }

    [Fact]
    public void Combine_NestedTraversal_IsRejected()
    {
        var ok = PathSafety.TryCombineUnder(Path.GetTempPath(), out _, "sub/../../escape.txt");
        Assert.False(ok);
    }

    [Fact]
    public void Combine_AbsoluteSegment_IsRejected()
    {
        var abs = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "Windows", "System32");
        var ok = PathSafety.TryCombineUnder(Path.GetTempPath(), out _, abs);
        Assert.False(ok);
    }

    [Fact]
    public void MakeUnique_NonExisting_ReturnsSame()
    {
        var p = Path.Combine(Path.GetTempPath(), "LanCopyUnique_" + System.Guid.NewGuid().ToString("N") + ".bin");
        Assert.Equal(p, PathSafety.MakeUnique(p));
    }

    [Fact]
    public void MakeUnique_Existing_AppendsCounter()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LanCopyUnique_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var p = Path.Combine(dir, "doc.txt");
            File.WriteAllText(p, "x");
            var unique = PathSafety.MakeUnique(p);
            Assert.NotEqual(p, unique);
            Assert.Equal(Path.Combine(dir, "doc (2).txt"), unique);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

public class SpeedSparklineTests
{
    [Fact]
    public void Empty_ReturnsEmpty()
        => Assert.Equal("", SpeedSparkline.Render(System.Array.Empty<double>()));

    [Fact]
    public void AllZero_ReturnsEmpty()
        => Assert.Equal("", SpeedSparkline.Render(new[] { 0.0, 0.0 }));

    [Fact]
    public void Max_MapsToHighestBar()
    {
        var s = SpeedSparkline.Render(new[] { 0.0, 10.0 });
        Assert.Equal(2, s.Length);
        Assert.Equal('\u2588', s[1]);
    }
}