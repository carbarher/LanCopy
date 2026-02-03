using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SlskDown
{
    /// <summary>
    /// Regla de auto-descarga configurable
    /// </summary>
    public class DownloadRule
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public int Priority { get; set; } = 5; // 1-10, mayor = mÃ¡s prioritario
        
        // Condiciones
        public string AuthorPattern { get; set; } = ""; // Regex
        public string FilenamePattern { get; set; } = ""; // Regex
        public long MinSize { get; set; } = 0; // Bytes
        public long MaxSize { get; set; } = 0; // 0 = sin lÃ­mite
        public int MinBitrate { get; set; } = 0; // kbps
        public string[] RequiredExtensions { get; set; } = Array.Empty<string>();
        public string[] ExcludedWords { get; set; } = Array.Empty<string>();
        public string[] RequiredWords { get; set; } = Array.Empty<string>();
        public bool SpanishOnly { get; set; } = false;
        
        // Acciones
        public string TargetFolder { get; set; } = ""; // VacÃ­o = carpeta default
        public bool NotifyOnMatch { get; set; } = true;
        public int MaxDownloadsPerDay { get; set; } = 0; // 0 = sin lÃ­mite
        
        // EstadÃ­sticas
        public int MatchCount { get; set; } = 0;
        public int DownloadCount { get; set; } = 0;
        public DateTime LastMatch { get; set; } = DateTime.MinValue;

        // Regex compilados (no serializar)
        [System.Text.Json.Serialization.JsonIgnore]
        private Regex _authorRegex;
        
        [System.Text.Json.Serialization.JsonIgnore]
        private Regex _filenameRegex;

        /// <summary>
        /// Compila los patrones regex
        /// </summary>
        public void CompilePatterns()
        {
            try
            {
                if (!string.IsNullOrEmpty(AuthorPattern))
                {
                    _authorRegex = new Regex(AuthorPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error compilando AuthorPattern: {ex.Message}");
            }

            try
            {
                if (!string.IsNullOrEmpty(FilenamePattern))
                {
                    _filenameRegex = new Regex(FilenamePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error compilando FilenamePattern: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifica si un resultado coincide con esta regla
        /// </summary>
        public bool Matches(SearchResult result, Func<string, bool> isSpanishFunc = null)
        {
            if (!Enabled)
                return false;

            // Verificar autor
            if (_authorRegex != null && !_authorRegex.IsMatch(result.Username))
                return false;

            // Verificar filename
            if (_filenameRegex != null && !_filenameRegex.IsMatch(result.Filename))
                return false;

            // Verificar tamaÃ±o
            if (MinSize > 0 && result.Size < MinSize)
                return false;

            if (MaxSize > 0 && result.Size > MaxSize)
                return false;

            // Verificar bitrate
            if (MinBitrate > 0 && result.Bitrate < MinBitrate)
                return false;

            // Verificar extensiÃ³n
            if (RequiredExtensions != null && RequiredExtensions.Length > 0)
            {
                var ext = Path.GetExtension(result.Filename).TrimStart('.').ToLowerInvariant();
                if (!RequiredExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            // Verificar palabras excluidas
            if (ExcludedWords != null && ExcludedWords.Length > 0)
            {
                if (ExcludedWords.Any(word =>
                        !string.IsNullOrEmpty(word) &&
                        result.Filename.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                    return false;
            }

            // Verificar palabras requeridas
            if (RequiredWords != null && RequiredWords.Length > 0)
            {
                if (!RequiredWords.All(word =>
                        !string.IsNullOrEmpty(word) &&
                        result.Filename.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                    return false;
            }

            // Verificar espaÃ±ol
            if (SpanishOnly && isSpanishFunc != null)
            {
                if (!isSpanishFunc(result.Filename))
                    return false;
            }

            // Verificar lÃ­mite diario
            if (MaxDownloadsPerDay > 0)
            {
                // Esto se debe verificar externamente con el historial de descargas
                // AquÃ­ solo marcamos que la regla tiene lÃ­mite
            }

            return true;
        }

        /// <summary>
        /// Registra un match
        /// </summary>
        public void RecordMatch()
        {
            MatchCount++;
            LastMatch = DateTime.Now;
        }

        /// <summary>
        /// Registra una descarga
        /// </summary>
        public void RecordDownload()
        {
            DownloadCount++;
        }
    }

    /// <summary>
    /// Gestor de reglas de auto-descarga
    /// </summary>
    public class DownloadRulesManager
    {
        private readonly string _rulesFile;
        private List<DownloadRule> _rules = new List<DownloadRule>();
        private readonly object _lock = new object();

        public DownloadRulesManager(string rulesFile = "download_rules.json")
        {
            _rulesFile = rulesFile;
            LoadRules();
        }

        /// <summary>
        /// Carga las reglas desde el archivo
        /// </summary>
        public void LoadRules()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_rulesFile))
                    {
                        var json = File.ReadAllText(_rulesFile);
                        _rules = JsonSerializer.Deserialize<List<DownloadRule>>(json) ?? new List<DownloadRule>();
                        
                        // Compilar patrones
                        foreach (var rule in _rules)
                        {
                            rule.CompilePatterns();
                        }
                    }
                    else
                    {
                        // Crear reglas por defecto
                        CreateDefaultRules();
                        SaveRules();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cargando reglas: {ex.Message}");
                    _rules = new List<DownloadRule>();
                }
            }
        }

        /// <summary>
        /// Guarda las reglas al archivo
        /// </summary>
        public void SaveRules()
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    var json = JsonSerializer.Serialize(_rules, options);
                    File.WriteAllText(_rulesFile, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error guardando reglas: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Crea reglas por defecto
        /// </summary>
        private void CreateDefaultRules()
        {
            _rules = new List<DownloadRule>
            {
                new DownloadRule
                {
                    Name = "Libros en espaÃ±ol (EPUB/PDF)",
                    Enabled = true,
                    Priority = 10,
                    RequiredExtensions = new[] { "epub", "pdf", "mobi" },
                    SpanishOnly = true,
                    MinSize = 100 * 1024, // 100 KB
                    MaxSize = 50 * 1024 * 1024, // 50 MB
                    NotifyOnMatch = true
                },
                new DownloadRule
                {
                    Name = "Alta calidad (>192 kbps)",
                    Enabled = false,
                    Priority = 8,
                    MinBitrate = 192,
                    RequiredExtensions = new[] { "mp3", "flac", "m4a" },
                    NotifyOnMatch = false
                },
                new DownloadRule
                {
                    Name = "Excluir comics",
                    Enabled = true,
                    Priority = 5,
                    ExcludedWords = new[] { "comic", "manga", "cbr", "cbz" },
                    RequiredExtensions = new[] { "epub", "pdf" }
                }
            };

            foreach (var rule in _rules)
            {
                rule.CompilePatterns();
            }
        }

        /// <summary>
        /// Agrega una regla
        /// </summary>
        public void AddRule(DownloadRule rule)
        {
            lock (_lock)
            {
                rule.CompilePatterns();
                _rules.Add(rule);
                SaveRules();
            }
        }

        /// <summary>
        /// Elimina una regla
        /// </summary>
        public void RemoveRule(string name)
        {
            lock (_lock)
            {
                _rules.RemoveAll(r => r.Name == name);
                SaveRules();
            }
        }

        /// <summary>
        /// Obtiene todas las reglas
        /// </summary>
        public List<DownloadRule> GetRules()
        {
            lock (_lock)
            {
                return new List<DownloadRule>(_rules);
            }
        }

        /// <summary>
        /// Encuentra la mejor regla que coincida con un resultado
        /// </summary>
        public DownloadRule FindBestMatch(SearchResult result, Func<string, bool> isSpanishFunc = null)
        {
            lock (_lock)
            {
                return _rules
                    .Where(r => r.Matches(result, isSpanishFunc))
                    .OrderByDescending(r => r.Priority)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Verifica si un resultado debe ser descargado segÃºn las reglas
        /// </summary>
        public bool ShouldDownload(SearchResult result, Func<string, bool> isSpanishFunc = null)
        {
            var rule = FindBestMatch(result, isSpanishFunc);
            if (rule != null)
            {
                rule.RecordMatch();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Obtiene estadÃ­sticas de las reglas
        /// </summary>
        public string GetStatistics()
        {
            lock (_lock)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—");
                sb.AppendLine("â•‘           ESTADÃSTICAS DE REGLAS                         â•‘");
                sb.AppendLine("â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
                sb.AppendLine();

                foreach (var rule in _rules.OrderByDescending(r => r.Priority))
                {
                    var status = rule.Enabled ? "âœ…" : "âŒ";
                    sb.AppendLine($"{status} {rule.Name} (Prioridad: {rule.Priority})");
                    sb.AppendLine($"   Matches: {rule.MatchCount}");
                    sb.AppendLine($"   Descargas: {rule.DownloadCount}");
                    if (rule.LastMatch != DateTime.MinValue)
                    {
                        sb.AppendLine($"   Ãšltimo match: {rule.LastMatch:yyyy-MM-dd HH:mm:ss}");
                    }
                    sb.AppendLine();
                }

                return sb.ToString();
            }
        }
    }
}

