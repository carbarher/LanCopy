using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;

namespace SlskDown
{
    // ═══════════════════════════════════════════════════════════════
    // TRANSCRIPCIÓN AUTOMÁTICA DE AUDIO (WHISPER AI)
    // ═══════════════════════════════════════════════════════════════
    
    public class AudioTranscriptionService : DisposableBase
    {
        private HttpClient httpClient;
        private Action<string> logAction;
        private string openAIKey;
        private ResultCache<string, string> transcriptionCache;
        
        public AudioTranscriptionService(Action<string> logger, string apiKey)
        {
            httpClient = HttpClientPool.OpenAI;
            logAction = logger;
            openAIKey = apiKey;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            transcriptionCache = new ResultCache<string, string>(TimeSpan.FromDays(30));
        }
        
        protected override void DisposeManagedResources()
        {
            transcriptionCache?.Clear();
        }
        
        public async Task<string> TranscribeAudiobook(string audioPath)
        {
            // Verificar caché primero
            if (transcriptionCache.TryGet(audioPath, out var cached))
            {
                logAction?.Invoke($"✅ Transcripción en caché: {Path.GetFileName(audioPath)}");
                return cached;
            }
            
            return await ApiRateLimiters.OpenAI.ExecuteAsync(async () =>
            {
                try
                {
                    logAction?.Invoke($"🎤 Transcribiendo audio: {Path.GetFileName(audioPath)}");
                
                // Preparar archivo para Whisper API
                using (var content = new MultipartFormDataContent())
                {
                    var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(audioPath));
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
                    content.Add(fileContent, "file", Path.GetFileName(audioPath));
                    content.Add(new StringContent("whisper-1"), "model");
                    content.Add(new StringContent("text"), "response_format");
                    
                    var response = await httpClient.PostAsync(
                        "https://api.openai.com/v1/audio/transcriptions",
                        content
                    );
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var transcription = await response.Content.ReadAsStringAsync();
                        
                        // Guardar en caché
                        transcriptionCache.Set(audioPath, transcription);
                        
                        // Guardar transcripción
                        var outputPath = Path.ChangeExtension(audioPath, ".txt");
                        await File.WriteAllTextAsync(outputPath, transcription);
                        
                        logAction?.Invoke($"✅ Transcripción completada: {outputPath}");
                        return transcription;
                    }
                }
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"❌ Error transcribiendo: {ex.Message}");
                }
                
