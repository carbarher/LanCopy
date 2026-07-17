using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace LanCopy.Services;

/// <summary>
/// Determina si una ruta del sistema operativo está protegida contra borrado/renombrado.
/// Dos niveles:
///  - IsProtected: barrera base (carpetas de sistema). Aplica a operaciones locales
///    y remotas. NO incluye las carpetas personales para no impedir que el propio
///    usuario gestione sus archivos en su equipo.
///  - IsProtectedForRemote: barrera reforzada para peticiones de un par remoto en
///    modo "disco completo". Añade el árbol personal del usuario (Documentos, Fotos,
///    Escritorio, Descargas, perfil...), evitando que alguien remoto borre datos
///    personales aunque se haya desactivado el confinamiento a la carpeta compartida.
/// </summary>
internal static class SystemProtection
{
    private static readonly HashSet<string> _systemRoots;
    private static readonly HashSet<string> _personalRoots;

    static SystemProtection()
    {
        _systemRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _personalRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddSys(Environment.SpecialFolder f)
        {
            var p = Environment.GetFolderPath(f);
            if (!string.IsNullOrEmpty(p)) _systemRoots.Add(NormalizeRoot(p));
        }
        void AddSysPath(string? p)
        {
            if (!string.IsNullOrWhiteSpace(p)) _systemRoots.Add(NormalizeRoot(p));
        }

        void AddKnownSystemNamesForDrive(string? driveRoot)
        {
            if (string.IsNullOrWhiteSpace(driveRoot)) return;
            string root;
            try
            {
                root = Path.GetPathRoot(Path.GetFullPath(driveRoot)) ?? driveRoot;
            }
            catch
            {
                root = driveRoot;
            }

            if (string.IsNullOrWhiteSpace(root)) return;

            string[] names =
            [
                "Windows",
                "WinNT",
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
                "Documents and Settings",
                "pagefile.sys",
                "swapfile.sys",
                "hiberfil.sys",
                "bootmgr",
                "BOOTNXT",
                "DumpStack.log.tmp"
            ];

            foreach (var name in names)
                AddSysPath(Path.Combine(root, name));
        }

        void AddUnixSystemRoots()
        {
            string[] roots =
            [
                "/bin",
                "/sbin",
                "/usr",
                "/etc",
                "/var",
                "/private",
                "/opt",
                "/dev",
                "/proc",
                "/sys",
                "/run",
                "/boot",
                "/root",
                "/snap",
                "/nix",
                "/System",
                "/Library",
                "/Applications",
                "/Network",
                "/Volumes",
                "/cores"
            ];

            foreach (var root in roots)
                AddSysPath(root);
        }

        void AddPersonal(Environment.SpecialFolder f)
        {
            var p = Environment.GetFolderPath(f);
            if (!string.IsNullOrEmpty(p)) _personalRoots.Add(p);
        }

        AddSys(Environment.SpecialFolder.Windows);
        AddSys(Environment.SpecialFolder.System);
        AddSys(Environment.SpecialFolder.SystemX86);
        AddSys(Environment.SpecialFolder.ProgramFiles);
        AddSys(Environment.SpecialFolder.ProgramFilesX86);
        AddSys(Environment.SpecialFolder.CommonApplicationData); // C:\ProgramData
        AddSysPath(Environment.GetEnvironmentVariable("ProgramFiles"));
        AddSysPath(Environment.GetEnvironmentVariable("ProgramW6432"));
        AddSysPath(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
        AddSysPath(Environment.GetEnvironmentVariable("SystemRoot"));
        AddSysPath(Environment.GetEnvironmentVariable("WinDir"));
        AddSysPath(Environment.GetEnvironmentVariable("ProgramData"));
        AddUnixSystemRoots();

        AddKnownSystemNamesForDrive(Environment.GetEnvironmentVariable("SystemDrive"));
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
                AddKnownSystemNamesForDrive(drive.Name);
        }
        catch
        {
            // If drive enumeration is unavailable, environment/special folders still protect the OS drive.
        }

        AddPersonal(Environment.SpecialFolder.UserProfile);   // C:\Users\<user>
        AddPersonal(Environment.SpecialFolder.MyDocuments);
        AddPersonal(Environment.SpecialFolder.MyPictures);
        AddPersonal(Environment.SpecialFolder.MyMusic);
        AddPersonal(Environment.SpecialFolder.MyVideos);
        AddPersonal(Environment.SpecialFolder.Desktop);
        AddPersonal(Environment.SpecialFolder.DesktopDirectory);
        AddPersonal(Environment.SpecialFolder.Favorites);
        AddPersonal(Environment.SpecialFolder.ApplicationData);      // AppData\Roaming
        AddPersonal(Environment.SpecialFolder.LocalApplicationData); // AppData\Local

        // "Descargas" no tiene SpecialFolder propio; se deriva del perfil.
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
            _personalRoots.Add(Path.Combine(profile, "Downloads"));
    }

