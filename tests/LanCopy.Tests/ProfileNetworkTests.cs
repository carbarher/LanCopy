using System.Collections.Generic;
using System.Linq;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class ProfileStoreTests
{
    private static List<ConnectionProfile> Sample() => new()
    {
        new ConnectionProfile("home", "192.168.1.5", "8742", "1234", true, false),
        new ConnectionProfile("work", "10.0.0.9", "9000", "", false, true),
    };

    [Fact]
    public void Find_ReturnsMatch_OrNull()
    {
        var list = Sample();
        Assert.Equal("10.0.0.9", ProfileStore.Find(list, "work")!.Ip);
        Assert.Null(ProfileStore.Find(list, "missing"));
    }

    [Fact]
    public void Upsert_AddsNewProfile()
    {
        var list = Sample();
        ProfileStore.Upsert(list, new ConnectionProfile("lab", "172.16.0.1", "8742", "", true, true));
        Assert.Equal(3, list.Count);
        Assert.Equal("172.16.0.1", ProfileStore.Find(list, "lab")!.Ip);
    }

    [Fact]
    public void Upsert_ReplacesExisting_NoDuplicates()
    {
        var list = Sample();
        ProfileStore.Upsert(list, new ConnectionProfile("home", "1.1.1.1", "1234", "pin", false, true));
        Assert.Equal(2, list.Count);
        var home = ProfileStore.Find(list, "home")!;
        Assert.Equal("1.1.1.1", home.Ip);
        Assert.Equal("1234", home.Port);
    }

    [Fact]
    public void Remove_DeletesByName_ReturnsResult()
    {
        var list = Sample();
        Assert.True(ProfileStore.Remove(list, "home"));
        Assert.Single(list);
        Assert.False(ProfileStore.Remove(list, "home"));
    }

    [Fact]
    public void Names_PreservesOrder()
    {
        var names = ProfileStore.Names(Sample());
        Assert.Equal(new[] { "home", "work" }, names.ToArray());
    }
}

public class NetworkValidationTests
{
    [Theory]
    [InlineData("8742", true, 8742)]
    [InlineData("1", true, 1)]
    [InlineData("65535", true, 65535)]
    [InlineData("  9000  ", true, 9000)]
    [InlineData("0", false, 0)]
    [InlineData("65536", false, 0)]
    [InlineData("-5", false, 0)]
    [InlineData("abc", false, 0)]
    [InlineData("", false, 0)]
    [InlineData(null, false, 0)]
    public void TryParsePort_Validates(string? input, bool expected, int expectedPort)
    {
        var ok = NetworkValidation.TryParsePort(input, out var port);
        Assert.Equal(expected, ok);
        Assert.Equal(expectedPort, port);
    }

    [Fact]
    public void ParsePortOrDefault_FallsBack()
    {
        Assert.Equal(8742, NetworkValidation.ParsePortOrDefault("nope"));
        Assert.Equal(1234, NetworkValidation.ParsePortOrDefault("bad", 1234));
        Assert.Equal(9000, NetworkValidation.ParsePortOrDefault("9000"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(65535, true)]
    [InlineData(65536, false)]
    public void IsValidPort_Range(int port, bool expected)
        => Assert.Equal(expected, NetworkValidation.IsValidPort(port));
}