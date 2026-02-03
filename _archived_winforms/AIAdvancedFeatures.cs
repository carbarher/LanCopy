using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown
{
    // ═══════════════════════════════════════════════════════════════
    // PREDICCIÓN DE CALIDAD DE ARCHIVOS CON ML
    // ═══════════════════════════════════════════════════════════════
    
    public class FileQualityFeatures
    {
        public long FileSize { get; set; }
        public string Extension { get; set; }
        public int Bitrate { get; set; }
        public bool HasMetadata { get; set; }
        public double UploaderReputation { get; set; }
        public int UploaderSharedFiles { get; set; }
        public double ExpectedSizeRatio { get; set; }
    }
    
    public class QualityPrediction
    {
        public double Score { get; set; } // 0-100
        public string Quality { get; set; } // Excellent, Good, Medium, Low
        public string Recommendation { get; set; }
        public Dictionary<string, double> Factors { get; set; }
    }
    
    public class FileQualityPredictor
    {
        private Action<string> logAction;
        private Dictionary<string, double> historicalQuality = new Dictionary<string, double>();
        
        public FileQualityPredictor(Action<string> logger)
        {
            logAction = logger;
        }
        
        public QualityPrediction PredictQuality(FileQualityFeatures features)
        {
            var factors = new Dictionary<string, double>();
            double totalScore = 0;
            
            // Factor 1: Tamaño del archivo (20%)
            double sizeScore = CalculateSizeScore(features.FileSize, features.Extension);
            factors["Size"] = sizeScore;
            totalScore += sizeScore * 0.20;
            
            // Factor 2: Bitrate para audio (25%)
            double bitrateScore = CalculateBitrateScore(features.Bitrate, features.Extension);
            factors["Bitrate"] = bitrateScore;
            totalScore += bitrateScore * 0.25;
            
            // Factor 3: Metadata completitud (15%)
            double metadataScore = features.HasMetadata ? 100 : 30;
            factors["Metadata"] = metadataScore;
            totalScore += metadataScore * 0.15;
            
            // Factor 4: Reputación del uploader (25%)
            double reputationScore = Math.Min(100, features.UploaderReputation * 20);
            factors["Reputation"] = reputationScore;
            totalScore += reputationScore * 0.25;
            
            // Factor 5: Cantidad de archivos compartidos (10%)
            double sharesScore = Math.Min(100, features.UploaderSharedFiles / 100.0);
            factors["Shares"] = sharesScore;
            totalScore += sharesScore * 0.10;
            
            // Factor 6: Ratio tamaño esperado (5%)
            double ratioScore = CalculateRatioScore(features.ExpectedSizeRatio);
            factors["Ratio"] = ratioScore;
            totalScore += ratioScore * 0.05;
            
            var prediction = new QualityPrediction
            {
                Score = totalScore,
                Factors = factors
            };
            
            if (totalScore >= 90)
            {
                prediction.Quality = "Excellent";
                prediction.Recommendation = "⭐ Excelente calidad - Descargar inmediatamente";
            }
            else if (totalScore >= 70)
            {
                prediction.Quality = "Good";
                prediction.Recommendation = "✅ Buena calidad - Recomendado";
            }
            else if (totalScore >= 50)
            {
                prediction.Quality = "Medium";
                prediction.Recommendation = "⚠️ Calidad media - Considerar alternativas";
            }
            else
            {
                prediction.Quality = "Low";
                prediction.Recommendation = "❌ Baja calidad - Buscar mejor fuente";
            }
            
            return prediction;
        }
        
        private double CalculateSizeScore(long size, string extension)
        {
            var expectedSizes = new Dictionary<string, (long min, long max)>
            {
                { ".mp3", (3 * 1024 * 1024, 15 * 1024 * 1024) },
                { ".flac", (20 * 1024 * 1024, 80 * 1024 * 1024) },
                { ".epub", (500 * 1024, 10 * 1024 * 1024) },
                { ".pdf", (1 * 1024 * 1024, 50 * 1024 * 1024) }
            };
            
            if (expectedSizes.TryGetValue(extension.ToLowerInvariant(), out var range))
            {
                if (size >= range.min && size <= range.max)
                    return 100;
                else if (size < range.min)
                    return 40;
                else
                    return 70;
            }
            
            return 60;
        }
        
        private double CalculateBitrateScore(int bitrate, string extension)
        {
            if (!extension.ToLowerInvariant().Contains("mp3") && !extension.ToLowerInvariant().Contains("m4a"))
                return 80;
            
            if (bitrate >= 320) return 100;
            if (bitrate >= 256) return 90;
            if (bitrate >= 192) return 75;
            if (bitrate >= 128) return 60;
            return 40;
        }
        
        private double CalculateRatioScore(double ratio)
        {
            if (ratio >= 0.9 && ratio <= 1.1) return 100;
            if (ratio >= 0.7 && ratio <= 1.3) return 80;
            return 50;
        }
        
        public void RecordActualQuality(string fileId, double actualQuality)
        {
            historicalQuality[fileId] = actualQuality;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // ANÁLISIS DE SENTIMIENTO EN CHAT
    // ═══════════════════════════════════════════════════════════════
    
    public enum SentimentType
    {
        Normal,
        Spam,
        Toxic,
        Promotion,
        Helpful
    }
    
    public class SentimentResult
    {
        public SentimentType Type { get; set; }
        public double Confidence { get; set; }
        public bool ShouldModerate { get; set; }
        public string Reason { get; set; }
    }
    
    public class ChatSentimentAnalyzer
    {
        private Action<string> logAction;
        private HashSet<string> spamKeywords;
        private HashSet<string> toxicKeywords;
        private HashSet<string> promotionKeywords;
        
        public ChatSentimentAnalyzer(Action<string> logger)
        {
            logAction = logger;
            
            spamKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "click here", "free download", "limited time", "act now", "buy now"
            };
            
            toxicKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "idiot", "stupid", "moron", "hate", "kill"
            };
            
            promotionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "check out my", "visit my site", "subscribe to", "follow me"
            };
        }
        
        public SentimentResult AnalyzeMessage(string message)
        {
            var result = new SentimentResult
            {
                Type = SentimentType.Normal,
                Confidence = 0,
                ShouldModerate = false
            };
            
            var lowerMessage = message.ToLowerInvariant();
            
            // Detectar spam
            int spamCount = spamKeywords.Count(k => lowerMessage.Contains(k));
            if (spamCount >= 2)
            {
                result.Type = SentimentType.Spam;
                result.Confidence = Math.Min(100, spamCount * 40);
                result.ShouldModerate = true;
                result.Reason = "Múltiples palabras clave de spam detectadas";
                return result;
            }
            
            // Detectar toxicidad
            int toxicCount = toxicKeywords.Count(k => lowerMessage.Contains(k));
            if (toxicCount > 0)
            {
                result.Type = SentimentType.Toxic;
                result.Confidence = Math.Min(100, toxicCount * 50);
                result.ShouldModerate = toxicCount >= 2;
                result.Reason = "Lenguaje tóxico detectado";
                return result;
            }
            
            // Detectar promoción
            int promoCount = promotionKeywords.Count(k => lowerMessage.Contains(k));
            if (promoCount > 0)
            {
                result.Type = SentimentType.Promotion;
                result.Confidence = Math.Min(100, promoCount * 60);
                result.ShouldModerate = false;
                result.Reason = "Contenido promocional";
                return result;
            }
            
            // Detectar mensajes útiles
            if (lowerMessage.Contains("thanks") || lowerMessage.Contains("help") || 
                lowerMessage.Contains("gracias") || lowerMessage.Contains("ayuda"))
            {
                result.Type = SentimentType.Helpful;
                result.Confidence = 70;
                result.Reason = "Mensaje útil o agradecimiento";
            }
            
            return result;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // GENERADOR DE PLAYLISTS CON IA
    // ═══════════════════════════════════════════════════════════════
    
    public class MusicFile
    {
        public string Path { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Genre { get; set; }
        public int BPM { get; set; }
        public string Mood { get; set; }
        public int Year { get; set; }
    }
    
    public class Playlist
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<MusicFile> Files { get; set; } = new List<MusicFile>();
        public string Icon { get; set; }
    }
    
    public class AIPlaylistGenerator
    {
        private Action<string> logAction;
        
        public AIPlaylistGenerator(Action<string> logger)
        {
            logAction = logger;
        }
        
        public List<Playlist> GenerateSmartPlaylists(List<MusicFile> files)
        {
            var playlists = new List<Playlist>();
            
            // Playlist 1: Workout (BPM alto)
            var workout = new Playlist
            {
                Name = "💪 Workout",
                Description = "Música energética para ejercicio",
                Icon = "💪",
                Files = files.Where(f => f.BPM >= 120 && f.BPM <= 160).ToList()
            };
            if (workout.Files.Count > 0) playlists.Add(workout);
            
            // Playlist 2: Chill (BPM bajo)
            var chill = new Playlist
            {
                Name = "😌 Chill",
                Description = "Música relajante",
                Icon = "😌",
                Files = files.Where(f => f.BPM > 0 && f.BPM < 100).ToList()
            };
            if (chill.Files.Count > 0) playlists.Add(chill);
            
            // Playlist 3: Focus (géneros específicos)
            var focus = new Playlist
            {
                Name = "🎯 Focus",
                Description = "Música para concentración",
                Icon = "🎯",
                Files = files.Where(f => 
                    f.Genre?.Contains("Classical") == true || 
                    f.Genre?.Contains("Ambient") == true ||
                    f.Genre?.Contains("Jazz") == true
                ).ToList()
            };
            if (focus.Files.Count > 0) playlists.Add(focus);
            
            // Playlist 4: Oldies (por año)
            var oldies = new Playlist
            {
                Name = "📻 Oldies",
                Description = "Clásicos del pasado",
                Icon = "📻",
                Files = files.Where(f => f.Year > 0 && f.Year < 2000).ToList()
            };
            if (oldies.Files.Count > 0) playlists.Add(oldies);
            
            // Playlist 5: Party (géneros bailables)
            var party = new Playlist
            {
                Name = "🎉 Party",
                Description = "Música para fiestas",
                Icon = "🎉",
                Files = files.Where(f => 
                    f.Genre?.Contains("Pop") == true || 
                    f.Genre?.Contains("Dance") == true ||
                    f.Genre?.Contains("Electronic") == true
                ).ToList()
            };
            if (party.Files.Count > 0) playlists.Add(party);
            
            logAction?.Invoke($"🎵 Generadas {playlists.Count} playlists inteligentes");
            
            return playlists;
        }
    }
}
