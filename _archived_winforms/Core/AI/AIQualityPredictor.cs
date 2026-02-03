using System;
using System.Text.Json;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core.AI
{
    /// <summary>
    /// Predictor de calidad de archivos y usuarios usando IA
    /// </summary>
    public class AIQualityPredictor
    {
        private readonly OllamaClient ollama;
        
        public event Action<string> OnLog;

        public AIQualityPredictor(OllamaClient ollama)
        {
            this.ollama = ollama;
        }

        /// <summary>
        /// Predice la calidad de un resultado de búsqueda
        /// </summary>
        public async Task<QualityScore> PredictQualityAsync(SearchResult result)
        {
            try
            {
                Log($"🎯 Analizando calidad: {result.FileName}");

                var features = new
                {
                    fileName = result.FileName,
                    fileSize = result.FileSize,
                    bitrate = result.Bitrate,
                    username = result.Username,
                    uploadSpeed = result.UploadSpeed,
                    queueLength = result.QueueLength,
                    extension = System.IO.Path.GetExtension(result.FileName)
                };

                var prompt = $@"
Analiza la calidad de este archivo P2P:

Nombre: {features.fileName}
Tamaño: {features.fileSize / (1024 * 1024):F1} MB
Bitrate: {features.bitrate} kbps
Usuario: {features.username}
Velocidad: {features.uploadSpeed / 1024:F1} KB/s
Cola: {features.queueLength} archivos

Evalúa (escala 1-10) en formato JSON:
{{
  ""fileQuality"": 8,
  ""userReliability"": 7,
  ""downloadSuccess"": 9,
  ""overallScore"": 8.0,
  ""recommendation"": ""descargar"",
  ""reasoning"": ""Explicación breve"",
  ""warnings"": [""advertencia1""]
}}

Considera:
- Tamaño apropiado para el tipo de archivo
- Bitrate adecuado
- Reputación del usuario
- Probabilidad de descarga exitosa
";

                var response = await openAI.GetCompletionAsync(prompt, temperature: 0.3);
                
                if (string.IsNullOrEmpty(response))
                    return CreateDefaultScore();

                var score = ParseQualityScore(response);
                Log($"✅ Score: {score.OverallScore:F1}/10 - {score.Recommendation}");
                
                return score;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en PredictQualityAsync: {ex.Message}");
                return CreateDefaultScore();
            }
        }

        /// <summary>
        /// Compara múltiples resultados y rankea por calidad
        /// </summary>
        public async Task<RankedResults> RankResultsByQualityAsync(SearchResult[] results)
        {
            try
            {
                Log($"📊 Rankeando {results.Length} resultados");

                var ranked = new RankedResults();

                foreach (var result in results)
                {
                    var score = await PredictQualityAsync(result);
                    result.AIQualityScore = score.OverallScore;
                    result.AIRecommendation = score.Recommendation;
                    
                    if (score.OverallScore >= 8.0)
                        ranked.Excellent.Add(result);
                    else if (score.OverallScore >= 6.0)
                        ranked.Good.Add(result);
                    else if (score.OverallScore >= 4.0)
                        ranked.Average.Add(result);
                    else
                        ranked.Poor.Add(result);
                }

                Log($"✅ Ranking: {ranked.Excellent.Count} excelentes, {ranked.Good.Count} buenos");
                return ranked;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en RankResultsByQualityAsync: {ex.Message}");
                return new RankedResults();
            }
        }

        /// <summary>
        /// Analiza la reputación de un usuario
        /// </summary>
        public async Task<UserReputation> AnalyzeUserReputationAsync(string username, int filesShared, int uploadSpeed)
        {
            try
            {
                var prompt = $@"
Analiza la reputación de este usuario P2P:

Usuario: {username}
Archivos compartidos: {filesShared}
Velocidad de subida: {uploadSpeed / 1024:F1} KB/s

Evalúa en formato JSON:
{{
  ""trustScore"": 7,
  ""reliability"": ""alta"",
  ""recommendation"": ""confiable"",
  ""reasoning"": ""Explicación""
}}
";

                var response = await ollama.GetCompletionAsync(prompt, temperature: 0.3);
                
                if (string.IsNullOrEmpty(response))
                    return null;

                return ParseUserReputation(response);
            }
            catch (Exception ex)
            {
                Log($"❌ Error en AnalyzeUserReputationAsync: {ex.Message}");
                return null;
            }
        }

        private QualityScore ParseQualityScore(string jsonResponse)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    var data = JsonSerializer.Deserialize<QualityScoreData>(jsonText);
                    
                    return new QualityScore
                    {
                        FileQuality = data.fileQuality,
                        UserReliability = data.userReliability,
                        DownloadSuccess = data.downloadSuccess,
                        OverallScore = data.overallScore,
                        Recommendation = data.recommendation ?? "revisar",
                        Reasoning = data.reasoning ?? "",
                        Warnings = data.warnings ?? new string[0]
                    };
                }

                return CreateDefaultScore();
            }
            catch
            {
                return CreateDefaultScore();
            }
        }

        private UserReputation ParseUserReputation(string jsonResponse)
        {
            try
            {
                var startIndex = jsonResponse.IndexOf('{');
                var endIndex = jsonResponse.LastIndexOf('}');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonText = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    return JsonSerializer.Deserialize<UserReputation>(jsonText);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private QualityScore CreateDefaultScore()
        {
            return new QualityScore
            {
                FileQuality = 5,
                UserReliability = 5,
                DownloadSuccess = 5,
                OverallScore = 5.0,
                Recommendation = "revisar",
                Reasoning = "Análisis no disponible"
            };
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }

        private class QualityScoreData
        {
            public int fileQuality { get; set; }
            public int userReliability { get; set; }
            public int downloadSuccess { get; set; }
            public double overallScore { get; set; }
            public string recommendation { get; set; }
            public string reasoning { get; set; }
            public string[] warnings { get; set; }
        }
    }

    public class QualityScore
    {
        public int FileQuality { get; set; }
        public int UserReliability { get; set; }
        public int DownloadSuccess { get; set; }
        public double OverallScore { get; set; }
        public string Recommendation { get; set; }
        public string Reasoning { get; set; }
        public string[] Warnings { get; set; }
    }

    public class RankedResults
    {
        public System.Collections.Generic.List<SearchResult> Excellent { get; set; } = new();
        public System.Collections.Generic.List<SearchResult> Good { get; set; } = new();
        public System.Collections.Generic.List<SearchResult> Average { get; set; } = new();
        public System.Collections.Generic.List<SearchResult> Poor { get; set; } = new();
    }

    public class UserReputation
    {
        public int trustScore { get; set; }
        public string reliability { get; set; }
        public string recommendation { get; set; }
        public string reasoning { get; set; }
    }
}
