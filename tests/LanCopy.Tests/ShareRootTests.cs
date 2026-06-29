using System.IO;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class ShareRootTests
{
    private static string OutsideRootDir()
        => OperatingSystem.IsWindows() ? @"C:\Windows" : "/etc";

    private static string DotDotTraversalAttempt()
        => OperatingSystem.IsWindows() ? @"..\..\..\Windows" : "../../../etc";

    private static string NewTempRoot()
    {
        var p = Path.Combine(Path.GetTempPath(), "LanCopyTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    [Fact]
    public void EmptyPath_ResolvesToRoot()
    {
        var root = NewTempRoot();
        ShareRoot.SetRoot(root);
        Assert.True(ShareRoot.TryResolve("", out var full, out _));
        Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(full));
    }

    [Fact]
    public void RelativePath_StaysInsideRoot()
    {
        var root = NewTempRoot();
        ShareRoot.SetRoot(root);
        Assert.True(ShareRoot.TryResolve("sub/file.txt", out var full, out _));
        Assert.StartsWith(Path.GetFullPath(root), Path.GetFullPath(full));
    }

    [Fact]
    public void TraversalWithDotDot_IsBlocked()
    {
        var root = NewTempRoot();
        ShareRoot.SetRoot(root);
        Assert.False(ShareRoot.TryResolve(DotDotTraversalAttempt(), out _, out var reason));
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void AbsolutePathOutsideRoot_IsBlocked()
    {
        var root = NewTempRoot();
        ShareRoot.SetRoot(root);
        Assert.False(ShareRoot.TryResolve(OutsideRootDir(), out _, out _));
    }

    [Fact]
    public void AbsolutePathInsideRoot_IsAllowed()
    {
        var root = NewTempRoot();
        ShareRoot.SetRoot(root);
        var inside = Path.Combine(root, "ok.txt");
        Assert.True(ShareRoot.TryResolve(inside, out var full, out _));
        Assert.Equal(Path.GetFullPath(inside), Path.GetFullPath(full));
    }
}