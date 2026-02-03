using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.AI
{
    public class FileQualityScore
    {
        public double Score { get; set; } // 0-10
        public List<string> Positives { get; set; } = new List<string>();
        public List<string> Negatives { get; set; } = new List<string>();
        public string Recommendation { get; set; }
    }

    /// <summary>
    /// Analizador de calidad de archivos antes de descargar
    /// </summary>
    public class FileQualityAnalyzer
    {
        public FileQualityScore AnalyzeFile(string filename, long sizeBytes, string username = null)
        {
            var score = new FileQualityScore { Score = 5.0 };

            // Análisis de extensión
            var ext = System.IO.Path.GetExtension(filename).ToLower();
            AnalyzeExtension(ext, score);

            // Análisis de tamaño
            AnalyzeSize(sizeBytes, ext, score);

            // Análisis de nombre
            AnalyzeFilename(filename, score);

            // Análisis de proveedor (si disponible)
            if (!string.IsNullOrEmpty(username))
                AnalyzeProvider(username, score);

            // Generar recomendación
            GenerateRecommendation(score);

            return score;
        }

        private void AnalyzeExtension(string ext, FileQualityScore score)
        {
            switch (ext)
            {
                case ".epub":
                    score.Score += 2.0;
                    score.Positives.Add("EPUB: Formato ideal para ebooks");
                    break;
                case ".pdf":
                    score.Score += 1.5;
                    score.Positives.Add("PDF: Buena compatibilidad");
                    break;
                case ".mobi":
                case ".azw3":
                    score.Score += 1.0;
                    score.Positives.Add($"{ext.ToUpper()}: Compatible con Kindle");
                    break;
                case ".txt":
                    score.Score -= 1.0;
                    score.Negatives.Add("TXT: Sin formato, puede ser difícil de leer");
                    break;
                case ".doc":
                case ".docx":
                    score.Score -= 0.5;
                    score.Negatives.Add("DOC: No es formato ideal para ebooks");
                    break;
            }
        }

        private void AnalyzeSize(long bytes, string ext, FileQualityScore score)
        {
            var mb = bytes / (1024.0 * 1024);

            if (ext == ".epub")
            {
                if (mb >= 1 && mb <= 10)
                {
                    score.Score += 1.0;
                    score.Positives.Add($"Tamaño óptimo: {mb:F1} MB");
                }
                else if (mb < 0.5)
                {
                    score.Score -= 1.5;
                    score.Negatives.Add($"Muy pequeño ({mb:F1} MB): Posible baja calidad");
                }
                else if (mb > 50)
                {
                    score.Score -= 0.5;
                    score.Negatives.Add($"Muy grande ({mb:F1} MB): Puede tener imágenes innecesarias");
                }
            }
            else if (ext == ".pdf")
            {
                if (mb >= 2 && mb <= 20)
                {
                    score.Score += 0.5;
                    score.Positives.Add($"Tamaño razonable: {mb:F1} MB");
                }
                else if (mb < 1)
                {
                    score.Score -= 2.0;
                    score.Negatives.Add($"Muy pequeño ({mb:F1} MB): Posible OCR malo o páginas faltantes");
                }
            }
        }

        private void AnalyzeFilename(string filename, FileQualityScore score)
        {
            var lower = filename.ToLower();

            // Indicadores positivos
            if (lower.Contains("retail") || lower.Contains("original"))
            {
                score.Score += 1.0;
                score.Positives.Add("Versión retail/original");
            }

            if (lower.Contains("illustrated") || lower.Contains("ilustrado"))
            {
                score.Score += 0.5;
                score.Positives.Add("Versión ilustrada");
            }

            if (lower.Contains("complete") || lower.Contains("completo"))
            {
                score.Score += 0.5;
                score.Positives.Add("Versión completa");
            }

            // Indicadores negativos
            if (lower.Contains("sample") || lower.Contains("muestra"))
            {
                score.Score -= 3.0;
                score.Negatives.Add("⚠️ MUESTRA: No es el libro completo");
            }

            if (lower.Contains("ocr") && !lower.Contains("no ocr"))
            {
                score.Score -= 1.0;
                score.Negatives.Add("OCR: Puede tener errores de reconocimiento");
            }

            if (lower.Contains("scan") || lower.Contains("escaneado"))
            {
                score.Score -= 0.5;
                score.Negatives.Add("Escaneado: Calidad puede variar");
            }

            // Detección de idioma en nombre
            if (lower.Contains("spanish") || lower.Contains("español") || lower.Contains("spa"))
            {
                score.Positives.Add("Idioma: Español confirmado en nombre");
            }
        }

        private void AnalyzeProvider(string username, FileQualityScore score)
        {
            // Aquí podrías integrar con un sistema de reputación
            // Por ahora, análisis básico
            
            if (username.Length > 15)
            {
                score.Positives.Add("Usuario con nombre establecido");
            }
        }

        private void GenerateRecommendation(FileQualityScore score)
        {
            score.Score = Math.Max(0, Math.Min(10, score.Score));

            if (score.Score >= 8)
                score.Recommendation = "✅ EXCELENTE - Altamente recomendado";
            else if (score.Score >= 6)
                score.Recommendation = "👍 BUENO - Calidad aceptable";
            else if (score.Score >= 4)
                score.Recommendation = "⚠️ REGULAR - Considera buscar alternativa";
            else
                score.Recommendation = "❌ MALO - No recomendado, busca otra versión";
        }

        public string GenerateQualityReport(FileQualityScore score, string filename)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📊 ANÁLISIS DE CALIDAD: {filename}\n");
            sb.AppendLine($"Puntuación: {score.Score:F1}/10");
            sb.AppendLine($"{score.Recommendation}\n");

            if (score.Positives.Count > 0)
            {
                sb.AppendLine("✅ ASPECTOS POSITIVOS:");
                foreach (var positive in score.Positives)
                {
                    sb.AppendLine($"  • {positive}");
                }
                sb.AppendLine();
            }

            if (score.Negatives.Count > 0)
            {
                sb.AppendLine("⚠️ ASPECTOS NEGATIVOS:");
                foreach (var negative in score.Negatives)
                {
                    sb.AppendLine($"  • {negative}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public List<dynamic> RankFilesByQuality(List<dynamic> files)
        {
            var scored = files.Select(f => new
            {
                File = f,
                Quality = AnalyzeFile(
                    f.FileName?.ToString() ?? "",
                    f.SizeBytes ?? f.Size ?? 0,
                    f.Username?.ToString()
                )
            }).OrderByDescending(x => x.Quality.Score).ToList();

            return scored.Select(x => x.File).ToList();
        }
    }
}