                return null;
            });
        }
        
        public async Task TranscribeAllAudiobooks(string directory)
        {
            var audioFiles = Directory.GetFiles(directory, "*.mp3", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(directory, "*.m4a", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(directory, "*.wav", SearchOption.AllDirectories));
            
            int count = 0;
            foreach (var audioFile in audioFiles)
            {
                await TranscribeAudiobook(audioFile);
                count++;
                
                // Delay para respetar rate limits
                await Task.Delay(1000);
            }
            
            logAction?.Invoke($"✅ {count} audiobooks transcritos");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // TRADUCCIÓN EN TIEMPO REAL
    // ═══════════════════════════════════════════════════════════════
    
    public class TranslationService : DisposableBase
    {
        private HttpClient httpClient;
        private Action<string> logAction;
        private string deepLKey;
        private ResultCache<string, string> translationCache;
        
        public TranslationService(Action<string> logger, string apiKey)
        {
            httpClient = HttpClientPool.DeepL;
            logAction = logger;
            deepLKey = apiKey;
            translationCache = new ResultCache<string, string>(TimeSpan.FromDays(30));
        }
        
        protected override void DisposeManagedResources()
        {
            translationCache?.Clear();
        }
        
        public async Task<string> TranslateText(string text, string targetLanguage)
        {
            var cacheKey = $"{text}_{targetLanguage}";
            if (translationCache.TryGet(cacheKey, out var cached))
            {
                return cached;
            }
            
            return await ApiRateLimiters.DeepL.ExecuteAsync(async () =>
            {
                try
                {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", deepLKey),
                    new KeyValuePair<string, string>("text", text),
                    new KeyValuePair<string, string>("target_lang", targetLanguage.ToUpper())
                });
                
                var response = await httpClient.PostAsync("https://api-free.deepl.com/v2/translate", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(result);
                    var translation = data.GetProperty("translations")[0].GetProperty("text").GetString();
                    
                    translationCache.Set(cacheKey, translation);
                    return translation;
                }
            }
                catch (Exception ex)
                {
                    logAction?.Invoke($"❌ Error traduciendo: {ex.Message}");
                }
                
                return text;
            });
        }
        
        public async Task<string> TranslateBook(string bookPath, string targetLanguage)
        {
            try
            {
                logAction?.Invoke($"🌐 Traduciendo libro a {targetLanguage}: {Path.GetFileName(bookPath)}");
                
                // Leer contenido
                var content = await File.ReadAllTextAsync(bookPath);
                
                // Dividir en chunks (DeepL tiene límite de caracteres)
                var chunks = SplitIntoChunks(content, 5000);
                var translatedChunks = new List<string>();
                
                foreach (var chunk in chunks)
                {
                    var translated = await TranslateText(chunk, targetLanguage);
                    translatedChunks.Add(translated);
                    
                    // Delay para rate limiting
                    await Task.Delay(500);
                }
                
                // Guardar traducción
                var translatedContent = string.Join("\n\n", translatedChunks);
                var outputPath = Path.ChangeExtension(bookPath, $".{targetLanguage}.txt");
                await File.WriteAllTextAsync(outputPath, translatedContent);
                
                logAction?.Invoke($"✅ Traducción completada: {outputPath}");
                return outputPath;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error traduciendo libro: {ex.Message}");
                return null;
            }
        }
        
        private List<string> SplitIntoChunks(string text, int chunkSize)
        {
            var chunks = new List<string>();
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
            }
            return chunks;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // RESÚMENES AUTOMÁTICOS CON GPT-4
    // ═══════════════════════════════════════════════════════════════
    
    public class BookSummaryService : DisposableBase
    {
        private HttpClient httpClient;
        private Action<string> logAction;
        private string openAIKey;
        private ResultCache<string, BookSummary> summaryCache;
        
        public BookSummaryService(Action<string> logger, string apiKey)
        {
            httpClient = HttpClientPool.OpenAI;
            logAction = logger;
            openAIKey = apiKey;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            summaryCache = new ResultCache<string, BookSummary>(TimeSpan.FromDays(30));
        }
        
        protected override void DisposeManagedResources()
        {
            summaryCache?.Clear();
        }
        
        public async Task<BookSummary> GenerateSummary(string bookPath)
        {
            if (summaryCache.TryGet(bookPath, out var cached))
            {
                logAction?.Invoke($"✅ Resumen en caché: {Path.GetFileName(bookPath)}");
                return cached;
            }
            
            return await ApiRateLimiters.OpenAI.ExecuteAsync(async () =>
            {
                try
                {
                    logAction?.Invoke($"📝 Generando resumen: {Path.GetFileName(bookPath)}");
                
                // Leer contenido del libro
                var content = await File.ReadAllTextAsync(bookPath);
                
                // Limitar a primeros 10,000 caracteres para el prompt
                var excerpt = content.Substring(0, Math.Min(10000, content.Length));
                
                var request = new
                {
                    model = "gpt-4",
                    messages = new[]
                    {
                        new { role = "system", content = "Eres un experto en análisis literario. Genera resúmenes concisos y útiles de libros." },
                        new { role = "user", content = $"Por favor, genera un resumen estructurado de este libro:\n\n{excerpt}\n\nIncluye:\n1. Resumen breve (2-3 párrafos)\n2. Temas principales\n3. Personajes clave\n4. Conclusiones" }
                    },
                    max_tokens = 1000
                };
                
                var response = await httpClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
                );
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(result);
                    var summaryText = data.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                    
                    var summary = new BookSummary
                    {
                        BookPath = bookPath,
                        GeneratedDate = DateTime.Now,
                        Text = summaryText
                    };
                    
                    summaryCache.Set(bookPath, summary);
                    
                    // Guardar resumen
                    var summaryPath = Path.ChangeExtension(bookPath, ".summary.txt");
                    await File.WriteAllTextAsync(summaryPath, summaryText);
                    
                    logAction?.Invoke($"✅ Resumen generado: {summaryPath}");
                    return summary;
                }
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"❌ Error generando resumen: {ex.Message}");
                }
                
                return null;
            });
        }
    }
    
    public class BookSummary
    {
        public string BookPath { get; set; }
        public DateTime GeneratedDate { get; set; }
        public string Text { get; set; }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // ANÁLISIS DE SENTIMIENTO DE LIBROS
    // ═══════════════════════════════════════════════════════════════
    
    public class BookSentimentAnalyzer : DisposableBase
    {
        private HttpClient httpClient;
        private Action<string> logAction;
        private string openAIKey;
        private ResultCache<string, BookSentiment> sentimentCache;
        
        public BookSentimentAnalyzer(Action<string> logger, string apiKey)
        {
            httpClient = HttpClientPool.OpenAI;
            logAction = logger;
            openAIKey = apiKey;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            sentimentCache = new ResultCache<string, BookSentiment>(TimeSpan.FromDays(30));
        }
        
        protected override void DisposeManagedResources()
        {
            sentimentCache?.Clear();
        }
        
        public async Task<BookSentiment> AnalyzeSentiment(string bookPath)
        {
            try
            {
                logAction?.Invoke($"😊 Analizando sentimiento: {Path.GetFileName(bookPath)}");
                
                var content = await File.ReadAllTextAsync(bookPath);
                var excerpt = content.Substring(0, Math.Min(5000, content.Length));
                
                var request = new
                {
                    model = "gpt-4",
                    messages = new[]
                    {
                        new { role = "system", content = "Analiza el tono emocional y mood de textos literarios." },
                        new { role = "user", content = $"Analiza el tono emocional de este texto y clasifícalo:\n\n{excerpt}\n\nProvee:\n1. Mood general (alegre, triste, tenso, reflexivo, etc.)\n2. Tono (optimista, pesimista, neutral)\n3. Intensidad emocional (1-10)\n4. Temas emocionales principales" }
                    },
                    max_tokens = 500
                };
                
                var response = await httpClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
                );
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(result);
                    var analysis = data.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                    
                    var sentiment = new BookSentiment
                    {
                        BookPath = bookPath,
                        Mood = ExtractMood(analysis),
                        Tone = ExtractTone(analysis),
                        EmotionalIntensity = ExtractIntensity(analysis),
                        Analysis = analysis
                    };
                    
                    logAction?.Invoke($"✅ Sentimiento: {sentiment.Mood} ({sentiment.Tone})");
                    return sentiment;
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error analizando sentimiento: {ex.Message}");
            }
            
            return null;
        }
        
        private string ExtractMood(string analysis)
        {
            // Extracción simple de mood
            if (analysis.Contains("alegre", StringComparison.OrdinalIgnoreCase)) return "Alegre";
            if (analysis.Contains("triste", StringComparison.OrdinalIgnoreCase)) return "Triste";
            if (analysis.Contains("tenso", StringComparison.OrdinalIgnoreCase)) return "Tenso";
            if (analysis.Contains("reflexivo", StringComparison.OrdinalIgnoreCase)) return "Reflexivo";
            return "Neutral";
        }
        
        private string ExtractTone(string analysis)
        {
            if (analysis.Contains("optimista", StringComparison.OrdinalIgnoreCase)) return "Optimista";
            if (analysis.Contains("pesimista", StringComparison.OrdinalIgnoreCase)) return "Pesimista";
            return "Neutral";
        }
        
        private int ExtractIntensity(string analysis)
        {
            // Buscar número entre 1-10
            for (int i = 1; i <= 10; i++)
            {
                if (analysis.Contains(i.ToString()))
                {
                    return i;
                }
            }
            return 5;
        }
    }
    
    public class BookSentiment
    {
        public string BookPath { get; set; }
        public string Mood { get; set; }
        public string Tone { get; set; }
        public int EmotionalIntensity { get; set; }
        public string Analysis { get; set; }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // RECOMENDACIONES POR CONTEXTO
    // ═══════════════════════════════════════════════════════════════
    
    public class ContextualRecommendationEngine
    {
        private Action<string> logAction;
        
        public ContextualRecommendationEngine(Action<string> logger)
        {
            logAction = logger;
        }
        
        public List<BookMetadata> GetRecommendationsByContext(
            List<BookMetadata> library,
            DateTime time,
            string location,
            string mood)
        {
            var recommendations = new List<BookMetadata>();
            
            // Filtrar por hora del día
            if (time.Hour >= 6 && time.Hour < 12)
            {
                // Mañana: Libros motivacionales, técnicos
                recommendations = library.Where(b =>
                    b.Genre?.Contains("Self-Help") == true ||
                    b.Genre?.Contains("Business") == true ||
                    b.Genre?.Contains("Technical") == true
                ).ToList();
                
                logAction?.Invoke("☀️ Recomendaciones matutinas: Motivación y aprendizaje");
            }
            else if (time.Hour >= 12 && time.Hour < 18)
            {
                // Tarde: Ficción ligera, biografías
                recommendations = library.Where(b =>
                    b.Genre?.Contains("Fiction") == true ||
                    b.Genre?.Contains("Biography") == true
                ).ToList();
                
                logAction?.Invoke("🌤️ Recomendaciones vespertinas: Ficción y biografías");
            }
            else if (time.Hour >= 18 && time.Hour < 22)
            {
                // Noche: Novelas, misterio
                recommendations = library.Where(b =>
                    b.Genre?.Contains("Mystery") == true ||
                    b.Genre?.Contains("Thriller") == true ||
                    b.Genre?.Contains("Romance") == true
                ).ToList();
                
                logAction?.Invoke("🌙 Recomendaciones nocturnas: Misterio y romance");
            }
            else
            {
                // Madrugada: Filosofía, poesía
                recommendations = library.Where(b =>
                    b.Genre?.Contains("Philosophy") == true ||
                    b.Genre?.Contains("Poetry") == true
                ).ToList();
                
                logAction?.Invoke("🌃 Recomendaciones de madrugada: Filosofía y poesía");
            }
            
            // Filtrar por mood
            if (!string.IsNullOrEmpty(mood))
            {
                recommendations = FilterByMood(recommendations, mood);
            }
            
            // Filtrar por ubicación
            if (!string.IsNullOrEmpty(location))
            {
                recommendations = FilterByLocation(recommendations, location);
            }
            
            return recommendations.Take(10).ToList();
        }
        
        private List<BookMetadata> FilterByMood(List<BookMetadata> books, string mood)
        {
            switch (mood.ToLowerInvariant())
            {
                case "happy":
                case "alegre":
                    return books.Where(b => b.Rating >= 4).ToList();
                    
                case "sad":
                case "triste":
                    return books.Where(b => b.Genre?.Contains("Drama") == true).ToList();
                    
                case "stressed":
                case "estresado":
                    return books.Where(b => b.Genre?.Contains("Comedy") == true).ToList();
                    
                case "curious":
                case "curioso":
                    return books.Where(b => b.Genre?.Contains("Science") == true).ToList();
                    
                default:
                    return books;
            }
        }
        
        private List<BookMetadata> FilterByLocation(List<BookMetadata> books, string location)
        {
            switch (location.ToLowerInvariant())
            {
                case "commute":
                case "transporte":
                    // Libros cortos o por capítulos
                    return books.Where(b => b.Pages < 300).ToList();
                    
                case "home":
                case "casa":
                    // Cualquier libro
                    return books;
                    
                case "work":
                case "trabajo":
                    // Libros técnicos o de desarrollo personal
                    return books.Where(b =>
                        b.Genre?.Contains("Business") == true ||
                        b.Genre?.Contains("Technical") == true
                    ).ToList();
                    
                default:
                    return books;
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // INTEGRACIÓN CON E-READERS (KINDLE, KOBO)
    // ═══════════════════════════════════════════════════════════════
    
    public class EReaderIntegration : DisposableBase
    {
        private Action<string> logAction;
        
        public EReaderIntegration(Action<string> logger)
        {
            logAction = logger;
        }
        
        protected override void DisposeManagedResources() { }
        
        public async Task<bool> DetectKindle()
        {
            // Detectar Kindle conectado por USB
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.IsReady && drive.DriveType == DriveType.Removable)
                {
                    var kindleMarker = Path.Combine(drive.RootDirectory.FullName, "system", "version.txt");
                    if (File.Exists(kindleMarker))
                    {
                        logAction?.Invoke($"📱 Kindle detectado: {drive.Name}");
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        public async Task SyncToKindle(string bookPath)
        {
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Removable)
                    {
                        var kindleMarker = Path.Combine(drive.RootDirectory.FullName, "system", "version.txt");
                        if (File.Exists(kindleMarker))
                        {
                            var documentsPath = Path.Combine(drive.RootDirectory.FullName, "documents");
                            var destPath = Path.Combine(documentsPath, Path.GetFileName(bookPath));
                            
                            File.Copy(bookPath, destPath, overwrite: true);
                            
                            logAction?.Invoke($"✅ Libro sincronizado a Kindle: {Path.GetFileName(bookPath)}");
                            return;
                        }
                    }
                }
                
                logAction?.Invoke("❌ Kindle no detectado");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error sincronizando a Kindle: {ex.Message}");
            }
        }
        
        public async Task SyncLibraryToKindle(List<string> bookPaths)
        {
            int synced = 0;
            foreach (var bookPath in bookPaths)
            {
                await SyncToKindle(bookPath);
                synced++;
            }
            
            logAction?.Invoke($"✅ {synced} libros sincronizados a Kindle");
        }
    }
}
