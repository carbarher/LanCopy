using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core.Neural
{
    /// <summary>
    /// Red Neuronal simplificada para clasificación automática de contenido
    /// </summary>
    public class ContentClassifier
    {
        private readonly Dictionary<string, float[]> _embeddings;
        private readonly Dictionary<string, GenrePattern> _genrePatterns;
        private readonly Dictionary<string, QualityPattern> _qualityPatterns;
        private readonly object _lockObject = new object();

        public ContentClassifier()
        {
            _embeddings = new Dictionary<string, float[]>();
            _genrePatterns = new Dictionary<string, GenrePattern>();
            _qualityPatterns = new Dictionary<string, QualityPattern>();
            InitializePatterns();
        }

        /// <summary>
        /// Clasifica un archivo automáticamente
        /// </summary>
        public async Task<ContentClassification> ClassifyFile(AutoSearchFileResult file)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    var embedding = GenerateEmbedding(file);
                    var genre = ClassifyGenre(embedding, file);
                    var quality = ClassifyQuality(embedding, file);
                    var confidence = CalculateConfidence(embedding, genre, quality);

                    return new ContentClassification
                    {
                        Filename = file.Filename,
                        PredictedGenre = genre,
                        PredictedQuality = quality,
                        ConfidenceScore = confidence,
                        Embedding = embedding,
                        Features = ExtractFeatures(file)
                    };
                }
            });
        }

        /// <summary>
        /// Detecta duplicados usando embeddings
        /// </summary>
        public async Task<List<DuplicateGroup>> FindDuplicates(List<AutoSearchFileResult> files)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    var embeddings = new List<(AutoSearchFileResult file, float[] embedding)>();
                    
                    // Generar embeddings para todos los archivos
                    foreach (var file in files)
                    {
                        var embedding = GenerateEmbedding(file);
                        embeddings.Add((file, embedding));
                    }

                    // Agrupar por similitud
                    var duplicateGroups = new List<DuplicateGroup>();
                    var processed = new HashSet<int>();

                    for (int i = 0; i < embeddings.Count; i++)
                    {
                        if (processed.Contains(i)) continue;

                        var group = new DuplicateGroup
                        {
                            Representative = embeddings[i].file,
                            Duplicates = new List<AutoSearchFileResult>()
                        };

                        for (int j = i + 1; j < embeddings.Count; j++)
                        {
                            if (processed.Contains(j)) continue;

                            var similarity = CalculateCosineSimilarity(
                                embeddings[i].embedding, 
                                embeddings[j].embedding);

                            if (similarity > 0.85f) // Umbral de similitud
                            {
                                group.Duplicates.Add(embeddings[j].file);
                                processed.Add(j);
                            }
                        }

                        if (group.Duplicates.Any())
                        {
                            duplicateGroups.Add(group);
                        }
                        processed.Add(i);
                    }

                    return duplicateGroups.OrderByDescending(g => g.Duplicates.Count).ToList();
                }
            });
        }

        /// <summary>
        /// Genera embedding vectorial para un archivo
        /// </summary>
        private float[] GenerateEmbedding(AutoSearchFileResult file)
        {
            var features = ExtractFeatures(file);
            var embedding = new float[128]; // Vector de 128 dimensiones
            
            // Características del nombre de archivo
            var nameFeatures = ExtractNameFeatures(file.Filename);
            for (int i = 0; i < Math.Min(nameFeatures.Length, 64); i++)
            {
                embedding[i] = nameFeatures[i];
            }

            // Características del autor
            var authorFeatures = ExtractAuthorFeatures(file.Author);
            for (int i = 0; i < Math.Min(authorFeatures.Length, 32); i++)
            {
                embedding[64 + i] = authorFeatures[i];
            }

            // Características de tamaño y bitrate
            embedding[96] = NormalizeSize(file.Size);
            embedding[97] = NormalizeBitrate(file.BitRate);
            embedding[98] = features.Length > 0 ? 1.0f : 0.0f; // Tiene características
            embedding[99] = file.Filename.Contains("flac", StringComparison.OrdinalIgnoreCase) ? 1.0f : 0.0f;
            embedding[100] = file.Filename.Contains("mp3", StringComparison.OrdinalIgnoreCase) ? 1.0f : 0.0f;
            embedding[101] = file.Filename.Contains("320", StringComparison.OrdinalIgnoreCase) ? 1.0f : 0.0f;
            embedding[102] = file.Filename.Contains("lossless", StringComparison.OrdinalIgnoreCase) ? 1.0f : 0.0f;
            
            // Características adicionales para completar 128 dimensiones
            for (int i = 103; i < 128; i++)
            {
                embedding[i] = (float)new Random().NextDouble() * 0.1f; // Pequeño ruido
            }

            // Normalizar vector
            var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
            if (magnitude > 0)
            {
                for (int i = 0; i < embedding.Length; i++)
                {
                    embedding[i] /= magnitude;
                }
            }

            return embedding;
        }

        /// <summary>
        /// Extrae características del nombre de archivo
        /// </summary>
        private float[] ExtractNameFeatures(string filename)
        {
            var features = new float[64];
            var cleanName = System.IO.Path.GetFileNameWithoutExtension(filename).ToLower();
            
            // Análisis de palabras clave
            var keywords = new Dictionary<string, int>
            {
                ["remix"] = 0, ["live"] = 0, ["acoustic"] = 0, ["studio"] = 0,
                ["demo"] = 0, ["bonus"] = 0, ["track"] = 0, ["explicit"] = 0,
                ["radio"] = 0, ["extended"] = 0, ["instrumental"] = 0, ["edit"] = 0
            };

            foreach (var keyword in keywords.Keys.ToList())
            {
                keywords[keyword] = cleanName.Split(' ').Count(word => word.Contains(keyword));
            }

            // Codificar características
            int index = 0;
            foreach (var count in keywords.Values)
            {
                features[index++] = Math.Min(count / 5.0f, 1.0f); // Normalizar a 0-1
            }

            // Características de longitud y estructura
            features[index++] = Math.Min(cleanName.Length / 100.0f, 1.0f);
            features[index++] = cleanName.Count(char.IsDigit) / (float)cleanName.Length;
            features[index++] = cleanName.Count(char.IsLetter) / (float)cleanName.Length;

            return features;
        }

        /// <summary>
        /// Extrae características del autor
        /// </summary>
        private float[] ExtractAuthorFeatures(string author)
        {
            var features = new float[32];
            var cleanAuthor = author.ToLower();
            
            // Análisis de patrones de nombre
            features[0] = cleanAuthor.Contains("the ") ? 1.0f : 0.0f;
            features[1] = cleanAuthor.Contains("dj") ? 1.0f : 0.0f;
            features[2] = cleanAuthor.Contains("mc") ? 1.0f : 0.0f;
            features[3] = cleanAuthor.Count(char.IsDigit) > 0 ? 1.0f : 0.0f;
            features[4] = Math.Min(cleanAuthor.Length / 50.0f, 1.0f);
            
            // Espacio para características futuras
            for (int i = 5; i < 32; i++)
            {
                features[i] = 0.0f;
            }

            return features;
        }

        /// <summary>
        /// Extrae características adicionales
        /// </summary>
        private Dictionary<string, float> ExtractFeatures(AutoSearchFileResult file)
        {
            var features = new Dictionary<string, float>();
            
            features["size_mb"] = file.Size / (1024.0f * 1024.0f);
            features["bitrate_kbps"] = file.BitRate;
            features["filename_length"] = file.Filename.Length;
            features["author_length"] = file.Author.Length;
            features["has_numbers"] = file.Filename.Any(char.IsDigit) ? 1.0f : 0.0f;
            features["is_lossless"] = file.Filename.Contains("flac", StringComparison.OrdinalIgnoreCase) ? 1.0f : 0.0f;
            
            return features;
        }

        /// <summary>
        /// Clasifica el género musical
        /// </summary>
        private string ClassifyGenre(float[] embedding, AutoSearchFileResult file)
        {
            var filename = file.Filename.ToLower();
            var scores = new Dictionary<string, float>();

            foreach (var pattern in _genrePatterns)
            {
                var score = CalculateGenreScore(embedding, filename, pattern.Value);
                scores[pattern.Key] = score;
            }

            return scores.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        /// <summary>
        /// Clasifica la calidad del audio
        /// </summary>
        private string ClassifyQuality(float[] embedding, AutoSearchFileResult file)
        {
            var filename = file.Filename.ToLower();
            var scores = new Dictionary<string, float>();

            foreach (var pattern in _qualityPatterns)
            {
                var score = CalculateQualityScore(embedding, filename, file.BitRate, pattern.Value);
                scores[pattern.Key] = score;
            }

            return scores.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        /// <summary>
        /// Calcula puntuación de género
        /// </summary>
        private float CalculateGenreScore(float[] embedding, string filename, GenrePattern pattern)
        {
            float score = 0.0f;
            
            // Similitud con embedding del patrón
            if (pattern.Embedding != null)
            {
                score += CalculateCosineSimilarity(embedding, pattern.Embedding) * 0.5f;
            }

            // Coincidencia de palabras clave
            foreach (var keyword in pattern.Keywords)
            {
                if (filename.Contains(keyword))
                {
                    score += 0.1f;
                }
            }

            return Math.Min(score, 1.0f);
        }

        /// <summary>
        /// Calcula puntuación de calidad
        /// </summary>
        private float CalculateQualityScore(float[] embedding, string filename, int bitrate, QualityPattern pattern)
        {
            float score = 0.0f;
            
            // Basado en bitrate
            if (bitrate >= pattern.MinBitrate && bitrate <= pattern.MaxBitrate)
            {
                score += 0.5f;
            }

            // Basado en palabras clave
            foreach (var keyword in pattern.Keywords)
            {
                if (filename.Contains(keyword))
                {
                    score += 0.2f;
                }
            }

            // Basado en extensión
            if (pattern.PreferredExtensions.Any(ext => filename.Contains(ext)))
            {
                score += 0.3f;
            }

            return Math.Min(score, 1.0f);
        }

        /// <summary>
        /// Calcula confianza de la clasificación
        /// </summary>
        private float CalculateConfidence(float[] embedding, string genre, string quality)
        {
            float confidence = 0.5f; // Base confidence

            // Aumentar confianza si hay patrones claros
            if (_genrePatterns.ContainsKey(genre) && _genrePatterns[genre].Embedding != null)
            {
                confidence += CalculateCosineSimilarity(embedding, _genrePatterns[genre].Embedding) * 0.3f;
            }

            return Math.Min(confidence, 1.0f);
        }

        /// <summary>
        /// Calcula similitud de coseno entre dos vectores
        /// </summary>
        private float CalculateCosineSimilarity(float[] vec1, float[] vec2)
        {
            if (vec1.Length != vec2.Length) return 0.0f;

            float dotProduct = 0.0f;
            float magnitude1 = 0.0f;
            float magnitude2 = 0.0f;

            for (int i = 0; i < vec1.Length; i++)
            {
                dotProduct += vec1[i] * vec2[i];
                magnitude1 += vec1[i] * vec1[i];
                magnitude2 += vec2[i] * vec2[i];
            }

            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            if (magnitude1 == 0 || magnitude2 == 0) return 0.0f;

            return dotProduct / (magnitude1 * magnitude2);
        }

        /// <summary>
        /// Normaliza el tamaño del archivo
        /// </summary>
        private float NormalizeSize(long size)
        {
            var sizeMB = size / (1024.0f * 1024.0f);
            return Math.Min(sizeMB / 100.0f, 1.0f); // Normalizar a 0-1 (100MB = 1.0)
        }

        /// <summary>
        /// Normaliza el bitrate
        /// </summary>
        private float NormalizeBitrate(int bitrate)
        {
            return Math.Min(bitrate / 1000.0f, 1.0f); // Normalizar a 0-1 (1000kbps = 1.0)
        }

        /// <summary>
        /// Inicializa patrones predefinidos
        /// </summary>
        private void InitializePatterns()
        {
            // Patrones de género
            _genrePatterns["rock"] = new GenrePattern
            {
                Keywords = new[] { "rock", "guitar", "band", "album" },
                Embedding = GenerateGenreEmbedding("rock")
            };

            _genrePatterns["electronic"] = new GenrePattern
            {
                Keywords = new[] { "electronic", "techno", "house", "edm", "dj" },
                Embedding = GenerateGenreEmbedding("electronic")
            };

            _genrePatterns["jazz"] = new GenrePattern
            {
                Keywords = new[] { "jazz", "blues", "swing", "bebop" },
                Embedding = GenerateGenreEmbedding("jazz")
            };

            _genrePatterns["classical"] = new GenrePattern
            {
                Keywords = new[] { "classical", "orchestra", "symphony", "concerto" },
                Embedding = GenerateGenreEmbedding("classical")
            };

            // Patrones de calidad
            _qualityPatterns["lossless"] = new QualityPattern
            {
                Keywords = new[] { "flac", "lossless", "wav", "alac" },
                PreferredExtensions = new[] { "flac", "wav", "alac" },
                MinBitrate = 1000,
                MaxBitrate = 3000
            };

            _qualityPatterns["high"] = new QualityPattern
            {
                Keywords = new[] { "320", "high", "hq" },
                PreferredExtensions = new[] { "mp3" },
                MinBitrate = 256,
                MaxBitrate = 320
            };

            _qualityPatterns["medium"] = new QualityPattern
            {
                Keywords = new[] { "192", "medium" },
                PreferredExtensions = new[] { "mp3" },
                MinBitrate = 128,
                MaxBitrate = 256
            };

            _qualityPatterns["standard"] = new QualityPattern
            {
                Keywords = new[] { "128", "standard" },
                PreferredExtensions = new[] { "mp3" },
                MinBitrate = 64,
                MaxBitrate = 192
            };
        }

        /// <summary>
        /// Genera embedding de ejemplo para un género
        /// </summary>
        private float[] GenerateGenreEmbedding(string genre)
        {
            var embedding = new float[128];
            var random = new Random(genre.GetHashCode()); // Seed determinista
            
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)random.NextDouble();
            }

            // Normalizar
            var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
            if (magnitude > 0)
            {
                for (int i = 0; i < embedding.Length; i++)
                {
                    embedding[i] /= magnitude;
                }
            }

            return embedding;
        }
    }

    /// <summary>
    /// Resultado de clasificación de contenido
    /// </summary>
    public class ContentClassification
    {
        public string Filename { get; set; }
        public string PredictedGenre { get; set; }
        public string PredictedQuality { get; set; }
        public float ConfidenceScore { get; set; }
        public float[] Embedding { get; set; }
        public Dictionary<string, float> Features { get; set; }
    }

    /// <summary>
    /// Grupo de duplicados detectados
    /// </summary>
    public class DuplicateGroup
    {
        public AutoSearchFileResult Representative { get; set; }
        public List<AutoSearchFileResult> Duplicates { get; set; } = new List<AutoSearchFileResult>();
        public float SimilarityThreshold { get; set; } = 0.85f;
    }

    /// <summary>
    /// Patrón de género musical
    /// </summary>
    public class GenrePattern
    {
        public string[] Keywords { get; set; }
        public float[] Embedding { get; set; }
    }

    /// <summary>
    /// Patrón de calidad de audio
    /// </summary>
    public class QualityPattern
    {
        public string[] Keywords { get; set; }
        public string[] PreferredExtensions { get; set; }
        public int MinBitrate { get; set; }
        public int MaxBitrate { get; set; }
    }
}
