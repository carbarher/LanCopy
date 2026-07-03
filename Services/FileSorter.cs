using System;
using System.Collections.Generic;
using LanCopy.Models;

namespace LanCopy.Services;

/// <summary>
/// Ordenacion pura de entradas de archivo. Mantiene ".." al principio y agrupa
/// carpetas antes que archivos. Sin dependencias de UI (testeable).
/// </summary>
public static class FileSorter
{
    /// <summary>
    /// M12: Refactor de 5 pasadas LINQ + 4 ToList() intermedios a 1 pasada de clasificación
    /// + List.Sort in-place. La firma devuelve List&lt;T&gt; directamente (en lugar de IEnumerable)
    /// para eliminar el .ToList() del call site (M16).
    ///
    /// Antes:
    ///   pasada1: list.Where(..).ToList()  — dotdot
    ///   pasada2: list.Where(..).ToList()  — dirs
    ///   pasada3: list.Where(..).ToList()  — files
    ///   pasada4: g.OrderBy(..)           — dirs ordenado (materializa otra lista)
    ///   pasada5: g.OrderBy(..)           — files ordenado
    ///   + Concat → enumeracion diferida que .ToList() del call site materializa (lista 5)
    ///
    /// Ahora:
    ///   1 bucle foreach de clasificación + 2 List.Sort in-place
    ///   Resultado ya materializado — 0 listas extra en el call site.
    /// </summary>
    public static List<FileEntry> Sort(IEnumerable<FileEntry> items, string field, bool asc)
    {
        FileEntry? dotdot = null;
        var dirs  = new List<FileEntry>();
        var files = new List<FileEntry>();

        // 1 pasada: clasificar en grupos sin ToList() ni Where()
        foreach (var f in items)
        {
            if (f.Name == "..") dotdot = f;
            else if (f.IsDirectory) dirs.Add(f);
            else files.Add(f);
        }

        // Comparador en función del campo y dirección — List.Sort es in-place (TimSort)
        Comparison<FileEntry> cmp = field switch
        {
            "size" => asc
                ? static (a, b) => a.Size.CompareTo(b.Size)
                : static (a, b) => b.Size.CompareTo(a.Size),
            "date" => asc
                ? static (a, b) => a.LastWriteUtcTicks.CompareTo(b.LastWriteUtcTicks)
                : static (a, b) => b.LastWriteUtcTicks.CompareTo(a.LastWriteUtcTicks),
            _ => asc
                ? (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                : (a, b) => string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase),
        };

        dirs.Sort(cmp);
        files.Sort(cmp);

        // Construir resultado final: ".." primero, luego dirs, luego files
        var result = new List<FileEntry>((dotdot != null ? 1 : 0) + dirs.Count + files.Count);
        if (dotdot != null) result.Add(dotdot);
        result.AddRange(dirs);
        result.AddRange(files);
        return result;
    }
}