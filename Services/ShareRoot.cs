using System;
using System.IO;

namespace LanCopy.Services;

/// <summary>
/// Confina TODAS las operaciones de fichero del servidor a una carpeta raiz compartida.
/// Sin esto, un peer puede leer/escribir cualquier ruta del disco (path traversal).
/// La raiz se puede configurar; por defecto es una subcarpeta seria del perfil del usuario.
/// </summary>
public static class ShareRoot
{
    private static readonly object _lock = new();
    private static string _root = DefaultRoot();

    public static string DefaultRoot()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "LanCopy", "Shared");
    }

    public static string Root
    {
        get { lock (_lock) return _root; }
    }

    public static void SetRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var full = Path.GetFullPath(path);
        lock (_lock) _root = full;
    }

    public static void EnsureRootExists()
    {
        try { Directory.CreateDirectory(Root); } catch { }
    }

    /// <summary>
    /// Resuelve una ruta entrante (posiblemente relativa a la raiz o absoluta) y verifica
    /// que queda DENTRO de la raiz compartida. Bloquea path traversal y symlinks que
    /// escapen de la raiz. Devuelve false con motivo si no es valida.
    /// </summary>
    public static bool TryResolve(string? incoming, out string fullPath, out string reason)
    {
        fullPath = "";
        reason = "";
        var root = Root;

        try
        {
            string candidate;
            if (string.IsNullOrEmpty(incoming))
            {
                // path vacio = raiz compartida (listar contenido de la raiz)
                candidate = root;
            }
            else if (Path.IsPathRooted(incoming))
            {
                candidate = Path.GetFullPath(incoming);
            }
            else
            {
                candidate = Path.GetFullPath(Path.Combine(root, incoming));
            }

            // Resolver symlinks: comparamos la ruta canonica real cuando existe.
            var canonical = ResolveCanonical(candidate);
            var canonicalRoot = ResolveCanonical(root);

            if (!IsInside(canonicalRoot, canonical))
            {
                reason = "svc.outsideShare";
                return false;
            }

            // Defensa extra: un directorio intermedio puede ser una junction/symlink que
            // apunte fuera aunque la ruta canonica del hijo no se resuelva (fichero normal
            // bajo un dir enlazado). Bloquea cualquier reparse point entre la raiz y el destino.
            if (HasReparsePointBetween(root, candidate))
            {
                reason = "svc.reparse";
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch
        {
            reason = "svc.invalidPath";
            return false;
        }
    }

    // Recorre los componentes desde la raiz (exclusive) hasta el destino y devuelve true
    // si alguno es un reparse point (junction/symlink). El propio root se considera de confianza.
    private static bool HasReparsePointBetween(string root, string candidate)
    {
        try
        {
            var rootNorm = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var current = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar);
            while (!string.IsNullOrEmpty(current) &&
                   !string.Equals(current, rootNorm, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(current) || Directory.Exists(current))
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
                }
                var parent = Directory.GetParent(current)?.FullName;
                if (string.IsNullOrEmpty(parent) || parent == current) break;
                current = parent.TrimEnd(Path.DirectorySeparatorChar);
            }
        }
        catch { return true; }
        return false;
    }

    private static string ResolveCanonical(string path)
    {
        try
        {
            // .NET resuelve symlinks con ResolveLinkTarget en ficheros/dirs existentes.
            var info = Directory.Exists(path) ? (FileSystemInfo)new DirectoryInfo(path)
                       : File.Exists(path) ? new FileInfo(path)
                       : null;
            if (info != null)
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target != null) return Path.GetFullPath(target.FullName);
            }
        }
        catch { }
        return Path.GetFullPath(path);
    }

    private static bool IsInside(string root, string candidate)
    {
        var r = AppendSep(root);
        var c = AppendSep(candidate);
        // candidate == root tambien es valido (la propia raiz)
        return c.StartsWith(r, StringComparison.OrdinalIgnoreCase)
               || string.Equals(candidate.TrimEnd(Path.DirectorySeparatorChar),
                                root.TrimEnd(Path.DirectorySeparatorChar),
                                StringComparison.OrdinalIgnoreCase);
    }

    private static string AppendSep(string p)
    {
        if (string.IsNullOrEmpty(p)) return p;
        return p.EndsWith(Path.DirectorySeparatorChar) ? p : p + Path.DirectorySeparatorChar;
    }
}