using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace SlskDown.Core
{
    public class ChatMessage
    {
        public string Role { get; set; } // "user", "assistant", "system"
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ConversationContext
    {
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
        public List<string> FavoriteAuthors { get; set; } = new List<string>();
        public string LastSearchedAuthor { get; set; }
        public string LastDownloadedFile { get; set; }
        public DateTime SessionStart { get; set; } = DateTime.Now;
        
        public void AddMessage(string role, string content)
        {
            Messages.Add(new ChatMessage
            {
                Role = role,
                Content = content,
                Timestamp = DateTime.Now
            });

            // Mantener solo últimos 50 mensajes
            if (Messages.Count > 50)
                Messages.RemoveAt(0);
        }

        public List<ChatMessage> GetRecentMessages(int count = 10)
        {
            return Messages.TakeLast(count).ToList();
        }

        public string GetContextSummary()
        {
            var summary = new System.Text.StringBuilder();
            
            if (!string.IsNullOrEmpty(LastSearchedAuthor))
                summary.AppendLine($"Último autor buscado: {LastSearchedAuthor}");
            
            if (!string.IsNullOrEmpty(LastDownloadedFile))
                summary.AppendLine($"Última descarga: {LastDownloadedFile}");
            
            if (FavoriteAuthors.Count > 0)
                summary.AppendLine($"Autores favoritos: {string.Join(", ", FavoriteAuthors.Take(5))}");
            
            return summary.ToString();
        }
    }

    public class ChatShortcut
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public int TimesUsed { get; set; } = 0;
    }

    public static class ChatMemory
    {
        private static ConversationContext context = new ConversationContext();
        private static Dictionary<string, ChatShortcut> shortcuts = new Dictionary<string, ChatShortcut>(StringComparer.OrdinalIgnoreCase);
        private static string dataFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "chat_memory.json");
        private static string shortcutsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "chat_shortcuts.json");

        public static void AddMessage(string role, string content)
        {
            context.AddMessage(role, content);
            Save();
        }

        public static void SetVariable(string key, string value)
        {
            context.Variables[key] = value;
            Save();
        }

        public static string GetVariable(string key)
        {
            return context.Variables.TryGetValue(key, out var value) ? value : null;
        }

        public static void AddFavoriteAuthor(string author)
        {
            if (!context.FavoriteAuthors.Contains(author, StringComparer.OrdinalIgnoreCase))
            {
                context.FavoriteAuthors.Add(author);
                Save();
            }
        }

        public static List<string> GetFavoriteAuthors() => context.FavoriteAuthors.ToList();

        public static void SetLastSearchedAuthor(string author)
        {
            context.LastSearchedAuthor = author;
            Save();
        }

        public static string GetLastSearchedAuthor() => context.LastSearchedAuthor;

        public static void SetLastDownloadedFile(string filename)
        {
            context.LastDownloadedFile = filename;
            Save();
        }

        public static string GetContextSummary() => context.GetContextSummary();

        public static List<ChatMessage> GetRecentMessages(int count = 10)
        {
            return context.GetRecentMessages(count);
        }

        public static string BuildPrompt(string userMessage, int maxMessages = 10)
        {
            var prompt = new System.Text.StringBuilder();
            
            // Agregar contexto reciente
            // Agregar historial reciente (configurable)
            if (context.Messages.Count > 0)
            {
                prompt.AppendLine("\nCONVERSACIÓN RECIENTE:");
                foreach (var msg in context.Messages.TakeLast(maxMessages))
                {
                    prompt.AppendLine($"{msg.Role}: {msg.Content.Substring(0, Math.Min(100, msg.Content.Length))}...");
                }
                prompt.AppendLine();
            }

            // Agregar resumen de contexto
            var summary = context.GetContextSummary();
            if (!string.IsNullOrEmpty(summary))
            {
                prompt.AppendLine("Información relevante:");
                prompt.AppendLine(summary);
                prompt.AppendLine();
            }

            // Agregar mensaje actual
            prompt.AppendLine($"Usuario: {userMessage}");

            return prompt.ToString();
        }

        // Atajos personalizados
        public static void AddShortcut(string name, string command)
        {
            shortcuts[name] = new ChatShortcut
            {
                Name = name,
                Command = command
            };
            SaveShortcuts();
        }

        public static void RemoveShortcut(string name)
        {
            shortcuts.Remove(name);
            SaveShortcuts();
        }

        public static string GetShortcut(string name)
        {
            if (shortcuts.TryGetValue(name, out var shortcut))
            {
                shortcut.TimesUsed++;
                SaveShortcuts();
                return shortcut.Command;
            }
            return null;
        }

        public static List<ChatShortcut> GetAllShortcuts() => shortcuts.Values.ToList();

        public static string ExpandShortcuts(string input)
        {
            foreach (var shortcut in shortcuts.Values)
            {
                if (input.Contains(shortcut.Name, StringComparison.OrdinalIgnoreCase))
                {
                    input = input.Replace(shortcut.Name, shortcut.Command, StringComparison.OrdinalIgnoreCase);
                    shortcut.TimesUsed++;
                }
            }
            SaveShortcuts();
            return input;
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(dataFile))
                {
                    var json = File.ReadAllText(dataFile);
                    context = JsonSerializer.Deserialize<ConversationContext>(json) ?? new ConversationContext();
                }

                if (File.Exists(shortcutsFile))
                {
                    var json = File.ReadAllText(shortcutsFile);
                    var list = JsonSerializer.Deserialize<List<ChatShortcut>>(json) ?? new List<ChatShortcut>();
                    shortcuts = list.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var dataDir = Path.GetDirectoryName(dataFile);
                Directory.CreateDirectory(dataDir);

                var json = JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dataFile, json);
            }
            catch { }
        }

        private static void SaveShortcuts()
        {
            try
            {
                var dataDir = Path.GetDirectoryName(shortcutsFile);
                Directory.CreateDirectory(dataDir);

                var json = JsonSerializer.Serialize(shortcuts.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(shortcutsFile, json);
            }
            catch { }
        }

        public static void Clear()
        {
            context = new ConversationContext();
            Save();
        }
    }
}
