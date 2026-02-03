using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Sistema de auto-actualizaciÃ³n desde GitHub
    /// </summary>
    public partial class MainForm
    {
        private static readonly string githubApiUrl = "https://api.github.com/repos/slskdown/slskdown/releases/latest";
        private static readonly string downloadUrl = "https://github.com/slskdown/slskdown/releases/latest";
        private static readonly string versionFile = @"c:\p2p\SlskDown\version.json";
        private static readonly string updateLogFile = @"c:\p2p\SlskDown\update_log.txt";
        
        private Version currentVersion = new Version("2.0.0");
        private bool autoUpdateEnabled = true;
        private HttpClient? httpClient;
        
        /// <summary>
        /// InformaciÃ³n de versiÃ³n
        /// </summary>
        public class ReleaseInfo
        {
            public string TagName { get; set; } = "";
            public string Name { get; set; } = "";
            public string Body { get; set; } = "";
            public string PublishedAt { get; set; } = "";
            public bool Prerelease { get; set; }
            public Asset[] Assets { get; set; } = Array.Empty<Asset>();
        }
        
        /// <summary>
        /// Asset de descarga
        /// </summary>
        public class Asset
        {
            public string Name { get; set; } = "";
            public string BrowserDownloadUrl { get; set; } = "";
            public long Size { get; set; }
            public string ContentType { get; set; } = "";
        }
        
        /// <summary>
        /// Inicializar sistema de auto-actualizaciÃ³n
        /// </summary>
        private void InitializeGitHubAutoUpdate()
        {
            try
            {
                Console.WriteLine("[GitHubUpdate] ðŸ”„ Inicializando sistema de auto-actualizaciÃ³n");
                
                // Crear HTTP client
                httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "SlskDown-AutoUpdater");
                
                // Cargar versiÃ³n actual
                LoadCurrentVersion();
                
                // Verificar actualizaciones en segundo plano
                Task.Run(CheckForUpdates);
                
                Console.WriteLine($"[GitHubUpdate] âœ… Auto-actualizaciÃ³n inicializada - VersiÃ³n actual: {currentVersion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error inicializando auto-actualizaciÃ³n: {ex.Message}");
                autoUpdateEnabled = false;
            }
        }
        
        /// <summary>
        /// Cargar versiÃ³n actual desde archivo
        /// </summary>
        private void LoadCurrentVersion()
        {
            try
            {
                if (File.Exists(versionFile))
                {
                    var json = File.ReadAllText(versionFile);
                    var versionData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    
                    if (versionData?.ContainsKey("version") == true)
                    {
                        currentVersion = new Version(versionData["version"]);
                    }
                }
                else
                {
                    // Crear archivo de versiÃ³n si no existe
                    SaveCurrentVersion();
                }
                
                Console.WriteLine($"[GitHubUpdate] ðŸ“‹ VersiÃ³n actual cargada: {currentVersion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error cargando versiÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Guardar versiÃ³n actual a archivo
        /// </summary>
        private void SaveCurrentVersion()
        {
            try
            {
                var versionData = new Dictionary<string, string>
                {
                    ["version"] = currentVersion.ToString(),
                    ["build_date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["build_number"] = DateTime.Now.Ticks.ToString()
                };
                
                var json = JsonSerializer.Serialize(versionData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(versionFile, json);
                
                Console.WriteLine($"[GitHubUpdate] ðŸ’¾ VersiÃ³n guardada: {currentVersion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error guardando versiÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Verificar actualizaciones disponibles
        /// </summary>
        private async Task CheckForUpdates()
        {
            try
            {
                Console.WriteLine("[GitHubUpdate] ðŸ” Verificando actualizaciones...");
                
                var response = await httpClient!.GetAsync(githubApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GitHubUpdate] âŒ Error API GitHub: {response.StatusCode}");
                    return;
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var releaseInfo = JsonSerializer.Deserialize<ReleaseInfo>(json);
                
                if (releaseInfo != null)
                {
                    await ProcessReleaseInfo(releaseInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error verificando actualizaciones: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Procesar informaciÃ³n de release
        /// </summary>
        private async Task ProcessReleaseInfo(ReleaseInfo releaseInfo)
        {
            try
            {
                // Extraer versiÃ³n del tag
                var latestVersion = new Version(releaseInfo.TagName.TrimStart('v'));
                
                Console.WriteLine($"[GitHubUpdate] ðŸ“¦ Ãšltima versiÃ³n disponible: {latestVersion}");
                Console.WriteLine($"[GitHubUpdate] ðŸ“¦ VersiÃ³n actual: {currentVersion}");
                
                if (latestVersion > currentVersion)
                {
                    Console.WriteLine("[GitHubUpdate] ðŸŽ‰ Â¡Nueva actualizaciÃ³n disponible!");
                    
                    // Mostrar notificaciÃ³n
                    ShowUpdateNotification(releaseInfo, latestVersion);
                    
                    // Guardar informaciÃ³n de actualizaciÃ³n
                    await SaveUpdateInfo(releaseInfo, latestVersion);
                }
                else if (latestVersion == currentVersion)
                {
                    Console.WriteLine("[GitHubUpdate] âœ… SlskDown estÃ¡ actualizado");
                }
                else
                {
                    Console.WriteLine("[GitHubUpdate] âš ï¸ VersiÃ³n de desarrollo detectada");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error procesando release: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Mostrar notificaciÃ³n de actualizaciÃ³n
        /// </summary>
        private void ShowUpdateNotification(ReleaseInfo releaseInfo, Version newVersion)
        {
            try
            {
                var title = $"ðŸ”„ ActualizaciÃ³n Disponible v{newVersion}";
                var message = $"Nueva versiÃ³n disponible: {releaseInfo.Name}\n\n" +
                             $"Â¿Desea descargar e instalar ahora?";
                
                var result = MessageBox.Show(
                    message,
                    title,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );
                
                if (result == DialogResult.Yes)
                {
                    Task.Run(() => DownloadAndInstallUpdate(releaseInfo));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error mostrando notificaciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Guardar informaciÃ³n de actualizaciÃ³n
        /// </summary>
        private async Task SaveUpdateInfo(ReleaseInfo releaseInfo, Version newVersion)
        {
            try
            {
                var updateInfo = new
                {
                    available_version = newVersion.ToString(),
                    release_name = releaseInfo.Name,
                    release_notes = releaseInfo.Body,
                    published_at = releaseInfo.PublishedAt,
                    download_url = downloadUrl,
                    checked_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                
                var json = JsonSerializer.Serialize(updateInfo, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(updateLogFile, json);
                
                Console.WriteLine($"[GitHubUpdate] ðŸ’¾ InformaciÃ³n de actualizaciÃ³n guardada");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error guardando info de actualizaciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Descargar e instalar actualizaciÃ³n
        /// </summary>
        private async Task DownloadAndInstallUpdate(ReleaseInfo releaseInfo)
        {
            try
            {
                Console.WriteLine("[GitHubUpdate] â¬‡ï¸ Iniciando descarga de actualizaciÃ³n...");
                
                // Buscar asset de Windows
                var windowsAsset = releaseInfo.Assets
                    .FirstOrDefault(a => a.Name.Contains("win") || a.Name.Contains("windows") || a.Name.EndsWith(".exe"));
                
                if (windowsAsset == null)
                {
                    MessageBox.Show("No se encontrÃ³ el archivo de instalaciÃ³n para Windows", "Error de ActualizaciÃ³n", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // Descargar archivo
                var tempFile = Path.GetTempFileName();
                await DownloadFile(windowsAsset.BrowserDownloadUrl, tempFile);
                
                Console.WriteLine($"[GitHubUpdate] âœ… Descarga completada: {windowsAsset.Name}");
                
                // Mostrar diÃ¡logo de instalaciÃ³n
                ShowInstallDialog(tempFile, windowsAsset.Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error descargando actualizaciÃ³n: {ex.Message}");
                MessageBox.Show($"Error descargando actualizaciÃ³n: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Descargar archivo con progreso
        /// </summary>
        private async Task DownloadFile(string url, string filePath)
        {
            try
            {
                using var response = await httpClient!.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;
                
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                
                var buffer = new byte[8192];
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    
                    // Actualizar progreso (opcional)
                    var progress = totalBytes > 0 ? (int)((downloadedBytes * 100) / totalBytes) : 0;
                    
                    if (downloadedBytes % (1024 * 1024) == 0) // Cada MB
                    {
                        Console.WriteLine($"[GitHubUpdate] â¬‡ï¸ Descargando: {progress}% ({FormatBytes(downloadedBytes)}/{FormatBytes(totalBytes)})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error descargando archivo: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Mostrar diÃ¡logo de instalaciÃ³n
        /// </summary>
        private void ShowInstallDialog(string installerPath, string fileName)
        {
            try
            {
                var message = $"La actualizaciÃ³n ha sido descargada:\n\n" +
                             $"ðŸ“ {fileName}\n" +
                             $"ðŸ’¾ {FormatBytes(new FileInfo(installerPath).Length)}\n\n" +
                             $"Â¿Desea instalar ahora?\n\n" +
                             $"âš ï¸ SlskDown se cerrarÃ¡ durante la instalaciÃ³n.";
                
                var result = MessageBox.Show(
                    message,
                    "Instalar ActualizaciÃ³n",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.Yes)
                {
                    // Iniciar instalaciÃ³n
                    StartInstallation(installerPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error mostrando diÃ¡logo de instalaciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Iniciar proceso de instalaciÃ³n
        /// </summary>
        private void StartInstallation(string installerPath)
        {
            try
            {
                Console.WriteLine("[GitHubUpdate] ðŸš€ Iniciando instalaciÃ³n...");
                
                // Crear script de instalaciÃ³n
                var installScript = CreateInstallScript(installerPath);
                
                // Iniciar proceso de instalaciÃ³n
                var startInfo = new ProcessStartInfo
                {
                    FileName = installScript,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                Process.Start(startInfo);
                
                // Cerrar aplicaciÃ³n actual
                Application.Exit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error iniciando instalaciÃ³n: {ex.Message}");
                MessageBox.Show($"Error iniciando instalaciÃ³n: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Crear script de instalaciÃ³n
        /// </summary>
        private string CreateInstallScript(string installerPath)
        {
            try
            {
                var scriptPath = Path.GetTempFileName() + ".bat";
                
                var script = $@"
@echo off
echo [GitHubUpdate] ðŸš€ Iniciando instalaciÃ³n de SlskDown...
timeout /t 2 /nobreak > nul

echo [GitHubUpdate] ðŸ“¦ Ejecutando instalador...
start "" /wait ""{installerPath}""

echo [GitHubUpdate] âœ… InstalaciÃ³n completada
echo [GitHubUpdate] ðŸ”„ Reiniciando SlskDown...
timeout /t 3 /nobreak > nul

start "" ""c:\p2p\slsk.bat""

echo [GitHubUpdate] ðŸ§¹ Limpiando archivos temporales...
del ""{scriptPath}""
del ""{installerPath}""

echo [GitHubUpdate] âœ… Proceso de actualizaciÃ³n completado
";
                
                File.WriteAllText(scriptPath, script);
                
                Console.WriteLine($"[GitHubUpdate] ðŸ“ Script de instalaciÃ³n creado: {scriptPath}");
                
                return scriptPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error creando script: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Verificar actualizaciones manualmente
        /// </summary>
        private async Task CheckForUpdatesManually()
        {
            try
            {
                Console.WriteLine("[GitHubUpdate] ðŸ” VerificaciÃ³n manual de actualizaciones");
                
                // Mostrar mensaje de verificaciÃ³n
                AddColoredLogMessage("ðŸ” Verificando actualizaciones en GitHub...", LogMessageType.Info);
                
                await CheckForUpdates();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error en verificaciÃ³n manual: {ex.Message}");
                AddColoredLogMessage($"âŒ Error verificando actualizaciones: {ex.Message}", LogMessageType.Error);
            }
        }
        
        /// <summary>
        /// Mostrar historial de actualizaciones
        /// </summary>
        private void ShowUpdateHistory()
        {
            try
            {
                var history = "";
                
                if (File.Exists(updateLogFile))
                {
                    history = File.ReadAllText(updateLogFile);
                }
                else
                {
                    history = "No hay historial de actualizaciones disponible.";
                }
                
                var message = $"""
ðŸ“‹ HISTORIAL DE ACTUALIZACIONES
========================================
VersiÃ³n Actual: {currentVersion}
Ãšltima VerificaciÃ³n: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

{history}

ðŸ’¾ Datos guardados en: {updateLogFile}
ðŸ”„ API: {githubApiUrl}
""";
                
                Console.WriteLine(message);
                MessageBox.Show(message, "Historial de Actualizaciones - SlskDown", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error mostrando historial: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Formatear bytes para legibilidad
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        /// <summary>
        /// Limpiar recursos de auto-actualizaciÃ³n
        /// </summary>
        private void CleanupAutoUpdate()
        {
            try
            {
                if (httpClient != null)
                {
                    httpClient.Dispose();
                    httpClient = null;
                }
                
                Console.WriteLine("[GitHubUpdate] ðŸ§¹ Recursos de auto-actualizaciÃ³n limpiados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHubUpdate] âŒ Error limpiando auto-actualizaciÃ³n: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Forzar verificaciÃ³n de actualizaciones (para botÃ³n)
        /// </summary>
        private void ForceUpdateCheck()
        {
            Task.Run(async () =>
            {
                try
                {
                    await CheckForUpdatesManually();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GitHubUpdate] âŒ Error en verificaciÃ³n forzada: {ex.Message}");
                }
            });
        }
    }
}

