using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("2.0.0", "1.9.9", 1)]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("1.0.0.1", "1.0.0.0", 1)]
    public void CompareVersions_Works(string a, string b, int expectedSign)
    {
        var r = UpdateChecker.CompareVersions(a, b);
        Assert.Equal(expectedSign, Math.Sign(r));
    }

    [Fact]
    public void CurrentVersion_IsParseable()
    {
        var v = UpdateChecker.CurrentVersion;
        Assert.False(string.IsNullOrWhiteSpace(v));
        Assert.Equal(0, UpdateChecker.CompareVersions(v, v));
    }
}