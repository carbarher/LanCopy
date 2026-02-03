using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SlskDown.Core
{
    /// <summary>
    /// Cliente para eMule WebServer (puerto 4711)
    /// Alternativa a External Connection cuando solo está disponible el Servidor Web
    /// </summary>
    public class EmuleWebClient : IEmuleClient
    {
        private readonly string _baseUrl;
        private readonly string _password;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private bool _isConnected;
        private EmuleConnectionState _state = EmuleConnectionState.Disconnected;
        private Action<string> _logCallback;
        private string _sessionId = "";

        public bool IsConnected => _isConnected;
        public EmuleConnectionState State => _state;

        public event EventHandler<EmuleConnectionStateChangedEventArgs> StateChanged;
        public event EventHandler<EmuleSearchResultsEventArgs> SearchResultsReceived;

        public void SetLogCallback(Action<string> logCallback)
        {
            _logCallback = logCallback;
        }

        private void Log(string message)
        {
            _logCallback?.Invoke($"eMule: {message}");
        }

        /// <summary>
        /// Constructor del cliente eMule WebServer
        /// </summary>
        /// <param name="host">Host del servidor web (por defecto localhost)</param>
        /// <param name="port">Puerto del servidor web (por defecto 4711)</param>
        /// <param name="password">Contraseña del administrador</param>
        public EmuleWebClient(string host = "127.0.0.1", int port = 4711, string password = "")
        {
            _baseUrl = $"http://{host}:{port}";
            _password = password;
            
            // Usar CookieContainer para mantener la sesión
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isConnected)
            {
                return;
            }

            try
            {
                ChangeState(EmuleConnectionState.Connecting, "Conectando a eMule WebServer...");
                Log($"Password configurado: '{_password}' (longitud: {_password?.Length ?? 0})");

                // Autenticarse en eMule WebServer
                // El formato correcto es: /?w=<password> para autenticarse
                var loginUrl = $"{_baseUrl}/?w={Uri.EscapeDataString(_password)}";
                Log($"URL de login: {loginUrl}");
                
                var response = await _httpClient.GetAsync(loginUrl, cancellationToken);
                Log($"Respuesta HTTP: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Log($"Contenido recibido ({content.Length} bytes)");
                    
                    // Mostrar preview del contenido
                    if (content.Length < 2000)
                    {
                        Log($"HTML completo:");
                        Log(content);
                    }
                    else
                    {
                        var preview = content.Substring(0, Math.Min(500, content.Length));
                        Log($"Preview (primeros 500 chars):");
                        Log(preview);
                    }
                    
                    // Verificar que la autenticación fue exitosa
                    // eMule devuelve XML con result="FAILED" si la contraseña es incorrecta
                    if (content.Contains("WRONG_PASSWORD") || 
                        content.Contains("result=\"FAILED\"") ||
                        content.Contains("contraseña es incorrecta") ||
                        content.Contains("password is incorrect"))
                    {
                        Log("Contraseña incorrecta según respuesta del servidor");
                        throw new InvalidOperationException("Contraseña incorrecta");
                    }
                    
                    // Si no hay error, consideramos el login exitoso
                    _isConnected = true;
                    ChangeState(EmuleConnectionState.Connected, "Conectado a eMule WebServer");
                    Log("Login exitoso");
                }
                else
                {
                    Log($"Error HTTP: {response.StatusCode}");
                    throw new InvalidOperationException($"Error HTTP: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Log($"Excepción: {ex.Message}");
                ChangeState(EmuleConnectionState.Error, $"Error conectando: {ex.Message}");
                throw;
            }
        }

        public Task DisconnectAsync()
        {
            _isConnected = false;
            ChangeState(EmuleConnectionState.Disconnected, "Desconectado");
            return Task.CompletedTask;
        }

        public async Task<string> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Cliente eMule no está conectado");
            }

            var searchId = Guid.NewGuid().ToString("N");

            try
            {
                // Hacer login antes de buscar para asegurar sesión válida
                Log("Haciendo login antes de búsqueda...");
                var loginContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("w", "password"),
                    new KeyValuePair<string, string>("p", _password)
                });
                
                var loginResponse = await _httpClient.PostAsync(_baseUrl, loginContent, cancellationToken);
                var loginHtml = await loginResponse.Content.ReadAsStringAsync();
                
                // Verificar que el login fue exitoso (no debe contener el formulario de login)
                if (loginHtml.Contains("Introduce tu contraseña") || loginHtml.Contains("Enter password"))
                {
                    Log("Login falló - contraseña incorrecta o sesión no establecida");
                    throw new InvalidOperationException("Login falló");
                }
                
                // Extraer ID de sesión del HTML (buscar parámetro ses= en los enlaces)
                var sesMatch = System.Text.RegularExpressions.Regex.Match(loginHtml, @"ses=([A-Za-z0-9]+)");
                if (sesMatch.Success)
                {
                    _sessionId = sesMatch.Groups[1].Value;
                    Log($"Login exitoso, sesión: {_sessionId}");
                }
                else
                {
                    Log("Login exitoso, iniciando búsqueda...");
                }

                // NOTA: eMule WebServer acumula resultados de todas las búsquedas
                // Los comandos Clear, Stop y logout no limpian el historial
                // Esta es una limitación conocida del WebServer de eMule
                // Los resultados mostrados pueden incluir búsquedas anteriores
                
                // Enviar búsqueda vía WebServer
                // URL: /?ses=<sessionId>&w=search&t=<query>&c=Start
                var searchUrl = $"{_baseUrl}/?ses={_sessionId}&w=search&t={Uri.EscapeDataString(query)}&c=Start";
                Log($"Enviando búsqueda a eMule: '{query}'");
                Log($"URL de búsqueda: {searchUrl}");
                var response = await _httpClient.GetAsync(searchUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    Log($"Búsqueda enviada, respuesta recibida ({html.Length} bytes)");
                    
                    // Mostrar preview de la respuesta para debug
                    if (html.Length < 1000)
                    {
                        Log($"Respuesta completa: {html}");
                    }
                    else
                    {
                        var preview = html.Substring(0, Math.Min(500, html.Length));
                        Log($"Preview respuesta: {preview}");
                    }
                    
                    // IMPORTANTE: Esperar a que el polling termine ANTES de retornar
                    // Esto asegura que eMule complete la búsqueda antes de aceptar otra
                    await Task.Delay(2000, cancellationToken); // Delay inicial
                    await ParseSearchResultsFromHtml(searchId, query, cancellationToken);
                }
                else
                {
                    Log($"Error HTTP en búsqueda: {response.StatusCode}");
                    throw new InvalidOperationException($"Error en búsqueda: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error en búsqueda eMule: {ex.Message}", ex);
            }

            return searchId;
        }

        public Task CancelSearchAsync(string searchId)
        {
            return Task.CompletedTask;
        }

        public async Task<EmuleDownload> DownloadAsync(EmuleSearchResult result, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Cliente eMule no está conectado");
            }

            try
            {
                // Enviar comando de descarga vía WebServer
                // URL: /?ses=&w=transfer&op=start&file=<hash>
                var downloadUrl = $"{_baseUrl}/?ses=&w=transfer&op=start&file={result.FileHash}";
                var response = await _httpClient.GetAsync(downloadUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Error iniciando descarga: {response.StatusCode}");
                }

                var download = new EmuleDownload
                {
                    DownloadId = Guid.NewGuid().ToString("N"),
                    FileHash = result.FileHash,
                    FileName = result.FileName,
                    FileSize = result.FileSize,
                    State = EmuleDownloadState.Queued
                };

                return download;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error iniciando descarga eMule: {ex.Message}", ex);
            }
        }

        private async Task ParseSearchResultsFromHtml(string searchId, string query, CancellationToken cancellationToken)
        {
            try
            {
                var allResults = new List<EmuleSearchResult>();
                var seenHashes = new HashSet<string>();
                
                Log($"Obteniendo resultados de eMule con polling...");
                
                // Polling: esperar hasta que eMule tenga todos los resultados
                // eMule puede tardar varios segundos en completar la búsqueda
                // TIMEOUT: Detener después de 30 segundos para evitar esperas indefinidas
                var resultsUrl = $"{_baseUrl}/?ses={_sessionId}&w=search&sort=name&sortAsc=1";
                int previousCount = 0;
                int stableCount = 0;
                const int pollIntervalMs = 1000;
                const int maxTimeoutSeconds = 30;
                
                var pollingStartTime = DateTime.UtcNow;
                int pollCount = 0;
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    var elapsedSeconds = (DateTime.UtcNow - pollingStartTime).TotalSeconds;
                    
                    // Timeout: detener después de 30 segundos
                    if (elapsedSeconds >= maxTimeoutSeconds)
                    {
                        Log($"Timeout de {maxTimeoutSeconds}s alcanzado. Resultados finales: {previousCount}");
                        break;
                    }
                    
                    var response = await _httpClient.GetAsync(resultsUrl, cancellationToken);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        Log($"Error obteniendo resultados: {response.StatusCode}");
                        return;
                    }

                    var html = await response.Content.ReadAsStringAsync();
                    var ed2kMatches = System.Text.RegularExpressions.Regex.Matches(
                        html, 
                        @"onMouseover=""searchmenu\(event,'(ed2k://\|file\|[^']+)'\)""",
                        System.Text.RegularExpressions.RegexOptions.Singleline
                    );
                    
                    int currentCount = ed2kMatches.Count;
                    pollCount++;
                    
                    if (pollCount == 1)
                    {
                        Log($"Poll {pollCount}: {currentCount} resultados");
                    }
                    else if (currentCount != previousCount)
                    {
                        Log($"Poll {pollCount}: {currentCount} resultados (+{currentCount - previousCount})");
                        stableCount = 0;
                    }
                    else
                    {
                        stableCount++;
                        if (stableCount >= 2)
                        {
                            Log($"Resultados estables en {currentCount} después de {pollCount} polls ({elapsedSeconds:F1}s)");
                            break;
                        }
                    }
                    
                    previousCount = currentCount;
                    await Task.Delay(pollIntervalMs, cancellationToken);
                }
                
                // Última petición para obtener los resultados finales
                var finalResponse = await _httpClient.GetAsync(resultsUrl, cancellationToken);
                if (!finalResponse.IsSuccessStatusCode)
                {
                    Log($"Error obteniendo resultados finales: {finalResponse.StatusCode}");
                    return;
                }

                var finalHtml = await finalResponse.Content.ReadAsStringAsync();
                var finalMatches = System.Text.RegularExpressions.Regex.Matches(
                    finalHtml, 
                    @"onMouseover=""searchmenu\(event,'(ed2k://\|file\|[^']+)'\)""",
                    System.Text.RegularExpressions.RegexOptions.Singleline
                );
                
                Log($"Enlaces ed2k encontrados: {finalMatches.Count}");
                
                if (finalMatches.Count == 0)
                {
                    Log("No se encontraron resultados");
                    return;
                }
                
                int newResultsInPage = 0;
                
                foreach (System.Text.RegularExpressions.Match match in finalMatches)
                {
                    var ed2kLink = match.Groups[1].Value;
                    
                    var ed2kParts = System.Text.RegularExpressions.Regex.Match(
                        ed2kLink, 
                        @"ed2k://\|file\|([^|]+)\|(\d+)\|([A-F0-9]+)\|"
                    );
                    
                    if (ed2kParts.Success)
                    {
                        var fileName = System.Web.HttpUtility.HtmlDecode(ed2kParts.Groups[1].Value);
                        var fileSize = long.Parse(ed2kParts.Groups[2].Value);
                        var fileHash = ed2kParts.Groups[3].Value.ToUpperInvariant();
                        
                        // Solo añadir si no es duplicado en esta búsqueda
                        if (seenHashes.Add(fileHash))
                        {
                            allResults.Add(new EmuleSearchResult
                            {
                                FileName = fileName,
                                FileSize = fileSize,
                                FileHash = fileHash,
                                FileType = Path.GetExtension(fileName)?.TrimStart('.') ?? "unknown",
                                SourceCount = 1,
                                CompleteSourceCount = 1
                            });
                            newResultsInPage++;
                        }
                    }
                }
                
                Log($"Búsqueda eMule completada: {allResults.Count} resultados únicos");

                if (allResults.Count > 0)
                {
                    SearchResultsReceived?.Invoke(this, new EmuleSearchResultsEventArgs
                    {
                        SearchId = searchId,
                        Results = allResults
                    });
                }
                else
                {
                    Log("No se encontraron resultados para parsear");
                }
            }
            catch (Exception ex)
            {
                Log($"Error en ParseSearchResultsFromHtml: {ex.Message}");
            }
        }

        private EmuleSearchResult ParseSearchResultRow(string rowHtml)
        {
            // Extraer nombre del archivo
            var fileName = ExtractBetween(rowHtml, "ed2k://|file|", "|");
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            // Decodificar URL
            fileName = Uri.UnescapeDataString(fileName);

            // Extraer tamaño (segundo parámetro del ed2k link)
            var sizeStr = ExtractBetween(rowHtml, $"|{Uri.EscapeDataString(fileName)}|", "|");
            if (!long.TryParse(sizeStr, out var fileSize))
            {
                fileSize = 0;
            }

            // Extraer hash (tercer parámetro del ed2k link)
            var hash = ExtractBetween(rowHtml, $"|{sizeStr}|", "|");

            // Extraer número de fuentes (buscar en las celdas <td>)
            var sourcesStr = ExtractBetween(rowHtml, "sources\">", "</td>");
            if (!int.TryParse(sourcesStr?.Trim(), out var sources))
            {
                sources = 1;
            }

            return new EmuleSearchResult
            {
                FileHash = hash ?? GenerateRandomHash(),
                FileName = fileName,
                FileSize = fileSize,
                SourceCount = sources,
                CompleteSourceCount = sources > 0 ? Math.Max(1, sources / 2) : 0,
                FileType = Path.GetExtension(fileName)?.TrimStart('.') ?? "unknown"
            };
        }

        private string ExtractBetween(string text, string start, string end)
        {
            var startIndex = text.IndexOf(start);
            if (startIndex < 0)
            {
                return null;
            }

            startIndex += start.Length;
            var endIndex = text.IndexOf(end, startIndex);
            if (endIndex < 0)
            {
                return null;
            }

            return text.Substring(startIndex, endIndex - startIndex);
        }

        private void SimulateSearchResults(string searchId, string query)
        {
            // Método mantenido para compatibilidad/fallback
            var results = new List<EmuleSearchResult>
            {
                new EmuleSearchResult
                {
                    FileHash = GenerateRandomHash(),
                    FileName = $"{query} - Resultado eMule 1.epub",
                    FileSize = 1024000 + new Random().Next(1000000),
                    SourceCount = new Random().Next(1, 50),
                    CompleteSourceCount = new Random().Next(1, 20),
                    FileType = "epub"
                },
                new EmuleSearchResult
                {
                    FileHash = GenerateRandomHash(),
                    FileName = $"{query} - Resultado eMule 2.pdf",
                    FileSize = 2048000 + new Random().Next(1000000),
                    SourceCount = new Random().Next(1, 50),
                    CompleteSourceCount = new Random().Next(1, 20),
                    FileType = "pdf"
                }
            };

            SearchResultsReceived?.Invoke(this, new EmuleSearchResultsEventArgs
            {
                SearchId = searchId,
                Results = results
            });
        }

        private string GenerateRandomHash()
        {
            var random = new Random();
            var hash = new byte[16];
            random.NextBytes(hash);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private void ChangeState(EmuleConnectionState newState, string message)
        {
            var previousState = _state;
            _state = newState;

            StateChanged?.Invoke(this, new EmuleConnectionStateChangedEventArgs
            {
                PreviousState = previousState,
                CurrentState = newState,
                Message = message
            });
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
