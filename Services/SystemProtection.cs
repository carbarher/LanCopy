using System;
using System.Collections.Generic;
using System.IO;

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
            if (!string.IsNullOrEmpty(p)) _systemRoots.Add(p);
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
        string normalized;
        try { normalized = Path.GetFullPath(path); }
        catch { return true; }

        if (IsProtected(normalized)) return true;

        foreach (var root in _personalRoots)
            if (IsUnderOrEqual(normalized, root))
                return true;

        return false;
    }

    private static bool IsUnderOrEqual(string path, string root)
    {
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return true;
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
    }
}