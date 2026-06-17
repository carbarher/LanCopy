using System;
using System.Collections.Generic;
using System.Linq;
using LanCopy.Models;

namespace LanCopy.Services;

/// <summary>
/// Ordenacion pura de entradas de archivo. Mantiene ".." al principio y agrupa
/// carpetas antes que archivos. Sin dependencias de UI (testeable).
/// </summary>
public static class FileSorter
{
    public static IEnumerable<FileEntry> Sort(IEnumerable<FileEntry> items, string field, bool asc)
    {
        var list = items as IList<FileEntry> ?? items.ToList();
        var dotdot = list.Where(f => f.Name == "..").ToList();
        var dirs = list.Where(f => f.IsDirectory && f.Name != "..").ToList();
        var files = list.Where(f => !f.IsDirectory).ToList();

        IEnumerable<FileEntry> SortGroup(List<FileEntry> g) => field switch
        {
            "size" => asc ? g.OrderBy(f => f.Size) : g.OrderByDescending(f => f.Size),
            "date" => asc ? g.OrderBy(f => f.LastWriteUtcTicks) : g.OrderByDescending(f => f.LastWriteUtcTicks),
            _ => asc ? g.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                          : g.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase),
        };
        return dotdot.Concat(SortGroup(dirs)).Concat(SortGroup(files));
    }
}