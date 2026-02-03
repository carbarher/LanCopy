using System;
using System.Collections.Generic;
using System.IO;

namespace SlskDown.Core.AI
{
    public class ConversionJob
    {
        public string SourceFile { get; set; }
        public string TargetFormat { get; set; }
        public string OutputFile { get; set; }
        public ConversionStatus Status { get; set; }
        public int Progress { get; set; }
        public string Error { get; set; }
    }

    public enum ConversionStatus
    {
        Pending,
        Converting,
        Completed,
        Failed
    }

    /// <summary>
    /// Convertidor de formatos de ebooks (requiere Calibre ebook-convert)
    /// </summary>
    public class FormatConverter
    {
        private string calibreConvertPath;
        private List<ConversionJob> jobs = new List<ConversionJob>();

        public FormatConverter()
        {
            DetectCalibreConvert();
        }

        private bool DetectCalibreConvert()
        {
            // Rutas comunes de ebook-convert
            var commonPaths = new[]
            {
                @"C:\Program Files\Calibre2\ebook-convert.exe",
                @"C:\Program Files (x86)\Calibre2\ebook-convert.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Calibre2", "ebook-convert.exe")
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    calibreConvertPath = path;
                    return true;
                }
            }

            return false;
        }

        public bool IsAvailable()
        {
            return !string.IsNullOrEmpty(calibreConvertPath) && File.Exists(calibreConvertPath);
        }

        public ConversionJob QueueConversion(string sourceFile, string targetFormat)
        {
            if (!IsAvailable())
                return null;

            var job = new ConversionJob
            {
                SourceFile = sourceFile,
                TargetFormat = targetFormat.ToLower().TrimStart('.'),
                OutputFile = Path.ChangeExtension(sourceFile, targetFormat),
                Status = ConversionStatus.Pending,
                Progress = 0
            };

            jobs.Add(job);
            return job;
        }

        public string GetConversionCommand(ConversionJob job)
        {
            // Comando para Calibre ebook-convert
            return $"\"{calibreConvertPath}\" \"{job.SourceFile}\" \"{job.OutputFile}\"";
        }

        public List<string> GetSupportedFormats()
        {
            return new List<string>
            {
                "epub", "mobi", "azw3", "pdf", "txt", "html", "docx", "rtf", "odt"
            };
        }

        public string GenerateConversionReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🔄 CONVERSIÓN DE FORMATOS\n");

            if (!IsAvailable())
            {
                sb.AppendLine("❌ Calibre no detectado");
                sb.AppendLine("\nPara usar esta función, instala Calibre:");
                sb.AppendLine("https://calibre-ebook.com/download\n");
                sb.AppendLine("Calibre incluye 'ebook-convert' que permite convertir entre formatos.");
                return sb.ToString();
            }

            sb.AppendLine($"✅ Calibre detectado: {calibreConvertPath}\n");
            sb.AppendLine("📊 FORMATOS SOPORTADOS:");
            sb.AppendLine("  Entrada: EPUB, MOBI, AZW3, PDF, TXT, HTML, DOCX");
            sb.AppendLine("  Salida: EPUB, MOBI, AZW3, PDF, TXT\n");

            if (jobs.Count > 0)
            {
                sb.AppendLine($"📋 TRABAJOS DE CONVERSIÓN: {jobs.Count}\n");
                foreach (var job in jobs)
                {
                    var statusIcon = job.Status switch
                    {
                        ConversionStatus.Pending => "⏳",
                        ConversionStatus.Converting => "🔄",
                        ConversionStatus.Completed => "✅",
                        ConversionStatus.Failed => "❌",
                        _ => "❓"
                    };

                    sb.AppendLine($"{statusIcon} {Path.GetFileName(job.SourceFile)} → {job.TargetFormat.ToUpper()}");
                    if (job.Status == ConversionStatus.Converting)
                        sb.AppendLine($"   Progreso: {job.Progress}%");
                    if (job.Status == ConversionStatus.Failed)
                        sb.AppendLine($"   Error: {job.Error}");
                }
            }
            else
            {
                sb.AppendLine("No hay conversiones en cola.\n");
                sb.AppendLine("Ejemplo: 'convierte libro.pdf a epub'");
            }

            return sb.ToString();
        }

        public string GenerateConversionSuggestion(string filename)
        {
            var ext = Path.GetExtension(filename).ToLower().TrimStart('.');
            
            var suggestions = ext switch
            {
                "pdf" => "💡 PDF detectado. Recomiendo convertir a EPUB para mejor experiencia de lectura.",
                "txt" => "💡 TXT detectado. Convertir a EPUB agregará formato y mejorará la lectura.",
                "mobi" => "💡 MOBI detectado. Si no usas Kindle, considera convertir a EPUB.",
                "doc" or "docx" => "💡 Documento Word detectado. Convertir a EPUB para leer como ebook.",
                _ => null
            };

            return suggestions;
        }

        public List<ConversionJob> GetJobs() => jobs;

        public void ClearCompletedJobs()
        {
            jobs.RemoveAll(j => j.Status == ConversionStatus.Completed);
        }
    }
}
