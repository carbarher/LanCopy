using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core.Collections
{
    /// <summary>
    /// Modo "Coleccionista" - Gestiona colecciones de archivos y auto-completado
    /// </summary>
    public class CollectionManager
    {
        private readonly List<Collection> collections;
        private readonly string collectionsPath;
        private readonly object collectionsLock = new object();

        public event Action<string> OnLog;
        public event Action<Collection> OnCollectionUpdated;
        public event Action<Collection, CollectionItem> OnItemFound;

        public CollectionManager(string dataDir)
        {
            collections = new List<Collection>();
            collectionsPath = Path.Combine(dataDir, "collections.json");
        }

        /// <summary>
        /// Crea una nueva colección
        /// </summary>
        public Collection CreateCollection(string name, string description, CollectionType type)
        {
            lock (collectionsLock)
            {
                var collection = new Collection
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = description,
                    Type = type,
                    CreatedAt = DateTime.Now,
                    AutoSearch = true,
                    Items = new List<CollectionItem>()
                };

                collections.Add(collection);
                Log($"✅ Colección creada: {name}");
                return collection;
            }
        }

        /// <summary>
        /// Agrega un item a una colección
        /// </summary>
        public void AddItem(string collectionId, string name, string searchTerm = null, bool required = true)
        {
            lock (collectionsLock)
            {
                var collection = collections.FirstOrDefault(c => c.Id == collectionId);
                if (collection == null)
                {
                    Log($"❌ Colección no encontrada: {collectionId}");
                    return;
                }

                var item = new CollectionItem
                {
                    Name = name,
                    SearchTerm = searchTerm ?? name,
                    Required = required,
                    Status = CollectionItemStatus.Missing,
                    AddedAt = DateTime.Now
                };

                collection.Items.Add(item);
                Log($"✅ Item agregado a '{collection.Name}': {name}");
                OnCollectionUpdated?.Invoke(collection);
            }
        }

        /// <summary>
        /// Marca un item como encontrado
        /// </summary>
        public void MarkItemFound(string collectionId, string itemName, string filePath, long fileSize)
        {
            lock (collectionsLock)
            {
                var collection = collections.FirstOrDefault(c => c.Id == collectionId);
                if (collection == null) return;

                var item = collection.Items.FirstOrDefault(i => i.Name == itemName);
                if (item == null) return;

                item.Status = CollectionItemStatus.Found;
                item.FoundAt = DateTime.Now;
                item.FilePath = filePath;
                item.FileSize = fileSize;

                Log($"✅ Item encontrado en '{collection.Name}': {itemName}");
                OnItemFound?.Invoke(collection, item);
                OnCollectionUpdated?.Invoke(collection);
            }
        }

        /// <summary>
        /// Marca un item como descargado
        /// </summary>
        public void MarkItemDownloaded(string collectionId, string itemName)
        {
            lock (collectionsLock)
            {
                var collection = collections.FirstOrDefault(c => c.Id == collectionId);
                if (collection == null) return;

                var item = collection.Items.FirstOrDefault(i => i.Name == itemName);
                if (item == null) return;

                item.Status = CollectionItemStatus.Downloaded;
                item.DownloadedAt = DateTime.Now;

                Log($"✅ Item descargado en '{collection.Name}': {itemName}");
                OnCollectionUpdated?.Invoke(collection);
            }
        }

        /// <summary>
        /// Obtiene estadísticas de una colección
        /// </summary>
        public CollectionStats GetStats(string collectionId)
        {
            lock (collectionsLock)
            {
                var collection = collections.FirstOrDefault(c => c.Id == collectionId);
                if (collection == null) return null;

                var stats = new CollectionStats
                {
                    CollectionName = collection.Name,
                    TotalItems = collection.Items.Count,
                    DownloadedItems = collection.Items.Count(i => i.Status == CollectionItemStatus.Downloaded),
                    FoundItems = collection.Items.Count(i => i.Status == CollectionItemStatus.Found),
                    MissingItems = collection.Items.Count(i => i.Status == CollectionItemStatus.Missing),
                    SearchingItems = collection.Items.Count(i => i.Status == CollectionItemStatus.Searching),
                    TotalSize = collection.Items.Where(i => i.FileSize > 0).Sum(i => i.FileSize)
                };

                stats.CompletionPercentage = stats.TotalItems > 0
                    ? (stats.DownloadedItems * 100.0 / stats.TotalItems)
                    : 0;

                return stats;
            }
        }

        /// <summary>
        /// Obtiene items faltantes de una colección
        /// </summary>
        public List<CollectionItem> GetMissingItems(string collectionId)
        {
            lock (collectionsLock)
            {
                var collection = collections.FirstOrDefault(c => c.Id == collectionId);
                if (collection == null) return new List<CollectionItem>();

                return collection.Items
                    .Where(i => i.Status == CollectionItemStatus.Missing || i.Status == CollectionItemStatus.Searching)
                    .ToList();
            }
        }

        /// <summary>
        /// Obtiene todas las colecciones
        /// </summary>
        public List<Collection> GetAllCollections()
        {
            lock (collectionsLock)
            {
                return collections.ToList();
            }
        }

        /// <summary>
        /// Elimina una colección
        /// </summary>
        public bool RemoveCollection(string collectionId)
        {
            lock (collectionsLock)
            {
                var collection = collections.FirstOrDefault(c => c.Id == collectionId);
                if (collection == null) return false;

                collections.Remove(collection);
                Log($"🗑️ Colección eliminada: {collection.Name}");
                return true;
            }
        }

        /// <summary>
        /// Guarda colecciones en archivo
        /// </summary>
        public async Task SaveAsync()
        {
            try
            {
                List<Collection> toSave;
                lock (collectionsLock)
                {
                    toSave = collections.ToList();
                }

                var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(collectionsPath, json);
                Log($"💾 Colecciones guardadas: {toSave.Count}");
            }
            catch (Exception ex)
            {
                Log($"❌ Error guardando colecciones: {ex.Message}");
            }
        }

        /// <summary>
        /// Carga colecciones desde archivo
        /// </summary>
        public async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(collectionsPath))
                {
                    Log("No hay colecciones guardadas");
                    return;
                }

                var json = await File.ReadAllTextAsync(collectionsPath);
                var loaded = JsonSerializer.Deserialize<List<Collection>>(json);

                if (loaded != null)
                {
                    lock (collectionsLock)
                    {
                        collections.Clear();
                        collections.AddRange(loaded);
                    }

                    Log($"✅ Colecciones cargadas: {loaded.Count}");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error cargando colecciones: {ex.Message}");
            }
        }

        /// <summary>
        /// Detecta duplicados en una colección
        /// </summary>
        public List<(CollectionItem item1, CollectionItem item2)> FindDuplicates(string collectionId)
        {
            lock (collectionsLock)
            {
                var collection = collections.FirstOrDefault(c => c.Id == collectionId);
                if (collection == null) return new List<(CollectionItem, CollectionItem)>();

                var duplicates = new List<(CollectionItem, CollectionItem)>();
                var items = collection.Items.ToList();

                for (int i = 0; i < items.Count; i++)
                {
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        if (AreSimilar(items[i].Name, items[j].Name))
                        {
                            duplicates.Add((items[i], items[j]));
                        }
                    }
                }

                return duplicates;
            }
        }

        private bool AreSimilar(string name1, string name2)
        {
            // Normalizar nombres
            var n1 = name1.ToLowerInvariant().Trim();
            var n2 = name2.ToLowerInvariant().Trim();

            // Exactamente iguales
            if (n1 == n2) return true;

            // Muy similares (Levenshtein distance < 3)
            var distance = LevenshteinDistance(n1, n2);
            return distance < 3;
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            var len1 = s1.Length;
            var len2 = s2.Length;
            var matrix = new int[len1 + 1, len2 + 1];

            for (int i = 0; i <= len1; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= len2; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost
                    );
                }
            }

            return matrix[len1, len2];
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }

    public class Collection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public CollectionType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool AutoSearch { get; set; }
        public List<CollectionItem> Items { get; set; }
    }

    public class CollectionItem
    {
        public string Name { get; set; }
        public string SearchTerm { get; set; }
        public bool Required { get; set; }
        public CollectionItemStatus Status { get; set; }
        public DateTime AddedAt { get; set; }
        public DateTime? FoundAt { get; set; }
        public DateTime? DownloadedAt { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
    }

    public enum CollectionType
    {
        Books,      // Libros de un autor
        Music,      // Discografía
        Series,     // Serie de libros
        Custom      // Personalizado
    }

    public enum CollectionItemStatus
    {
        Missing,    // No encontrado
        Searching,  // Buscando
        Found,      // Encontrado pero no descargado
        Downloaded  // Descargado
    }

    public class CollectionStats
    {
        public string CollectionName { get; set; }
        public int TotalItems { get; set; }
        public int DownloadedItems { get; set; }
        public int FoundItems { get; set; }
        public int MissingItems { get; set; }
        public int SearchingItems { get; set; }
        public double CompletionPercentage { get; set; }
        public long TotalSize { get; set; }
    }
}