    /// <summary>
    /// Barrera base (sistema). Devuelve true si la ruta NO debe modificarse.
    /// </summary>
    public static bool IsProtected(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        if (IsDriveRootLike(path)) return true;
        string normalized;
        try { normalized = Path.GetFullPath(path); }
        catch { return true; }

        // Raíces de unidad siempre protegidas (C:\, D:\, etc.)
        if (Path.GetPathRoot(normalized)?.Equals(normalized, StringComparison.OrdinalIgnoreCase) == true) return true;

        // Rutas de sistema conocidas (protege la raíz y todo su contenido)
        foreach (var root in _systemRoots)
            if (IsUnderOrEqual(normalized, root))
                return true;

        // Atributo de sistema en el archivo/carpeta
        try
        {
            var attr = File.GetAttributes(normalized);
            if (attr.HasFlag(FileAttributes.System)) return true;
        }
        catch { /* path no accesible → tratar como protegido */ return true; }

        return false;
    }

    /// <summary>
    /// Barrera reforzada para peticiones remotas en modo disco completo:
    /// base de sistema + árbol personal completo del usuario.
    /// </summary>
    public static bool IsProtectedForRemote(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        if (IsDriveRootLike(path)) return true;
        string normalized;
        try { normalized = Path.GetFullPath(path); }
        catch { return true; }

        if (IsProtected(normalized)) return true;

        foreach (var root in _personalRoots)
            if (IsUnderOrEqual(normalized, root))
                return true;

        return false;
    }

    /// <summary>
    /// Validates a write target, including a path that does not exist yet. Unlike
    /// <see cref="IsProtectedForRemote"/>, a new child of a drive root is allowed
    /// unless it belongs to a known protected system or personal tree.
    /// </summary>
    public static bool IsProtectedForRemoteWriteTarget(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        string normalized;
        try { normalized = Path.GetFullPath(path); }
        catch { return true; }
        if (IsDriveRootLike(normalized)) return true;

        foreach (var root in _systemRoots)
            if (IsUnderOrEqual(normalized, root)) return true;
        foreach (var root in _personalRoots)
            if (IsUnderOrEqual(normalized, root)) return true;

        // Attributes can only be checked when the target already exists.
        if (File.Exists(normalized) || Directory.Exists(normalized))
        {
            try { return File.GetAttributes(normalized).HasFlag(FileAttributes.System); }
            catch { return true; }
        }
        return false;
    }
    private static bool IsDriveRootLike(string path)
    {
        var trimmed = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Regex.IsMatch(trimmed, @"^[a-zA-Z]:$")) return true;

        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            return !string.IsNullOrEmpty(root)
                && string.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                 root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                 StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static string NormalizeRoot(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
    }

    private static bool IsUnderOrEqual(string path, string root)
    {
        path = NormalizeRoot(path);
        root = NormalizeRoot(root);
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return true;
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
    }
}

