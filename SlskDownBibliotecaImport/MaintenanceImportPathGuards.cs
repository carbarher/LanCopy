using System;
using System.IO;

namespace SlskDownBibliotecaImport;

/// <summary>
/// Validación pura de rutas para importación externa (manual y auto-inicio).
/// </summary>
public static class MaintenanceImportPathGuards
{
    public enum PathValidationCode
    {
        Ok,
        DestMissingOrInvalid,
        SrcMissingOrInvalid,
        SameDirectory,
        NestedDirectoryConflict,
        PathError
    }

    public static PathValidationCode Validate(string? downloadDirectory, string? importSourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(downloadDirectory) || !Directory.Exists(downloadDirectory))
            return PathValidationCode.DestMissingOrInvalid;
        if (string.IsNullOrWhiteSpace(importSourceDirectory) || !Directory.Exists(importSourceDirectory))
            return PathValidationCode.SrcMissingOrInvalid;
        try
        {
            var d = Path.GetFullPath(downloadDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var s = Path.GetFullPath(importSourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.Equals(s, d, StringComparison.OrdinalIgnoreCase))
                return PathValidationCode.SameDirectory;

            var dRoot = d + Path.DirectorySeparatorChar;
            var sRoot = s + Path.DirectorySeparatorChar;
            if (sRoot.StartsWith(dRoot, StringComparison.OrdinalIgnoreCase)
                || dRoot.StartsWith(sRoot, StringComparison.OrdinalIgnoreCase))
                return PathValidationCode.NestedDirectoryConflict;
        }
        catch (Exception)
        {
            return PathValidationCode.PathError;
        }

        return PathValidationCode.Ok;
    }

    public static bool IsPathUnderDirectoryRoot(string candidatePath, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory)) return false;
        try
        {
            var root = Path.GetFullPath(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var full = Path.GetFullPath(candidatePath);
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                return true;
            return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
