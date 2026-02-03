using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core
{
    public class DiagnosticResult
    {
        public string Issue { get; set; }
        public string Severity { get; set; } // "critical", "warning", "info"
        public string Description { get; set; }
        public List<string> Solutions { get; set; } = new List<string>();
        public bool AutoFixable { get; set; }
    }

    public static class Troubleshooter
    {
        public static List<DiagnosticResult> DiagnoseDownloadSpeed(double currentSpeed, int activeDownloads, int queueSize)
        {
            var results = new List<DiagnosticResult>();

            // Velocidad muy lenta
            if (currentSpeed < 50 * 1024 && activeDownloads > 0) // < 50 KB/s
            {
                results.Add(new DiagnosticResult
                {
                    Issue = "Velocidad de descarga muy lenta",
                    Severity = "warning",
                    Description = $"Velocidad actual: {FormatSpeed(currentSpeed)}. Esto es inusualmente lento.",
                    Solutions = new List<string>
                    {
                        "Reducir descargas simultáneas a 2-3",
                        "Buscar proveedores alternativos",
                        "Verificar conexión a internet",
                        "Pausar descargas de baja prioridad"
                    },
                    AutoFixable = true
                });
            }

            // Demasiadas descargas simultáneas
            if (activeDownloads > 5)
            {
                results.Add(new DiagnosticResult
                {
                    Issue = "Demasiadas descargas simultáneas",
                    Severity = "warning",
                    Description = $"{activeDownloads} descargas activas compitiendo por ancho de banda.",
                    Solutions = new List<string>
                    {
                        $"Reducir a 3 descargas simultáneas (actualmente: {activeDownloads})",
                        "Priorizar descargas importantes",
                        "Pausar descargas menos urgentes"
                    },
                    AutoFixable = true
                });
            }

            // Cola muy grande
            if (queueSize > 100)
            {
                results.Add(new DiagnosticResult
                {
                    Issue = "Cola de descargas muy grande",
                    Severity = "info",
                    Description = $"{queueSize} archivos en cola. Esto puede tardar mucho tiempo.",
                    Solutions = new List<string>
                    {
                        "Cancelar descargas de baja prioridad",
                        "Filtrar por tamaño para descargar archivos pequeños primero",
                        "Considerar descargar en lotes"
                    },
                    AutoFixable = false
                });
            }

            return results;
        }

        public static List<DiagnosticResult> DiagnoseFailedDownloads(List<(string filename, string error, int attempts)> failures)
        {
            var results = new List<DiagnosticResult>();

            if (failures.Count == 0)
                return results;

            // Muchos fallos
            if (failures.Count > 10)
            {
                results.Add(new DiagnosticResult
                {
                    Issue = "Alto número de descargas fallidas",
                    Severity = "critical",
                    Description = $"{failures.Count} descargas han fallado recientemente.",
                    Solutions = new List<string>
                    {
                        "Verificar conexión a Soulseek",
                        "Reiniciar cliente Soulseek",
                        "Buscar proveedores alternativos",
                        "Verificar espacio en disco"
                    },
                    AutoFixable = false
                });
            }

            // Fallos por usuario offline
            var offlineErrors = failures.Count(f => f.error.Contains("offline", StringComparison.OrdinalIgnoreCase));
            if (offlineErrors > 5)
            {
                results.Add(new DiagnosticResult
                {
                    Issue = "Múltiples usuarios offline",
                    Severity = "warning",
                    Description = $"{offlineErrors} descargas fallaron porque los usuarios están offline.",
                    Solutions = new List<string>
                    {
                        "Buscar proveedores alternativos automáticamente",
                        "Reintentar más tarde",
                        "Activar búsqueda de fuentes alternativas"
                    },
                    AutoFixable = true
                });
            }

            // Muchos reintentos
            var highRetries = failures.Where(f => f.attempts > 5).ToList();
            if (highRetries.Count > 3)
            {
                results.Add(new DiagnosticResult
                {
                    Issue = "Archivos con múltiples reintentos",
                    Severity = "warning",
                    Description = $"{highRetries.Count} archivos han sido reintentados más de 5 veces.",
                    Solutions = new List<string>
                    {
                        "Cancelar descargas problemáticas",
                        "Buscar fuentes alternativas",
                        "Verificar si los archivos están disponibles"
                    },
                    AutoFixable = true
                });
            }

            return results;
        }

        public static List<DiagnosticResult> DiagnoseSearchIssues(int searchCount, int resultsCount, TimeSpan searchTime)
        {
            var results = new List<DiagnosticResult>();

            // Sin resultados
            if (searchCount > 0 && resultsCount == 0)
            {
                results.Add(new DiagnosticResult
                {
                    Issue = "Búsqueda sin resultados",
                    Severity = "warning",
                    Description = "La búsqueda no devolvió ningún resultado.",
                    Solutions = new List<string>
                    {
                        "Intentar con términos de búsqueda más generales",
                        "Verificar ortografía del autor",
                        "Buscar en inglés si buscaste en español (o viceversa)",
                        "Verificar conexión a Soulseek"
                    },
                    AutoFixable = false
                });
            }

            // Búsqueda muy lenta
            if (searchTime.TotalSeconds > 30)
            {
                results.Add(new DiagnosticResult
                {
                    Issue = "Búsqueda muy lenta",
                    Severity = "info",
                    Description = $"La búsqueda tardó {searchTime.TotalSeconds:F0} segundos.",
                    Solutions = new List<string>
                    {
                        "Reducir timeout de búsqueda",
                        "Limitar número de resultados",
                        "Verificar conexión a internet"
                    },
                    AutoFixable = true
                });
            }

            // Pocos resultados
            if (resultsCount > 0 && resultsCount < 5)
            {
                results.Add(new DiagnosticResult
                {
                    Issue = "Pocos resultados encontrados",
                    Severity = "info",
                    Description = $"Solo se encontraron {resultsCount} resultados.",
                    Solutions = new List<string>
                    {
                        "Ampliar criterios de búsqueda",
                        "Buscar variaciones del nombre",
                        "Intentar en otro idioma"
                    },
                    AutoFixable = false
                });
            }

            return results;
        }

        public static string GenerateDiagnosticReport(List<DiagnosticResult> results)
        {
            if (results.Count == 0)
                return "✅ No se detectaron problemas. Todo funciona correctamente.";

            var report = new System.Text.StringBuilder();
            report.AppendLine("🔧 DIAGNÓSTICO DEL SISTEMA:\n");

            var critical = results.Where(r => r.Severity == "critical").ToList();
            var warnings = results.Where(r => r.Severity == "warning").ToList();
            var info = results.Where(r => r.Severity == "info").ToList();

            if (critical.Count > 0)
            {
                report.AppendLine("❌ PROBLEMAS CRÍTICOS:");
                foreach (var result in critical)
                {
                    report.AppendLine($"\n• {result.Issue}");
                    report.AppendLine($"  {result.Description}");
                    report.AppendLine("  Soluciones:");
                    foreach (var solution in result.Solutions)
                        report.AppendLine($"    - {solution}");
                }
                report.AppendLine();
            }

            if (warnings.Count > 0)
            {
                report.AppendLine("⚠️ ADVERTENCIAS:");
                foreach (var result in warnings)
                {
                    report.AppendLine($"\n• {result.Issue}");
                    report.AppendLine($"  {result.Description}");
                    report.AppendLine("  Soluciones:");
                    foreach (var solution in result.Solutions)
                        report.AppendLine($"    - {solution}");
                }
                report.AppendLine();
            }

            if (info.Count > 0)
            {
                report.AppendLine("ℹ️ INFORMACIÓN:");
                foreach (var result in info)
                {
                    report.AppendLine($"\n• {result.Issue}");
                    report.AppendLine($"  {result.Description}");
                }
                report.AppendLine();
            }

            var autoFixable = results.Count(r => r.AutoFixable);
            if (autoFixable > 0)
            {
                report.AppendLine($"💡 {autoFixable} problema(s) pueden solucionarse automáticamente.");
            }

            return report.ToString();
        }

        public static string DiagnoseSpecificFile(string filename, string status, int attempts, string lastError)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"🔍 DIAGNÓSTICO: {filename}\n");
            report.AppendLine($"Estado: {status}");
            report.AppendLine($"Intentos: {attempts}");
            
            if (!string.IsNullOrEmpty(lastError))
            {
                report.AppendLine($"Último error: {lastError}\n");

                if (lastError.Contains("offline", StringComparison.OrdinalIgnoreCase))
                {
                    report.AppendLine("Causa probable: Usuario desconectado");
                    report.AppendLine("\nSoluciones:");
                    report.AppendLine("  • Esperar a que el usuario se conecte");
                    report.AppendLine("  • Buscar proveedor alternativo");
                    report.AppendLine("  • Activar reintentos automáticos");
                }
                else if (lastError.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    report.AppendLine("Causa probable: Timeout de conexión");
                    report.AppendLine("\nSoluciones:");
                    report.AppendLine("  • Aumentar timeout de descarga");
                    report.AppendLine("  • Verificar conexión a internet");
                    report.AppendLine("  • Reintentar la descarga");
                }
                else if (lastError.Contains("space", StringComparison.OrdinalIgnoreCase))
                {
                    report.AppendLine("Causa probable: Sin espacio en disco");
                    report.AppendLine("\nSoluciones:");
                    report.AppendLine("  • Liberar espacio en disco");
                    report.AppendLine("  • Cambiar carpeta de descargas");
                    report.AppendLine("  • Eliminar archivos innecesarios");
                }
                else
                {
                    report.AppendLine("Causa: Error desconocido");
                    report.AppendLine("\nSoluciones generales:");
                    report.AppendLine("  • Reintentar la descarga");
                    report.AppendLine("  • Buscar proveedor alternativo");
                    report.AppendLine("  • Verificar logs para más detalles");
                }
            }

            if (attempts > 10)
            {
                report.AppendLine($"\n⚠️ ADVERTENCIA: {attempts} intentos es demasiado.");
                report.AppendLine("Considera cancelar esta descarga y buscar otra fuente.");
            }

            return report.ToString();
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }
    }
}
