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