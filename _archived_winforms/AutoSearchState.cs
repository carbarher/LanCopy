using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace SlskDown
{
    /// <summary>
    /// Estado persistente para bÃºsqueda automÃ¡tica con reconexiÃ³n
    /// </summary>
    public class AutoSearchState
    {
        public bool WasRunning { get; set; } = false;
        public int CurrentIndex { get; set; } = 0;
        public DateTime LastSearchTime { get; set; } = DateTime.MinValue;
        public string[] Authors { get; set; } = Array.Empty<string>();
        public int TotalAuthors { get; set; } = 0;
        public int CompletedPasses { get; set; } = 0;
        public int MaxPasses { get; set; } = 10;
        public int TimeoutSeconds { get; set; } = 60;
        public bool IsPaused { get; set; } = false;
        public string PauseReason { get; set; } = "";

        public void Save(string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                var tempPath = filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, filePath, true);
                Console.WriteLine($"[AutoSearchState] âœ… Estado guardado: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoSearchState] âŒ Error guardando estado: {ex.Message}");
            }
        }

        public static AutoSearchState Load(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var state = JsonSerializer.Deserialize<AutoSearchState>(json);
                    Console.WriteLine($"[AutoSearchState] âœ… Estado cargado: WasRunning={state?.WasRunning}, Index={state?.CurrentIndex}");
                    return state ?? new AutoSearchState();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoSearchState] âŒ Error cargando estado: {ex.Message}");
            }
            
            return new AutoSearchState();
        }

        public void Clear()
        {
            WasRunning = false;
            CurrentIndex = 0;
            LastSearchTime = DateTime.MinValue;
            Authors = Array.Empty<string>();
            TotalAuthors = 0;
            CompletedPasses = 0;
            IsPaused = false;
            PauseReason = "";
            
            Console.WriteLine("[AutoSearchState] ðŸ§¹ Estado limpiado");
        }

        public override string ToString()
        {
            return $"AutoSearchState: Running={WasRunning}, Index={CurrentIndex}/{TotalAuthors}, Passes={CompletedPasses}/{MaxPasses}";
        }
    }
}

