using System;
using System.IO;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class SystemProtectionTests
{
    private static string Profile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static string Docs => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static string Windows => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    [Fact]
    public void DriveRoot_IsProtected()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory)!;
        Assert.True(SystemProtection.IsProtected(root));
    }

    [Fact]
    public void WindowsFolder_IsProtected_Base()
    {
        Assert.True(SystemProtection.IsProtected(Windows));
        Assert.True(SystemProtection.IsProtected(Path.Combine(Windows, "System32")));
    }
    [Fact]
    public void DriveRootWithoutSlash_IsProtected()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.True(SystemProtection.IsProtected("C:"));
        Assert.False(SafeFileOps.TryValidateMutationPath("C:", out _, out var reason));
        Assert.Equal("svc.driveRootProtected", reason);
    }

    [Fact]
    public void ProgramFiles_IsProtected_Base()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(pf)) return;

        Assert.True(SystemProtection.IsProtected(pf));
        Assert.True(SystemProtection.IsProtected(Path.Combine(pf, "LanCopyProbe")));
        Assert.False(SafeFileOps.TryValidateMutationPath(pf, out _, out var reason));
        Assert.Equal("svc.sysProtected", reason);
    }

    [Fact]
    public void RealUserFile_NotBlocked_AtBaseLevel()
    {
        // Un archivo real del usuario (no de sistema) NO debe bloquearse en la
        // barrera base: el propio usuario gestiona sus archivos localmente.
        // Ubicacion neutral (no bajo carpetas de sistema): dir del binario de test.
        var tmp = Path.Combine(AppContext.BaseDirectory, "LanCopySP_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(tmp, "x");
        try { Assert.False(SystemProtection.IsProtected(tmp)); }
        finally { File.Delete(tmp); }
    }
    [Fact]
    public void KnownSensitiveRootFolders_AreProtected()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.GetPathRoot(Environment.SystemDirectory)!;
        var sensitive = new[]
        {
            "Windows",
            "Program Files",
            "Program Files (x86)",
            "ProgramData",
            "System Volume Information",
            "$Recycle.Bin",
            "Recovery",
            "Boot",
            "EFI",
            "Config.Msi",
            "MSOCache",
            "PerfLogs",
            "$WinREAgent",
            "$WINDOWS.~BT",
            "$Windows.~WS",
            "Documents and Settings"
        };

        foreach (var name in sensitive)
            Assert.True(SystemProtection.IsProtected(Path.Combine(root, name, "probe.txt")), name);
    }

    [Fact]
    public void KnownSensitiveRootFiles_AreProtected()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.GetPathRoot(Environment.SystemDirectory)!;
        var sensitive = new[] { "pagefile.sys", "swapfile.sys", "hiberfil.sys", "bootmgr", "BOOTNXT", "DumpStack.log.tmp" };

        foreach (var name in sensitive)
            Assert.True(SystemProtection.IsProtected(Path.Combine(root, name)), name);
    }

    [Fact]
    public void PersonalDocs_Blocked_ForRemote()
    {
        // Un par remoto en modo disco completo NO puede tocar el árbol personal,
        // aunque la ruta concreta no exista todavía (prefijo de raíz personal).
        var file = Path.Combine(Docs, "mi_archivo.txt");
        Assert.True(SystemProtection.IsProtectedForRemote(file));
    }

    [Fact]
    public void Profile_Blocked_ForRemote()
        => Assert.True(SystemProtection.IsProtectedForRemote(Path.Combine(Profile, "secreto.txt")));

    [Fact]
    public void Downloads_Blocked_ForRemote()
        => Assert.True(SystemProtection.IsProtectedForRemote(Path.Combine(Profile, "Downloads", "x.zip")));

    [Fact]
    public void SystemFolder_AlsoBlocked_ForRemote()
        => Assert.True(SystemProtection.IsProtectedForRemote(Path.Combine(Windows, "System32", "kernel32.dll")));

}

