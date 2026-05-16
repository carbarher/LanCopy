using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Centralizado helper para generar nombres de archivo normalizados.
/// Patrón: "Compositor - Título - Formato.ext"
/// Ejemplo: "Bach, Johann Sebastian - The Art of Fugue - PDF.pdf"
/// </summary>
public static class FileNameHelper
{
    private static readonly HashSet<char> InvalidChars =
        new(Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '|', '?', '*', '/', '\\' }));

    /// <summary>
    /// Genera un nombre de archivo normalizado.
    /// Patrón: "{Compositor} - {Título} - {Formato}.{ext}"
    /// </summary>
    /// <param name="composer">Compositor (puede estar vacío)</param>
    /// <param name="title">Título de la obra</param>
    /// <param name="format">Formato (PDF, MXL, XML, MIDI, etc.)</param>
    /// <returns>Nombre de archivo sanitizado</returns>
    public static string GenerateFileName(string? composer, string? title, string? format)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(composer))
            parts.Add(composer.Trim());

        if (!string.IsNullOrWhiteSpace(title))
            parts.Add(title.Trim());

        if (!string.IsNullOrWhiteSpace(format))
            parts.Add(format.Trim().ToUpperInvariant());

        if (parts.Count == 0)
            return "unnamed.file";

        var baseName = string.Join(" - ", parts);
        var ext = !string.IsNullOrWhiteSpace(format)
            ? $".{format.ToLowerInvariant()}"
            : "";

        // Si no hay extensión válida, intentar inferir del formato
        if (string.IsNullOrWhiteSpace(ext) || ext == ".")
        {
            ext = InferExtensionFromFormat(!string.IsNullOrWhiteSpace(format) ? format : "");
        }

        return SanitizeFileName($"{baseName}{ext}");
    }

    /// <summary>
    /// Genera nombre con información adicional (fuente, año, etc.)
    /// Patrón: "{Compositor} - {Título} - {Formato} ({OtrosDatos}).{ext}"
    /// </summary>
    public static string GenerateFileNameWithMetadata(
        string? composer,
        string? title,
        string? format,
        string? additionalInfo = null)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(composer))
            parts.Add(composer.Trim());

        if (!string.IsNullOrWhiteSpace(title))
            parts.Add(title.Trim());

        if (!string.IsNullOrWhiteSpace(format))
            parts.Add(format.Trim().ToUpperInvariant());

        var baseName = parts.Count > 0 ? string.Join(" - ", parts) : "unnamed";

        if (!string.IsNullOrWhiteSpace(additionalInfo))
            baseName = $"{baseName} ({additionalInfo.Trim()})";

        var ext = !string.IsNullOrWhiteSpace(format)
            ? $".{format.ToLowerInvariant()}"
            : "";

        if (string.IsNullOrWhiteSpace(ext) || ext == ".")
        {
            ext = InferExtensionFromFormat(!string.IsNullOrWhiteSpace(format) ? format : "");
        }

        return SanitizeFileName($"{baseName}{ext}");
    }

    /// <summary>
    /// Sanitiza un nombre de archivo existente manteniendo la extensión.
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        var ext = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);

        // Sanitizar el nombre base
        var sanitized = new StringBuilder(baseName.Length);
        foreach (var ch in baseName)
        {
            if (InvalidChars.Contains(ch))
                sanitized.Append('_');
            else
                sanitized.Append(ch);
        }

        var result = sanitized.ToString()
            .Replace("  ", " ")  // Colapsar espacios múltiples
            .Trim()
            .TrimEnd('.', ' ', '-');  // Quitar caracteres finales peligrosos

        if (string.IsNullOrWhiteSpace(result))
            result = "unnamed";

        // Limitar a 200 caracteres antes de la extensión
        if (result.Length > 200)
            result = result[..200].TrimEnd('_', ' ', '-');

        return result + ext;
    }

    /// <summary>
    /// Infiere la extensión basada en el formato.
    /// </summary>
    private static string InferExtensionFromFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return "";

        var upper = format.Trim().ToUpperInvariant();

        return upper switch
        {
            "PDF" => ".pdf",
            "MXL" => ".mxl",
            "XML" or "MUSICXML" => ".xml",
            "MIDI" or "MID" => ".mid",
            "MSCZ" => ".mscz",
            "MSCX" => ".mscx",
            "EPUB" => ".epub",
            "LY" => ".ly",
            "TXT" => ".txt",
            _ => $".{upper.ToLowerInvariant()}"
        };
    }

    /// <summary>
    /// Valida que un formato sea reconocido.
    /// </summary>
    public static bool IsValidFormat(string? format)
    {
        return !string.IsNullOrWhiteSpace(format) &&
               !InferExtensionFromFormat(format).Equals(
                   $".{format.Trim().ToLowerInvariant()}",
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normaliza un compositor (ej: "Bach, J.S." → "Bach, Johann Sebastian" si es posible)
    /// </summary>
    public static string NormalizeComposer(string? composer)
    {
        if (string.IsNullOrWhiteSpace(composer))
            return "Anónimo";

        var normalized = composer
            .Trim()
            .Replace("  ", " ");

        return normalized;
    }

    /// <summary>
    /// Normaliza un título (quitar caracteres especiales, espacios múltiples, etc.)
    /// </summary>
    public static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Sin título";

        var normalized = title
            .Trim()
            .Replace("  ", " ");

        return normalized;
    }

    /// <summary>
    /// Valida y corrige un nombre de archivo ANTES de conversión.
    /// Patrón esperado: "{Compositor} - {Título} - {Formato}.ext"
    /// Si no cumple, intenta renombrar al patrón correcto.
    /// </summary>
    /// <param name="inputPath">Ruta completa del archivo</param>
    /// <param name="correctedPath">Ruta del archivo tras corrección (puede ser igual a input)</param>
    /// <returns>True si se renombró, False si ya era válido o no se pudo renombrar</returns>
    public static bool TryValidateAndCorrectFileName(string inputPath, out string correctedPath)
    {
        correctedPath = inputPath;

        if (!File.Exists(inputPath))
            return false;

        var dir = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var fileName = Path.GetFileName(inputPath);
        var ext = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);


        // Si no tiene autor, borrar archivo y retornar false
        // Autor = primer campo del patrón esperado
        var parts = baseName.Split(new[] { " - " }, StringSplitOptions.None);
        if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0]) || parts[0].Trim().ToLowerInvariant() == "anónimo")
        {
            try { File.Delete(inputPath); } catch { }
            correctedPath = inputPath;
            return false;
        }

        // Patrón: "Algo - Algo - Algo" (mínimo 3 partes separadas por " - ")
        // Esto indica que ya sigue el formato "Compositor - Título - Formato"
        if (IsValidPattern(baseName))
            return false; // Ya es válido

        // Intentar extraer información y renombrar
        var correctedName = TryExtractAndBuildFileName(baseName, ext);
        if (string.Equals(correctedName, fileName, StringComparison.OrdinalIgnoreCase))
            return false; // No se pudo mejorar

        var newPath = Path.Combine(dir, correctedName);
        try
        {
            if (File.Exists(newPath))
            {
                // Si ya existe, agregar timestamp o GUID
                var uniqueName = $"{Path.GetFileNameWithoutExtension(correctedName)}_{Guid.NewGuid():N}{Path.GetExtension(correctedName)}";
                newPath = Path.Combine(dir, uniqueName);
            }

            File.Move(inputPath, newPath, overwrite: false);
            correctedPath = newPath;
            return true;
        }
        catch
        {
            // Si no se puede renombrar, retornar la ruta original
            correctedPath = inputPath;
            return false;
        }
    }

    /// <summary>
    /// Valida si un nombre base sigue el patrón esperado.
    /// Patrón: debe tener al menos 3 partes separadas por " - "
    /// </summary>
    private static bool IsValidPattern(string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
            return false;

        var parts = baseName.Split(new[] { " - " }, StringSplitOptions.None);
        return parts.Length >= 3 && parts.All(p => !string.IsNullOrWhiteSpace(p));
    }

    /// <summary>
    /// Intenta extraer información del nombre y construir uno válido.
    /// Estrategia: si tiene guiones o guiones bajos, usarlos como separadores.
    /// Fallback: generar nombre genérico con timestamp.
    /// </summary>
    private static string TryExtractAndBuildFileName(string baseName, string ext)
    {
        if (string.IsNullOrWhiteSpace(baseName))
            return $"unnamed_{DateTime.UtcNow:yyyyMMdd_HHmmss}{ext}";

        // Limpiar baseName de caracteres inválidos
        var cleaned = SanitizeFileName(baseName + ext);
        var cleanedBase = Path.GetFileNameWithoutExtension(cleaned);

        // Si ya tiene " - " intente reparar espacios
        if (cleanedBase.Contains(" - "))
            return cleaned;

        // Intentar split por guiones o guiones bajos
        var parts = cleanedBase
            .Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (parts.Count >= 3)
        {
            // Usar primeras 3 partes como: Compositor - Título - Formato
            var corrected = $"{parts[0]} - {parts[1]} - {string.Join(" ", parts.Skip(2))}";
            return SanitizeFileName(corrected + ext);
        }

        if (parts.Count == 2)
        {
            // Usar como: Compositor - Título - sin_especificar
            return SanitizeFileName($"{parts[0]} - {parts[1]} - {Path.GetExtension(ext).TrimStart('.')}{ext}");
        }

        // Fallback: nombre genérico
        return $"unnamed_{DateTime.UtcNow:yyyyMMdd_HHmmss}{ext}";
    }
}
