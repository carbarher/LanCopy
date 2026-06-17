using System.Collections.Generic;
using System.Linq;
using LanCopy.Models;

namespace LanCopy.Services;

/// <summary>
/// Persistencia del historial de transferencias. Aplica un tope máximo de
/// entradas y delega la E/S atómica en JsonStore. Sin dependencias de UI (testeable).
/// </summary>
public static class HistoryStore
{
    public const int MaxEntries = 50;

    public static bool Save(string path, IEnumerable<TransferRecord> records)
        => JsonStore.WriteAtomic(path, records.Take(MaxEntries).ToList());

    public static List<TransferRecord> Load(string path)
    {
        var items = JsonStore.Read<List<TransferRecord>>(path, new List<TransferRecord>());
        if (items == null) return new List<TransferRecord>();
        return items.Take(MaxEntries).ToList();
    }
}