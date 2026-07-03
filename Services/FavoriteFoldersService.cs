using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LanCopy.Services;

/// <summary>
/// Carpetas favoritas / accesos rápidos locales.
/// Persiste en JSON atómico. Máximo 20 entradas; usa MRU para las frecuentes.
/// </summary>
public static class FavoriteFoldersService
{
    public const int MaxFavorites = 20;

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy", "favorites.json");

    private static List<string> _cache = [];
    private static bool _loaded;
    private static readonly object _lock = new();

    public static List<string> Load()
    {
        lock (_lock)
        {
            if (_loaded) return [.. _cache];
            try
            {
                var items = JsonStore.Read<List<string>>(StorePath, []);
                _cache = (items ?? [])
                    .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                    .Take(MaxFavorites)
                    .ToList();
            }
            catch (Exception ex) { Log.Warn("favorites", "load-failed", new { error = ex.Message }); _cache = []; }
            _loaded = true;
            return [.. _cache];
        }
    }

    public static void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        // Normalizar y validar antes de guardar: evitar guardar rutas inválidas o junctions.
        try { path = Path.GetFullPath(path.Trim()); } catch { return; }
        if (!Directory.Exists(path)) return;
        lock (_lock)
        {
            Load();
            _cache.Remove(path);           // quitar duplicado
            _cache.Insert(0, path);        // insertar al principio (MRU)
            while (_cache.Count > MaxFavorites) _cache.RemoveAt(_cache.Count - 1);
            Save();
        }
    }

    public static void Remove(string path)
    {
        lock (_lock) { Load(); _cache.Remove(path); Save(); }
    }

    public static bool Contains(string path)
    {
        lock (_lock) { Load(); return _cache.Contains(path); }
    }

    private static void Save() =>
        JsonStore.WriteAtomic(StorePath, _cache);
}
