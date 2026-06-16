using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LanCopy.Models;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class JsonStoreTests
{
    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "LanCopyJS_" + Guid.NewGuid().ToString("N") + ".json");

    public sealed class Sample { public string Name { get; set; } = ""; public int Value { get; set; } }

    [Fact]
    public void WriteThenRead_RoundTrips()
    {
        var p = TempFile();
        try
        {
            Assert.True(JsonStore.WriteAtomic(p, new Sample { Name = "hola", Value = 42 }));
            var back = JsonStore.Read<Sample>(p);
            Assert.NotNull(back);
            Assert.Equal("hola", back!.Name);
            Assert.Equal(42, back.Value);
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Read_MissingFile_ReturnsFallback()
    {
        var fallback = new Sample { Name = "def" };
        var r = JsonStore.Read(TempFile(), fallback);
        Assert.Same(fallback, r);
    }

    [Fact]
    public void Read_CorruptJson_ReturnsFallback()
    {
        var p = TempFile();
        try
        {
            File.WriteAllText(p, "{ this is not valid json ]");
            var r = JsonStore.Read<Sample>(p, new Sample { Name = "fb" });
            Assert.Equal("fb", r!.Name);
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void WriteAtomic_LeavesNoTempFile()
    {
        var p = TempFile();
        try
        {
            JsonStore.WriteAtomic(p, new Sample { Name = "x" });
            Assert.False(File.Exists(p + ".tmp"));
            Assert.True(File.Exists(p));
        }
        finally { File.Delete(p); }
    }
}

public class HistoryStoreTests
{
    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "LanCopyHS_" + Guid.NewGuid().ToString("N") + ".json");

    private static TransferRecord R(string text)
        => new() { Time = "00:00:00", Text = text, Color = "#28A745" };

    [Fact]
    public void Save_Load_RoundTrips()
    {
        var p = TempFile();
        try
        {
            var records = new List<TransferRecord> { R("a"), R("b"), R("c") };
            Assert.True(HistoryStore.Save(p, records));
            var back = HistoryStore.Load(p);
            Assert.Equal(3, back.Count);
            Assert.Equal("a", back[0].Text);
            Assert.Equal("c", back[2].Text);
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Save_CapsToMaxEntries()
    {
        var p = TempFile();
        try
        {
            var records = Enumerable.Range(0, 120).Select(i => R("item" + i)).ToList();
            HistoryStore.Save(p, records);
            var back = HistoryStore.Load(p);
            Assert.Equal(HistoryStore.MaxEntries, back.Count);
            Assert.Equal("item0", back[0].Text); // conserva los primeros (más recientes)
        }
        finally { File.Delete(p); }
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
        => Assert.Empty(HistoryStore.Load(TempFile()));
}