using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Auto-tagging y categorización de archivos usando IA
    /// </summary>
    public class AIFileTagger
    {
        private readonly OllamaClient ollama;
        
        public event Action<string> OnLog;

        public AIFileTagger(OllamaClient ollama)
        {
            this.ollama = ollama;
        }

        /// <summary>
        /// Genera tags automáticos para un archivo
        /// </summary>
        public async Task<FileTags> AutoTagAsync(string fileName, string fileContent = null)
        {
            try
            {
                Log($"🏷️ Auto-tagging: {fileName}");

                var prompt = $@"
Analiza este archivo: ""{fileName}""

Genera metadata en formato JSON:
{{
  ""genre"": ""género literario o musical"",
  ""language"": ""idioma (español/inglés/etc)"",
  ""period"": ""época o período"",
  ""themes"": [""tema1"", ""tema2"", ""tema3""],
  ""audience"": ""público objetivo"",
  ""quality"": 8,
  ""category"": ""categoría principal""
}}

Sé específico y preciso.
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.3);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                var tags = ParseTags(response, fileName);
                Log($"✅ Tags generados: {tags.Genre}, {tags.Language}");
                
                return tags;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en AutoTagAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Organiza biblioteca completa automáticamente
        /// </summary>
        public async Task<OrganizationReport> OrganizeLibraryAsync(string sourceDir, string targetDir)
        {
            try
            {
                Log($"📚 Organizando biblioteca: {sourceDir}");

                var report = new OrganizationReport();
                var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    try
                    {
                        var fileName = Path.GetFileName(file);
                        var tags = await AutoTagAsync(fileName);

                        if (tags != null)
                        {
                            // Crear estructura de carpetas
                            var targetFolder = Path.Combine(
                                targetDir,
                                tags.Category,
                                tags.Language,
                                tags.Period
                            );

                            Directory.CreateDirectory(targetFolder);
                            
                            var targetPath = Path.Combine(targetFolder, fileName);
                            
                            // Mover archivo
                            if (!File.Exists(targetPath))
                            {
                                File.Move(file, targetPath);
                                report.FilesOrganized++;
                                Log($"✅ Organizado: {fileName} → {tags.Category}/{tags.Language}");
                            }
                            else
                            {
                                report.FilesDuplicated++;
                            }
                        }
                        else
                        {
                            report.FilesSkipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"⚠️ Error procesando {Path.GetFileName(file)}: {ex.Message}");
                        report.FilesWithErrors++;
                    }
                }

                Log($"📊 Organización completada: {report.FilesOrganized} archivos");
                return report;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en OrganizeLibraryAsync: {ex.Message}");
                return new OrganizationReport();
            }
        }

        /// <summary>
        /// Sugiere estructura de carpetas óptima
        /// </summary>
        public async Task<FolderStructure> SuggestFolderStructureAsync(List<string> fileNames)
        {
            try
            {
                var filesText = string.Join("\n", fileNames.Take(50));

                var prompt = $@"
Analiza estos archivos y sugiere una estructura de carpetas óptima:

{filesText}

Genera estructura en formato JSON:
{{
  ""categories"": [
    {{
      ""name"": ""Categoría"",
      ""subcategories"": [""Sub1"", ""Sub2""]
    }}
  ],
  ""reasoning"": ""Explicación breve""
}}
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.5);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                return ParseFolderStructure(response);
            }
            catch (Exception ex)
            {
                Log($"❌ Error en SuggestFolderStructureAsync: {ex.Message}");
                return null;
            }
        }

        private FileTags ParseTags(string jsonResponse, string fileName)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    var data = JsonSerializer.Deserialize<TagData>(jsonText);
                    
                    return new FileTags
                    {
                        FileName = fileName,
                        Genre = data.genre ?? "Unknown",
                        Language = data.language ?? "Unknown",
                        Period = data.period ?? "Unknown",
                        Themes = data.themes ?? new List<string>(),
                        Audience = data.audience ?? "General",
                        Quality = data.quality,
                        Category = data.category ?? "Other"
                    };
                }

                return CreateDefaultTags(fileName);
            }
            catch
            {
                return CreateDefaultTags(fileName);
            }
        }

        private FileTags CreateDefaultTags(string fileName)
        {
            return new FileTags
            {
                FileName = fileName,
                Genre = "Unknown",
                Language = "Unknown",
                Period = "Unknown",
                Category = "Other",
                Quality = 5
            };
        }

        private FolderStructure ParseFolderStructure(string jsonResponse)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    return JsonSerializer.Deserialize<FolderStructure>(jsonText);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }

        private class TagData
        {
            public string genre { get; set; }
            public string language { get; set; }
            public string period { get; set; }
            public List<string> themes { get; set; }
            public string audience { get; set; }
            public int quality { get; set; }
            public string category { get; set; }
        }
    }

    public class FileTags
    {
        public string FileName { get; set; }
        public string Genre { get; set; }
        public string Language { get; set; }
        public string Period { get; set; }
        public List<string> Themes { get; set; }
        public string Audience { get; set; }
        public int Quality { get; set; }
        public string Category { get; set; }
    }

    public class OrganizationReport
    {
        public int FilesOrganized { get; set; }
        public int FilesSkipped { get; set; }
        public int FilesDuplicated { get; set; }
        public int FilesWithErrors { get; set; }
    }

    public class FolderStructure
    {
        public List<CategoryInfo> categories { get; set; }
        public string reasoning { get; set; }
    }

    public class CategoryInfo
    {
        public string name { get; set; }
        public List<string> subcategories { get; set; }
    }
}
