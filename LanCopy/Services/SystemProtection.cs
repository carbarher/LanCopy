using System;
using System.Collections.Generic;
using System.IO;

namespace LanCopy.Services;

/// <summary>
/// Determina si una ruta del sistema operativo está protegida contra borrado/renombrado.
/// Aplica tanto en el servidor (PC remoto) como en el cliente local.
/// </summary>
internal static class SystemProtection
{
    private static readonly HashSet<string> _protectedRoots;

    static SystemProtection()
    {
        _protectedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(Environment.SpecialFolder f)
        {
            var p = Environment.GetFolderPath(f);
            if (!string.IsNullOrEmpty(p)) _protectedRoots.Add(p);
        }

        Add(Environment.SpecialFolder.Windows);
        Add(Environment.SpecialFolder.System);
        Add(Environment.SpecialFolder.SystemX86);
        Add(Environment.SpecialFolder.ProgramFiles);
        Add(Environment.SpecialFolder.ProgramFilesX86);
        Add(Environment.SpecialFolder.CommonApplicationData); // C:\ProgramData
    }

    /// <summary>
    /// Devuelve true si la ruta NO debe ser modificada (borrar, renombrar).
    /// </summary>
    public static bool IsProtected(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        // Raíces de unidad siempre protegidas (C:\, D:\, etc.)
        if (Path.GetPathRoot(path)?.Equals(path, StringComparison.OrdinalIgnoreCase) == true) return true;

        // Rutas de sistema conocidas
        foreach (var root in _protectedRoots)
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return true;

        // Atributo de sistema en el archivo/carpeta
        try
        {
            var attr = File.GetAttributes(path);
            if (attr.HasFlag(FileAttributes.System)) return true;
        }
        catch { /* path no accesible → tratar como protegido */ return true; }

        return false;
    }
}
