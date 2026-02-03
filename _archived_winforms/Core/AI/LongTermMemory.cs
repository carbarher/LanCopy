using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace SlskDown.Core.AI
{
    public class MemoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Content { get; set; }
        public string Category { get; set; } // "preference", "fact", "conversation"
        public int AccessCount { get; set; } = 0;
        public DateTime LastAccessed { get; set; } = DateTime.Now;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Memoria de largo plazo persistente con búsqueda semántica
    /// </summary>
    public class LongTermMemory
    {
        private List<MemoryEntry> memories = new List<MemoryEntry>();
        private string memoryFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "long_term_memory.json");

        public void Remember(string content, string category = "fact", Dictionary<string, string> metadata = null)
        {
            var memory = new MemoryEntry
            {
                Content = content,
                Category = category,
                Metadata = metadata ?? new Dictionary<string, string>()
            };

            memories.Add(memory);
            Save();
        }

        public List<MemoryEntry> Recall(string query, int maxResults = 5)
        {
            var lower = query.ToLower();
            
            // Búsqueda simple por contenido
            var results = memories
                .Where(m => m.Content.ToLower().Contains(lower))
                .OrderByDescending(m => m.AccessCount)
                .ThenByDescending(m => m.Timestamp)
                .Take(maxResults)
                .ToList();

            // Actualizar contador de acceso
            foreach (var result in results)
            {
                result.AccessCount++;
                result.LastAccessed = DateTime.Now;
            }

            Save();
            return results;
        }

        public List<MemoryEntry> RecallByCategory(string category, int maxResults = 10)
        {
            return memories
                .Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Timestamp)
                .Take(maxResults)
                .ToList();
        }

        public List<MemoryEntry> RecallRecent(int days = 7, int maxResults = 20)
        {
            var cutoff = DateTime.Now.AddDays(-days);
            return memories
                .Where(m => m.Timestamp >= cutoff)
                .OrderByDescending(m => m.Timestamp)
                .Take(maxResults)
                .ToList();
        }

        public string FindRelatedMemory(string context)
        {
            var related = Recall(context, 1);
            if (related.Count > 0)
            {
                var memory = related[0];
                var daysAgo = (DateTime.Now - memory.Timestamp).TotalDays;
                
                if (daysAgo < 1)
                    return $"Hace {(int)(daysAgo * 24)} horas: {memory.Content}";
                else if (daysAgo < 7)
                    return $"Hace {(int)daysAgo} días: {memory.Content}";
                else if (daysAgo < 30)
                    return $"Hace {(int)(daysAgo / 7)} semanas: {memory.Content}";
                else
                    return $"Hace {(int)(daysAgo / 30)} meses: {memory.Content}";
            }

            return null;
        }

        public void ForgetOldMemories(int daysToKeep = 90)
        {
            var cutoff = DateTime.Now.AddDays(-daysToKeep);
            memories.RemoveAll(m => m.Timestamp < cutoff && m.AccessCount < 2);
            Save();
        }

        public Dictionary<string, int> GetMemoryStats()
        {
            return new Dictionary<string, int>
            {
                ["total"] = memories.Count,
                ["preferences"] = memories.Count(m => m.Category == "preference"),
                ["facts"] = memories.Count(m => m.Category == "fact"),
                ["conversations"] = memories.Count(m => m.Category == "conversation"),
                ["this_week"] = memories.Count(m => m.Timestamp >= DateTime.Now.AddDays(-7))
            };
        }

        public void Load()
        {
            try
            {
                if (File.Exists(memoryFile))
                {
                    var json = File.ReadAllText(memoryFile);
                    memories = JsonSerializer.Deserialize<List<MemoryEntry>>(json) ?? new List<MemoryEntry>();
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                var dataDir = Path.GetDirectoryName(memoryFile);
                Directory.CreateDirectory(dataDir);

                var json = JsonSerializer.Serialize(memories, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(memoryFile, json);
            }
            catch { }
        }

        public void Clear()
        {
            memories.Clear();
            Save();
        }
    }
}
