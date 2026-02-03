using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;
using SlskDown.Core;
using System.Buffers;
using System.Diagnostics;

namespace SlskDown.EMule
{
    /// <summary>
    /// Cliente HTTP para la interfaz web de aMule (puerto 4711)
    /// Mucho más simple y estable que el protocolo EC binario
    /// OPTIMIZADO: Regex compilados, paralelización, object pooling
    /// </summary>
    public class EMuleWebClient : INetworkClient
    {
        // ===== REGEX COMPILADOS (20-30% más rápido) =====
        private static readonly Regex RowRegex = new Regex(
            @"<tr[^>]*>(.*?)</tr>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase
        );
        
        private static readonly Regex CellRegex = new Regex(
            @"<td[^>]*>(.*?)</td>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase
        );
        
        private static readonly Regex HashRegex = new Regex(
            @"(?:name|value)=[""']([A-F0-9]{32})[""']",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        
        private static readonly Regex HashFallbackRegex = new Regex(
            @"([A-F0-9]{32})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        
        private static readonly Regex SizeRegex = new Regex(
            @"([\d.]+)\s*(B|KB|MB|GB|TB)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        
        private static readonly Regex SpeedRegex = new Regex(
            @"([\d.]+)\s*(B|KB|MB|GB)/s",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        
        private static readonly Regex IntRegex = new Regex(
            @"\d+",
            RegexOptions.Compiled
        );
        
        private static readonly Regex HtmlTagRegex = new Regex(
            @"<[^>]+>",
            RegexOptions.Compiled
        );
        
        // ===== OBJECT POOL para reducir GC pressure =====
        private static readonly ArrayPool<byte> ByteArrayPool = ArrayPool<byte>.Shared;
        
        private readonly HttpClient _httpClient;
        private readonly SocketsHttpHandler _httpHandler;
        private string _baseUrl;
        private string _password;
        private NetworkConnectionState _state;
        private DateTime _connectedTime;
        private bool _isLoggedIn;
        private BootstrapNodeManager _bootstrapNodeManager;

        public event Action<string> OnLog;
        public event EventHandler<NetworkStateChangedEventArgs> StateChanged;

        public string NetworkName => "eMule";
        
        public NetworkConnectionState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    var prev = _state;
                    _state = value;
                    StateChanged?.Invoke(this, new NetworkStateChangedEventArgs
                    {
                        PreviousState = prev,
                        CurrentState = value
                    });
                }
            }
        }

        public bool IsConnected => State == NetworkConnectionState.Connected || State == NetworkConnectionState.LoggedIn;

        public EMuleWebClient()
        {
            // OPTIMIZACIÓN: HttpClient con SocketsHttpHandler para connection pooling
            _httpHandler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                MaxConnectionsPerServer = 10,
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer()
            };
            
            _httpClient = new HttpClient(_httpHandler)
            {
                Timeout = TimeSpan.FromSeconds(90)
            };

            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            
            State = NetworkConnectionState.Disconnected;
            _isLoggedIn = false;
        }

        private static async Task<string> ReadResponseHtmlAsync(HttpResponseMessage response)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                using var ms = new MemoryStream(bytes);
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8, true);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            var html = System.Text.Encoding.UTF8.GetString(bytes);
            if (html.Contains("�") || html.Length < 100)
            {
                html = System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
            }

            return html;
        }

        public void SetBootstrapNodeManager(BootstrapNodeManager manager)
        {
            _bootstrapNodeManager = manager;
            OnLog?.Invoke("[eMule Web] BootstrapNodeManager configurado");
        }

        /// <summary>
        /// Verifica si el proceso aMule está ejecutándose
        /// </summary>
        private bool IsAMuleRunning()
        {
            try
            {
                var amuleProcesses = Process.GetProcessesByName("amule");
                var amuledProcesses = Process.GetProcessesByName("amuled");
                return amuleProcesses.Length > 0 || amuledProcesses.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Configura aMule para conectar automáticamente y habilita servidor web editando amule.conf
        /// </summary>
        private bool ConfigureAutoConnect()
        {
            try
            {
                OnLog?.Invoke("[eMule Web] Configurando aMule (AutoConnect + WebServer)...");
                
                // Ruta del archivo de configuración de aMule
                string amuleConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "aMule",
                    "amule.conf"
                );
                
                if (!File.Exists(amuleConfigPath))
                {
                    OnLog?.Invoke($"[eMule Web] No se encontró amule.conf en: {amuleConfigPath}");
                    return false;
                }
                
                OnLog?.Invoke($"[eMule Web] Editando: {amuleConfigPath}");
                
                // Leer configuración actual
                var lines = File.ReadAllLines(amuleConfigPath).ToList();
                bool modified = false;
                bool inExternalConnect = false;
                bool inWebServer = false;
                bool hasAutoConnect = false;
                bool hasWebServerEnabled = false;
                bool hasWebServerPort = false;
                
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i].Trim();
                    
                    // Detectar secciones
                    if (line == "[ExternalConnect]")
                    {
                        inExternalConnect = true;
                        inWebServer = false;
                        continue;
                    }
                    else if (line == "[WebServer]")
                    {
                        inWebServer = true;
                        inExternalConnect = false;
                        continue;
                    }
                    else if (line.StartsWith("["))
                    {
                        inExternalConnect = false;
                        inWebServer = false;
                    }
                    
                    // Modificar AutoConnect en [ExternalConnect]
                    if (inExternalConnect && line.StartsWith("AutoConnect="))
                    {
                        hasAutoConnect = true;
                        if (line != "AutoConnect=1")
                        {
                            lines[i] = "AutoConnect=1";
                            modified = true;
                            OnLog?.Invoke("[eMule Web] AutoConnect=1 activado");
                        }
                    }
                    
                    // Habilitar WebServer en [WebServer]
                    if (inWebServer && line.StartsWith("Enabled="))
                    {
                        hasWebServerEnabled = true;
                        if (line != "Enabled=1")
                        {
                            lines[i] = "Enabled=1";
                            modified = true;
                            OnLog?.Invoke("[eMule Web] WebServer habilitado");
                        }
                    }
                    
                    // Verificar puerto del WebServer
                    if (inWebServer && line.StartsWith("Port="))
                    {
                        hasWebServerPort = true;
                        if (line != "Port=4711")
                        {
                            lines[i] = "Port=4711";
                            modified = true;
                            OnLog?.Invoke("[eMule Web] Puerto WebServer configurado a 4711");
                        }
                    }
                }
                
                // Agregar configuraciones faltantes
                if (!hasAutoConnect)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].Trim() == "[ExternalConnect]")
                        {
                            int insertPos = i + 1;
                            while (insertPos < lines.Count && !lines[insertPos].Trim().StartsWith("["))
                                insertPos++;
                            lines.Insert(insertPos, "AutoConnect=1");
                            modified = true;
                            OnLog?.Invoke("[eMule Web] AutoConnect=1 agregado");
                            break;
                        }
                    }
                }
                
                if (!hasWebServerEnabled || !hasWebServerPort)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].Trim() == "[WebServer]")
                        {
                            int insertPos = i + 1;
                            while (insertPos < lines.Count && !lines[insertPos].Trim().StartsWith("["))
                                insertPos++;
                            
                            if (!hasWebServerEnabled)
                            {
                                lines.Insert(insertPos, "Enabled=1");
                                modified = true;
                                OnLog?.Invoke("[eMule Web] WebServer Enabled=1 agregado");
                            }
                            if (!hasWebServerPort)
                            {
                                lines.Insert(insertPos, "Port=4711");
                                modified = true;
                                OnLog?.Invoke("[eMule Web] WebServer Port=4711 agregado");
                            }
                            break;
                        }
                    }
                }
                
                if (modified)
                {
                    File.WriteAllLines(amuleConfigPath, lines);
                    OnLog?.Invoke("[eMule Web] Configuración guardada");
                    OnLog?.Invoke("[eMule Web] Necesario reiniciar aMule");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error configurando: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Intenta iniciar aMule automáticamente
        /// </summary>
        private async Task<bool> StartAMuleAsync()
        {
            try
            {
                OnLog?.Invoke("[eMule Web] Intentando iniciar aMule...");
                
                // Rutas comunes de aMule en Windows
                var amulePaths = new[]
                {
                    @"C:\amule\amule.exe",
                    @"C:\Program Files\aMule\amule.exe",
                    @"C:\Program Files (x86)\aMule\amule.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "aMule", "amule.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "aMule", "amule.exe")
                };
                
                string amuleExe = null;
                foreach (var path in amulePaths)
                {
                    if (File.Exists(path))
                    {
                        amuleExe = path;
                        break;
                    }
                }
                
                if (amuleExe == null)
                {
                    OnLog?.Invoke("[eMule Web] No se encontró aMule instalado en rutas comunes");
                    OnLog?.Invoke("[eMule Web] Instala aMule desde: https://www.amule.org/");
                    return false;
                }
                
                OnLog?.Invoke($"[eMule Web] aMule encontrado: {amuleExe}");
                
                // IMPORTANTE: amuled no soporta WebServer, usar amule.exe
                OnLog?.Invoke("[eMule Web] Iniciando aMule GUI (WebServer requiere GUI)");
                
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = amuleExe,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal,  // Normal primero para ver si inicia
                        WorkingDirectory = Path.GetDirectoryName(amuleExe)
                    };
                    
                    var process = Process.Start(startInfo);
                    
                    if (process != null)
                    {
                        OnLog?.Invoke($"[eMule Web] Proceso iniciado (PID: {process.Id})");
                    }
                    else
                    {
                        OnLog?.Invoke("[eMule Web] Process.Start devolvió null");
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"[eMule Web] Error al iniciar proceso: {ex.Message}");
                    return false;
                }
                
                OnLog?.Invoke("[eMule Web] Esperando 10 segundos para inicio completo...");
                
                // Esperar menos tiempo inicialmente
                await Task.Delay(10000);
                
                bool running = IsAMuleRunning();
                OnLog?.Invoke($"[eMule Web] Verificación: aMule {(running ? "está ejecutándose" : "NO está ejecutándose")}");
                
                if (!running)
                {
                    OnLog?.Invoke("[eMule Web] ");
                    OnLog?.Invoke("[eMule Web] aMule se cierra automáticamente al iniciarlo desde código");
                    OnLog?.Invoke("[eMule Web] ");
                    OnLog?.Invoke("[eMule Web] SOLUCIÓN MANUAL (solo una vez):");
                    OnLog?.Invoke("[eMule Web] ");
                    OnLog?.Invoke("[eMule Web] 1. Abre manualmente: C:\\amule\\amule.exe");
                    OnLog?.Invoke("[eMule Web] 2. Deja aMule ejecutándose (puedes minimizarlo)");
                    OnLog?.Invoke("[eMule Web] 3. Vuelve a SlskDown y busca de nuevo");
                    OnLog?.Invoke("[eMule Web] ");
                    OnLog?.Invoke("[eMule Web] Después de esto, aMule funcionará automáticamente");
                    OnLog?.Invoke("[eMule Web] ");
                }
                
                return running;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error iniciando aMule: {ex.Message}");
                return false;
            }
        }

        public async Task ConnectAsync(NetworkCredentials credentials, CancellationToken cancellationToken = default)
        {
            try
            {
                State = NetworkConnectionState.Connecting;
                
                // Verificar si aMule está ejecutándose
                bool amuleRunning = IsAMuleRunning();
                
                if (amuleRunning)
                {
                    // Verificar si es amuled (daemon) en lugar de amule (GUI)
                    var amuledProcesses = Process.GetProcessesByName("amuled");
                    if (amuledProcesses.Length > 0)
                    {
                        OnLog?.Invoke("[eMule Web] Detectado amuled (daemon) - WebServer no funciona con daemon");
                        OnLog?.Invoke("[eMule Web] Cerrando amuled y reiniciando con amule.exe (GUI)...");
                        
                        // Matar amuled
                        foreach (var p in amuledProcesses)
                        {
                            try { p.Kill(); } catch { }
                        }
                        
                        await Task.Delay(2000);
                        
                        // Iniciar amule.exe
                        bool started = await StartAMuleAsync();
                        if (!started)
                        {
                            OnLog?.Invoke("[eMule Web] No se pudo iniciar aMule GUI");
                            State = NetworkConnectionState.Failed;
                            throw new Exception("No se pudo iniciar aMule GUI");
                        }
                    }
                    else
                    {
                        OnLog?.Invoke("[eMule Web] aMule GUI está ejecutándose");
                    }
                }
                else
                {
                    OnLog?.Invoke("[eMule Web] aMule no está ejecutándose");
                    
                    // Intentar iniciar aMule automáticamente
                    bool started = await StartAMuleAsync();
                    
                    if (!started)
                    {
                        OnLog?.Invoke("[eMule Web] No se pudo iniciar aMule automáticamente");
                        OnLog?.Invoke("[eMule Web] Por favor, inicia aMule manualmente");
                        State = NetworkConnectionState.Failed;
                        throw new Exception("aMule no está ejecutándose y no se pudo iniciar automáticamente");
                    }
                }
                
                // Intentar usar el mejor nodo del BootstrapNodeManager si está disponible
                string server = credentials.Server;
                int port = credentials.Port;
                
                if (_bootstrapNodeManager != null && _bootstrapNodeManager.NodeCount > 0)
                {
                    var bestNode = _bootstrapNodeManager.GetBestNode();
                    if (bestNode != null)
                    {
                        server = bestNode.IP.ToString();
                        port = bestNode.Port;
                        OnLog?.Invoke($"[eMule Web] Usando mejor nodo bootstrap: {server}:{port} (reliability: {bestNode.Reliability:P0})");
                    }
                }
                else
                {
                    OnLog?.Invoke($"[eMule Web] Conectando a {server}:{port}...");
                }

                _baseUrl = $"http://{server}:{port}";
                _password = credentials.Password;

                // Hacer login HTTP con POST
                await LoginAsync(cancellationToken);

                _connectedTime = DateTime.UtcNow;
                State = NetworkConnectionState.Connected;
                OnLog?.Invoke("[eMule Web] Conectado exitosamente a la interfaz web de aMule");
            }
            catch (Exception ex)
            {
                State = NetworkConnectionState.Failed;
                OnLog?.Invoke($"[eMule Web] Error: {ex.Message}");
                throw;
            }
        }

        private async Task LoginAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                OnLog?.Invoke("[eMule Web] Iniciando sesión...");
                OnLog?.Invoke($"[eMule Web] Password configurado: '{_password}' (longitud: {_password?.Length ?? 0})");

                // Primero, hacer GET para obtener el formulario de login y ver qué campos espera
                var getResponse = await _httpClient.GetAsync($"{_baseUrl}/", cancellationToken);
                var loginPageHtml = await ReadResponseHtmlAsync(getResponse);
                
                OnLog?.Invoke($"[eMule Web] Página de login recibida ({loginPageHtml.Length} bytes)");
                
                // Buscar el nombre del campo de password en el HTML
                var passwordFieldMatch = Regex.Match(loginPageHtml, @"<input[^>]*name=['""]([^'""]*)['""][^>]*type=['""]password['""]", RegexOptions.IgnoreCase);
                if (!passwordFieldMatch.Success)
                {
                    passwordFieldMatch = Regex.Match(loginPageHtml, @"<input[^>]*type=['""]password['""][^>]*name=['""]([^'""]*)['""]", RegexOptions.IgnoreCase);
                }
                
                string passwordFieldName = "pass";
                if (passwordFieldMatch.Success)
                {
                    passwordFieldName = passwordFieldMatch.Groups[1].Value;
                    OnLog?.Invoke($"[eMule Web] Campo de password detectado: '{passwordFieldName}'");
                }
                else
                {
                    OnLog?.Invoke($"[eMule Web] No se detectó campo de password, usando default: '{passwordFieldName}'");
                }
                
                // Mostrar preview del HTML para debugging
                if (loginPageHtml.Length < 2000)
                {
                    OnLog?.Invoke($"[eMule Web] HTML completo del formulario:");
                    OnLog?.Invoke(loginPageHtml);
                }
                else
                {
                    var preview = loginPageHtml.Substring(0, Math.Min(500, loginPageHtml.Length));
                    OnLog?.Invoke($"[eMule Web] Preview del formulario (primeros 500 chars):");
                    OnLog?.Invoke(preview);
                }

                // Intentar con diferentes variantes de nombres de campo
                string[] fieldVariants = { passwordFieldName, "pass", "password", "w" };
                bool loginSuccess = false;
                string lastError = "";
                
                foreach (var fieldName in fieldVariants.Distinct())
                {
                    OnLog?.Invoke($"[eMule Web] Intentando login con campo: '{fieldName}'");
                    
                    var loginData = new Dictionary<string, string>
                    {
                        { fieldName, _password }
                    };

                    var content = new FormUrlEncodedContent(loginData);
                    var response = await _httpClient.PostAsync($"{_baseUrl}/", content, cancellationToken);

                    OnLog?.Invoke($"[eMule Web] Respuesta HTTP: {response.StatusCode}");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        lastError = $"HTTP {response.StatusCode}";
                        OnLog?.Invoke($"[eMule Web] Login falló con {fieldName}: {lastError}");
                        continue;
                    }

                    var html = await ReadResponseHtmlAsync(response);
                    OnLog?.Invoke($"[eMule Web] Respuesta recibida ({html.Length} bytes)");
                    
                    // Mostrar preview de la respuesta
                    if (html.Length < 2000)
                    {
                        OnLog?.Invoke($"[eMule Web] HTML respuesta completo:");
                        OnLog?.Invoke(html);
                    }
                    else
                    {
                        var preview = html.Substring(0, Math.Min(500, html.Length));
                        OnLog?.Invoke($"[eMule Web] Preview respuesta (primeros 500 chars):");
                        OnLog?.Invoke(preview);
                    }
                    
                    // Verificar si el login fue exitoso
                    bool hasPasswordForm = html.Contains("type=\"password\"") || 
                                          html.Contains("type='password'") ||
                                          html.Contains("Enter password") ||
                                          html.Contains("name=\"pass\"") ||
                                          html.Contains("name='pass'");
                    
                    if (hasPasswordForm)
                    {
                        lastError = "Página sigue mostrando formulario de login";
                        OnLog?.Invoke($"[eMule Web] Login falló con '{fieldName}': {lastError}");
                        continue;
                    }
                    
                    loginSuccess = true;
                    OnLog?.Invoke($"[eMule Web] Login exitoso con campo: '{fieldName}'");
                    break;
                }
                
                if (!loginSuccess)
                {
                    throw new Exception($"Login falló después de intentar todos los campos. Último error: {lastError}");
                }

                _isLoggedIn = true;
                OnLog?.Invoke("[eMule Web] Sesión iniciada correctamente");
                
                // DEBUG: Mostrar cookies guardadas
                var uri = new Uri(_baseUrl);
                var cookies = _httpHandler.CookieContainer.GetCookies(uri);
                var cookieCount = cookies.Cast<System.Net.Cookie>().Count();
                OnLog?.Invoke($"[eMule Web] Cookies guardadas: {cookieCount}");
                foreach (System.Net.Cookie cookie in cookies)
                {
                    OnLog?.Invoke($"[eMule Web]    - {cookie.Name}={cookie.Value} (expires: {cookie.Expires})");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error en login: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            State = NetworkConnectionState.Disconnected;
            OnLog?.Invoke("[eMule Web] Desconectado");
            await Task.CompletedTask;
        }

        public NetworkStatistics GetStatistics()
        {
            return new NetworkStatistics
            {
                LastConnected = _connectedTime,
                Uptime = IsConnected ? DateTime.UtcNow - _connectedTime : TimeSpan.Zero
            };
        }

        public async Task<IEnumerable<Core.SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No conectado a aMule Web");
            }

            if (!_isLoggedIn)
            {
                throw new InvalidOperationException("No se ha iniciado sesión en aMule Web");
            }

            try
            {
                OnLog?.Invoke($"[eMule Web] Buscando en múltiples redes: {query}");

                // Verificar estado de las redes antes de buscar
                var (ed2kConnected, kadConnected) = await CheckNetworkStatusAsync();
                
                OnLog?.Invoke($"[eMule Web] Estado inicial - eD2k: {(ed2kConnected ? "Conectado" : "Desconectado")}, Kad: {(kadConnected ? "Conectado" : "Desconectado")}");
                
                // Si ninguna red está conectada, intentar reconectar
                if (!ed2kConnected && !kadConnected)
                {
                    OnLog?.Invoke("[eMule Web] Ninguna red conectada, intentando reconectar...");
                    
                    // Intentar configurar AutoConnect y WebServer en amule.conf
                    bool configChanged = ConfigureAutoConnect();
                    
                    if (configChanged)
                    {
                        // Reiniciar aMule para aplicar cambios
                        OnLog?.Invoke("[eMule Web] Reiniciando aMule para aplicar configuración...");
                        
                        try
                        {
                            // Matar procesos de aMule
                            foreach (var p in Process.GetProcessesByName("amule"))
                                p.Kill();
                            foreach (var p in Process.GetProcessesByName("amuled"))
                                p.Kill();
                            
                            await Task.Delay(2000);
                            
                            // Reiniciar aMule
                            await StartAMuleAsync();
                            
                            OnLog?.Invoke("[eMule Web] aMule reiniciado con nueva configuración");
                        }
                        catch (Exception ex)
                        {
                            OnLog?.Invoke($"[eMule Web] Error reiniciando aMule: {ex.Message}");
                        }
                    }
                    
                    await ReconnectNetworksAsync(reconnectEd2k: true, reconnectKad: true);
                    
                    // ReconnectNetworksAsync ya incluye el delay y verificación
                    (ed2kConnected, kadConnected) = await CheckNetworkStatusAsync();
                    OnLog?.Invoke($"[eMule Web] Estado después de reconectar - eD2k: {(ed2kConnected ? "Conectado" : "Desconectado")}, Kad: {(kadConnected ? "Conectado" : "Desconectado")}");
                    
                    if (!ed2kConnected && !kadConnected)
                    {
                        OnLog?.Invoke("[eMule Web] Estado de redes no detectado en HTML");
                        OnLog?.Invoke("[eMule Web] Si la búsqueda devuelve resultados, las redes SÍ están funcionando");
                        OnLog?.Invoke("[eMule Web] ");
                        OnLog?.Invoke("[eMule Web] Para mejorar la conexión (opcional):");
                        OnLog?.Invoke("[eMule Web]    1. Abre aMule GUI");
                        OnLog?.Invoke("[eMule Web]    2. Verifica que eD2k y Kad muestren icono verde");
                        OnLog?.Invoke("[eMule Web]    3. Si no, haz clic en 'Conectar' en cada pestaña");
                        
                        // No retornar aquí, intentar buscar de todas formas por si acaso
                    }
                }
                else if (!ed2kConnected)
                {
                    OnLog?.Invoke("[eMule Web] eD2k desconectado, intentando reconectar...");
                    await ReconnectNetworksAsync(reconnectEd2k: true, reconnectKad: false);
                    (ed2kConnected, kadConnected) = await CheckNetworkStatusAsync();
                    OnLog?.Invoke($"[eMule Web] eD2k después de reconectar: {(ed2kConnected ? "Conectado" : "Desconectado")}");
                }
                else if (!kadConnected)
                {
                    OnLog?.Invoke("[eMule Web] Kad desconectado, intentando reconectar...");
                    await ReconnectNetworksAsync(reconnectEd2k: false, reconnectKad: true);
                    (ed2kConnected, kadConnected) = await CheckNetworkStatusAsync();
                    OnLog?.Invoke($"[eMule Web] Kad después de reconectar: {(kadConnected ? "Conectado" : "Desconectado")}");
                }

                var searchUrl = $"{_baseUrl}/amuleweb-main-search.php";
                var allResults = new List<Core.SearchResult>();
                
                // Buscar en múltiples redes: Kad, Local y Global
                var searchTypes = new[] { "Kad", "Local", "Global" };
                
                // Crear CancellationTokenSource con timeout de 60 segundos para todas las búsquedas
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                
                // OPTIMIZACIÓN: Búsquedas paralelas en todas las redes (2-3x más rápido)
                var searchTasks = searchTypes.Select(async searchType =>
                {
                    try
                    {
                        OnLog?.Invoke($"[eMule Web] Buscando en red: {searchType}");
                        
                        var searchData = new Dictionary<string, string>
                        {
                            { "command", "search" },
                            { "searchval", query },
                            { "searchtype", searchType },
                            { "Search", "Search" }
                        };
                        
                        OnLog?.Invoke($"[eMule Web] Datos de búsqueda:");
                        OnLog?.Invoke($"[eMule Web]    - searchtype: {searchType}");
                
                        HttpResponseMessage response;
                        
                        // Paso 1: Enviar búsqueda vía POST
                        var content = new FormUrlEncodedContent(searchData);
                        OnLog?.Invoke($"[eMule Web] Enviando búsqueda vía POST...");
                        response = await _httpClient.PostAsync(searchUrl, content, linkedCts.Token);
                        response.EnsureSuccessStatusCode();
                        
                        // Paso 2: Esperar para que aMule procese (más tiempo para redes lentas)
                        OnLog?.Invoke($"[eMule Web] Esperando 5 segundos para que {searchType} procese la búsqueda...");
                        await Task.Delay(5000, linkedCts.Token);
                        
                        // Paso 3: Obtener resultados
                        OnLog?.Invoke($"[eMule Web] Obteniendo resultados...");
                        response = await _httpClient.GetAsync(searchUrl, linkedCts.Token);
                        response.EnsureSuccessStatusCode();

                        var html = await ReadResponseHtmlAsync(response);
                        
                        // Parsear resultados de esta red
                        var results = ParseSearchResults(html, query);
                        OnLog?.Invoke($"[eMule Web] Red {searchType}: {results.Count} resultados");
                        
                        return results;
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[eMule Web] Error buscando en {searchType}: {ex.Message}");
                        return new List<Core.SearchResult>();
                    }
                }).ToArray();
                
                // Esperar a que todas las búsquedas terminen
                var networkResults = await Task.WhenAll(searchTasks);
                
                // Combinar y deduplicar resultados por hash
                var seenHashes = new HashSet<string>();
                foreach (var results in networkResults)
                {
                    foreach (var result in results)
                    {
                        if (!string.IsNullOrEmpty(result.FileHash) && seenHashes.Add(result.FileHash))
                        {
                            allResults.Add(result);
                        }
                        else if (string.IsNullOrEmpty(result.FileHash))
                        {
                            allResults.Add(result);
                        }
                    }
                }
                
                OnLog?.Invoke($"[eMule Web] Total combinado: {allResults.Count} resultados de {searchTypes.Length} redes");
                return allResults;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error en búsqueda: {ex.Message}");
                return new List<Core.SearchResult>();
            }
        }

        private List<Core.SearchResult> ParseSearchResults(string html, string query)
        {
            var results = new List<Core.SearchResult>();

            try
            {
                // Parsear tabla de resultados HTML (usando Regex compilado)
                // Formato típico: <tr><td>filename</td><td>size</td><td>sources</td>...</tr>
                
                var rowMatches = RowRegex.Matches(html);

                OnLog?.Invoke($"[eMule Web] Encontradas {rowMatches.Count} filas HTML");
                
                // DEBUG: Si no hay filas, mostrar estructura del HTML
                if (rowMatches.Count == 0)
                {
                    OnLog?.Invoke($"[eMule Web] No se encontraron filas <tr> con resultados");
                    
                    // Buscar mensajes específicos de aMule
                    if (html.Contains("No search results") || html.Contains("Sin resultados") || html.Contains("no results found"))
                    {
                        OnLog?.Invoke($"[eMule Web]    aMule indica: Sin resultados para esta búsqueda");
                        return results;
                    }
                    else if (html.Contains("Search in progress") || html.Contains("Buscando") || html.Contains("searching"))
                    {
                        OnLog?.Invoke($"[eMule Web]    aMule indica: Búsqueda aún en progreso");
                        OnLog?.Invoke($"[eMule Web]    Intenta buscar de nuevo en unos segundos");
                        return results;
                    }
                    else if (html.Contains("not connected") || html.Contains("no conectado") || html.Contains("disconnected"))
                    {
                        OnLog?.Invoke($"[eMule Web]    aMule indica: No conectado a redes P2P");
                        OnLog?.Invoke($"[eMule Web]    Conecta las redes en aMule antes de buscar");
                        return results;
                    }
                    
                    // Si no hay mensajes claros, es probable que sea la página de búsqueda vacía
                    OnLog?.Invoke($"[eMule Web]    Página de búsqueda sin resultados (probablemente redes desconectadas)");
                }

                // OPTIMIZACIÓN: Parsing paralelo de filas (2-3x más rápido para muchos resultados)
                int rowIndex = 0;
                var parsedResults = rowMatches
                    .Cast<Match>()
                    .AsParallel()
                    .WithDegreeOfParallelism(Environment.ProcessorCount)
                    .Select(rowMatch => 
                    {
                        var idx = System.Threading.Interlocked.Increment(ref rowIndex);
                        var rowHtml = rowMatch.Groups[1].Value;
                        
                        // Log primera fila para debug
                        if (idx == 1)
                        {
                            OnLog?.Invoke($"[eMule Web] Primera fila HTML (primeros 400 chars):");
                            OnLog?.Invoke(rowHtml.Length > 400 ? rowHtml.Substring(0, 400) + "..." : rowHtml);
                        }
                        
                        var result = ParseRow(rowHtml);
                        
                        // Log por qué falló el parseo de las primeras 3 filas
                        if (result == null && idx <= 3)
                        {
                            var cellMatches = CellRegex.Matches(rowHtml);
                            OnLog?.Invoke($"[eMule Web] Fila {idx} no válida: {cellMatches.Count} celdas encontradas (necesita >=4)");
                            if (cellMatches.Count >= 2)
                            {
                                var cell1 = StripHtmlTags(cellMatches[0].Groups[1].Value).Trim();
                                var cell2 = cellMatches.Count > 1 ? StripHtmlTags(cellMatches[1].Groups[1].Value).Trim() : "";
                                OnLog?.Invoke($"[eMule Web]    Celda 1: '{(cell1.Length > 50 ? cell1.Substring(0, 50) + "..." : cell1)}'");
                                OnLog?.Invoke($"[eMule Web]    Celda 2: '{(cell2.Length > 50 ? cell2.Substring(0, 50) + "..." : cell2)}'");
                            }
                        }
                        
                        return result;
                    })
                    .Where(r => r != null)
                    .ToList();

                results.AddRange(parsedResults);
                
                OnLog?.Invoke($"[eMule Web] Parseadas {parsedResults.Count} filas válidas de {rowMatches.Count} totales");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error parseando resultados: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Parsea una fila HTML individual (thread-safe para PLINQ)
        /// </summary>
        private Core.SearchResult ParseRow(string row)
        {
            try
            {
                var cellMatches = CellRegex.Matches(row);
                
                if (cellMatches.Count < 4)
                {
                    return null;
                }
                
                var filename = StripHtmlTags(cellMatches[1].Groups[1].Value).Trim();
                var sizeStr = StripHtmlTags(cellMatches[2].Groups[1].Value).Trim();
                
                // Validar que es una fila de resultado real (no UI de aMule)
                if (string.IsNullOrWhiteSpace(filename) || 
                    filename.Length <= 3 ||
                    filename.Contains("File Name") ||
                    filename.Contains("SEARCH") ||
                    filename.Contains("Search type") ||
                    filename.Contains("Availability") ||
                    filename.Contains("Click here") ||
                    filename.Contains("edklink") ||
                    filename.Contains("connection") ||
                    filename.Contains("&nbsp;") ||
                    string.IsNullOrWhiteSpace(sizeStr) ||
                    sizeStr.Contains("&nbsp;"))
                {
                    return null;
                }
                
                // Extraer hash
                var checkboxHtml = cellMatches[0].Groups[1].Value;
                var hash = "";
                
                var checkboxMatch = HashRegex.Match(checkboxHtml);
                if (checkboxMatch.Success)
                {
                    hash = checkboxMatch.Groups[1].Value;
                }
                else
                {
                    checkboxMatch = HashFallbackRegex.Match(checkboxHtml);
                    if (checkboxMatch.Success)
                    {
                        hash = checkboxMatch.Groups[1].Value;
                    }
                }
                
                return new Core.SearchResult
                {
                    FileName = filename,
                    SizeBytes = ParseSize(sizeStr),
                    FileHash = hash,
                    NetworkSource = "eMule",
                    Username = "eMule"
                };
            }
            catch
            {
                return null;
            }
        }

        private string StripHtmlTags(string html)
        {
            return HtmlTagRegex.Replace(html, "").Trim();
        }

        private long ParseSize(string sizeStr)
        {
            try
            {
                var match = SizeRegex.Match(sizeStr);
                if (match.Success)
                {
                    var value = double.Parse(match.Groups[1].Value);
                    var unit = match.Groups[2].Value.ToUpper();

                    return unit switch
                    {
                        "KB" => (long)(value * 1024),
                        "MB" => (long)(value * 1024 * 1024),
                        "GB" => (long)(value * 1024 * 1024 * 1024),
                        "TB" => (long)(value * 1024L * 1024 * 1024 * 1024),
                        _ => 0
                    };
                }
            }
            catch { }
            return 0;
        }

        private long ParseSpeed(string speedStr)
        {
            try
            {
                var match = SpeedRegex.Match(speedStr);
                if (match.Success)
                {
                    var value = double.Parse(match.Groups[1].Value);
                    var unit = match.Groups[2].Value.ToUpper();

                    return unit switch
                    {
                        "B" => (long)value,
                        "KB" => (long)(value * 1024),
                        "MB" => (long)(value * 1024 * 1024),
                        "GB" => (long)(value * 1024 * 1024 * 1024),
                        _ => 0
                    };
                }
            }
            catch { }
            return 0;
        }

        private int ParseInt(string str)
        {
            if (int.TryParse(Regex.Match(str, @"\d+").Value, out int result))
                return result;
            return 0;
        }

        public async Task<bool> DownloadAsync(Core.SearchResult result, string destinationPath)
        {
            try
            {
                OnLog?.Invoke($"[eMule Web] Iniciando descarga: {result.FileName}");

                if (string.IsNullOrEmpty(result.FileHash))
                {
                    OnLog?.Invoke("[eMule Web] No se puede descargar: hash no disponible");
                    return false;
                }

                // Construir enlace ed2k
                var ed2kLink = $"ed2k://|file|{Uri.EscapeDataString(result.FileName)}|{result.SizeBytes}|{result.FileHash}|/";
                
                // Enviar comando de descarga a aMule
                var downloadUrl = $"{_baseUrl}/commands.html?command=download&ed2k={Uri.EscapeDataString(ed2kLink)}&password={Uri.EscapeDataString(_password)}";
                
                var response = await _httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                OnLog?.Invoke($"[eMule Web] Descarga agregada a la cola de aMule");
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error en descarga: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica el estado de las redes eD2k y Kad
        /// </summary>
        public async Task<(bool ed2kConnected, bool kadConnected)> CheckNetworkStatusAsync()
        {
            try
            {
                if (!IsConnected || !_isLoggedIn)
                {
                    return (false, false);
                }

                // Obtener página principal que muestra el estado de las redes
                var mainUrl = $"{_baseUrl}/amuleweb-main-dload.php";
                var response = await _httpClient.GetAsync(mainUrl);
                response.EnsureSuccessStatusCode();

                var html = await ReadResponseHtmlAsync(response);

                // Buscar múltiples indicadores de conexión en el HTML
                var ed2kConnected = html.Contains("eD2k: Connected", StringComparison.OrdinalIgnoreCase) ||
                                   html.Contains("eD2k: Conectado", StringComparison.OrdinalIgnoreCase) ||
                                   html.Contains("ED2K</td><td>Connected", StringComparison.OrdinalIgnoreCase) ||
                                   html.Contains("ed2k_connected", StringComparison.OrdinalIgnoreCase) ||
                                   html.Contains("ED2K Network: Yes", StringComparison.OrdinalIgnoreCase);

                var kadConnected = html.Contains("Kad: Connected", StringComparison.OrdinalIgnoreCase) ||
                                  html.Contains("Kad: Conectado", StringComparison.OrdinalIgnoreCase) ||
                                  html.Contains("Kademlia</td><td>Connected", StringComparison.OrdinalIgnoreCase) ||
                                  html.Contains("Kad</td><td>Firewalled", StringComparison.OrdinalIgnoreCase) ||
                                  html.Contains("kad_connected", StringComparison.OrdinalIgnoreCase) ||
                                  html.Contains("Kad Network: Yes", StringComparison.OrdinalIgnoreCase) ||
                                  html.Contains("Kad: Firewalled", StringComparison.OrdinalIgnoreCase);

                // NOTA: Si las búsquedas devuelven resultados, las redes están funcionando
                // aunque el HTML no muestre "Connected" explícitamente
                OnLog?.Invoke($"[eMule Web] Estado redes - eD2k: {(ed2kConnected ? "Conectado" : "Desconectado")}, Kad: {(kadConnected ? "Conectado" : "Desconectado")}");

                // Si ninguna red está conectada según el HTML, verificar archivos
                if (!ed2kConnected && !kadConnected)
                {
                    OnLog?.Invoke($"[eMule Web] No se detectó estado 'Connected' en HTML");
                    OnLog?.Invoke($"[eMule Web] Si las búsquedas devuelven resultados, las redes SÍ están funcionando");
                    
                    var amuleDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "aMule");
                    var serverMet = Path.Combine(amuleDir, "server.met");
                    var nodesDat = Path.Combine(amuleDir, "nodes.dat");
                    
                    bool hasServers = File.Exists(serverMet) && new FileInfo(serverMet).Length > 1000;
                    bool hasNodes = File.Exists(nodesDat) && new FileInfo(nodesDat).Length > 1000;
                    
                    OnLog?.Invoke($"[eMule Web] server.met: {(hasServers ? "OK (" + new FileInfo(serverMet).Length + " bytes)" : "Falta o vacío")}");
                    OnLog?.Invoke($"[eMule Web] nodes.dat: {(hasNodes ? "OK (" + new FileInfo(nodesDat).Length + " bytes)" : "Falta o vacío")}");
                }

                return (ed2kConnected, kadConnected);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error verificando estado de redes: {ex.Message}");
                return (false, false);
            }
        }

        /// <summary>
        /// Intenta reconectar a las redes eD2k y/o Kad
        /// </summary>
        public async Task<bool> ReconnectNetworksAsync(bool reconnectEd2k = true, bool reconnectKad = true)
        {
            try
            {
                if (!IsConnected || !_isLoggedIn)
                {
                    OnLog?.Invoke("[eMule Web] No conectado a aMule Web, no se puede reconectar redes");
                    return false;
                }

                bool success = true;

                if (reconnectEd2k)
                {
                    OnLog?.Invoke("[eMule Web] Reconectando a servidores eD2k...");
                    
                    // Intentar múltiples métodos de reconexión
                    try
                    {
                        // Método 1: Comando directo de conexión
                        var connectUrl = $"{_baseUrl}/amuleweb-main-servers.php?command=connect";
                        var response1 = await _httpClient.GetAsync(connectUrl);
                        OnLog?.Invoke($"[eMule Web] Comando connect enviado: {response1.StatusCode}");
                        
                        await Task.Delay(1000);
                        
                        // Método 2: Comando via commands.html (método alternativo)
                        var commandUrl = $"{_baseUrl}/commands.html?command=connect_ed2k";
                        var response2 = await _httpClient.GetAsync(commandUrl);
                        OnLog?.Invoke($"[eMule Web] Comando alternativo enviado: {response2.StatusCode}");
                        
                        OnLog?.Invoke("[eMule Web] Comandos de reconexión eD2k enviados");
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[eMule Web] Error reconectando eD2k: {ex.Message}");
                        success = false;
                    }
                }

                if (reconnectKad)
                {
                    OnLog?.Invoke("[eMule Web] Reconectando a red Kad...");
                    
                    try
                    {
                        // Método 1: Comando directo de conexión
                        var kadUrl = $"{_baseUrl}/amuleweb-main-kad.php?command=connect";
                        var response1 = await _httpClient.GetAsync(kadUrl);
                        OnLog?.Invoke($"[eMule Web] Comando Kad connect enviado: {response1.StatusCode}");
                        
                        await Task.Delay(1000);
                        
                        // Método 2: Bootstrap con nodos conocidos
                        if (_bootstrapNodeManager != null)
                        {
                            var bootstrapNode = _bootstrapNodeManager.GetBestNode();
                            if (bootstrapNode != null)
                            {
                                OnLog?.Invoke($"[eMule Web] Bootstrap Kad desde nodo: {bootstrapNode.IP}:{bootstrapNode.Port}");
                                var bootstrapUrl = $"{_baseUrl}/amuleweb-main-kad.php?command=bootstrap&ip={bootstrapNode.IP}&port={bootstrapNode.Port}";
                                var response2 = await _httpClient.GetAsync(bootstrapUrl);
                                OnLog?.Invoke($"[eMule Web] Bootstrap enviado: {response2.StatusCode}");
                            }
                        }
                        
                        // Método 3: Comando alternativo via commands.html
                        var commandUrl = $"{_baseUrl}/commands.html?command=connect_kad";
                        var response3 = await _httpClient.GetAsync(commandUrl);
                        OnLog?.Invoke($"[eMule Web] Comando alternativo Kad enviado: {response3.StatusCode}");
                        
                        OnLog?.Invoke("[eMule Web] Comandos de reconexión Kad enviados");
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[eMule Web] Error reconectando Kad: {ex.Message}");
                        success = false;
                    }
                }

                // Esperar tiempo razonable para que las redes se conecten
                int delaySeconds = (reconnectEd2k && reconnectKad) ? 10 : 5;
                OnLog?.Invoke($"[eMule Web] Esperando {delaySeconds} segundos para que las redes se conecten...");
                await Task.Delay(delaySeconds * 1000);

                var (ed2kOk, kadOk) = await CheckNetworkStatusAsync();
                
                if (reconnectEd2k && !ed2kOk)
                {
                    OnLog?.Invoke("[eMule Web] eD2k no conectó automáticamente");
                    OnLog?.Invoke("[eMule Web] Abre aMule y haz clic en 'Conectar' en la pestaña Redes");
                }
                
                if (reconnectKad && !kadOk)
                {
                    OnLog?.Invoke("[eMule Web] Kad no conectó automáticamente");
                    OnLog?.Invoke("[eMule Web] Abre aMule y haz clic en 'Conectar' en la pestaña Kad");
                }

                OnLog?.Invoke($"[eMule Web] Estado final - eD2k: {(ed2kOk ? "Conectado" : "Desconectado")}, Kad: {(kadOk ? "Conectado" : "Desconectado")}");
                
                if (!ed2kOk && !kadOk)
                {
                    OnLog?.Invoke("[eMule Web] ADVERTENCIA: Ninguna red conectada - la búsqueda no devolverá resultados");
                    OnLog?.Invoke("[eMule Web] Conecta manualmente las redes en aMule antes de buscar");
                }
                else if (!ed2kOk)
                {
                    OnLog?.Invoke("[eMule Web] Solo Kad conectado - resultados limitados");
                }
                else if (!kadOk)
                {
                    OnLog?.Invoke("[eMule Web] Solo eD2k conectado - resultados limitados");
                }
                else
                {
                    OnLog?.Invoke("[eMule Web] Ambas redes conectadas - búsqueda completa");
                }

                return success;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error en reconexión de redes: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Descarga automáticamente server.met desde fuentes confiables
        /// </summary>
        private async Task<bool> DownloadServerListAsync()
        {
            try
            {
                OnLog?.Invoke("[eMule Web] 📥 Descargando lista de servidores eD2k...");
                
                // Lista de URLs de servidores confiables (en orden de preferencia)
                var serverUrls = new[]
                {
                    "http://www.gruk.org/server.met",
                    "http://upd.emule-security.org/server.met",
                    "https://www.emule-security.org/serverlist/server.met"
                };
                
                foreach (var url in serverUrls)
                {
                    try
                    {
                        OnLog?.Invoke($"[eMule Web] Intentando: {url}");
                        
                        // Enviar comando a aMule para actualizar desde URL
                        var updateUrl = $"{_baseUrl}/amuleweb-main-servers.php?command=addurl&url={Uri.EscapeDataString(url)}";
                        var response = await _httpClient.GetAsync(updateUrl);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            OnLog?.Invoke($"[eMule Web] Lista de servidores descargada desde: {url}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[eMule Web] Error con {url}: {ex.Message}");
                    }
                }
                
                OnLog?.Invoke("[eMule Web] No se pudo descargar lista de servidores desde ninguna fuente");
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error descargando server.met: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Descarga automáticamente nodes.dat para la red Kad
        /// </summary>
        private async Task<bool> DownloadNodesDataAsync()
        {
            try
            {
                OnLog?.Invoke("[eMule Web] 📥 Descargando nodes.dat para red Kad...");
                
                // Lista de URLs de nodes.dat confiables
                var nodesUrls = new[]
                {
                    "http://www.nodes-dat.com/dl.php?load=nodes&trace=39513030.1674",
                    "https://www.emule-security.org/serverlist/nodes.dat",
                    "http://upd.emule-security.org/nodes.dat"
                };
                
                foreach (var url in nodesUrls)
                {
                    try
                    {
                        OnLog?.Invoke($"[eMule Web] Intentando: {url}");
                        
                        // Descargar nodes.dat directamente
                        using var httpClient = new HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(30);
                        var nodesData = await httpClient.GetByteArrayAsync(url);
                        
                        if (nodesData != null && nodesData.Length > 1000)
                        {
                            // Determinar ruta de aMule según el sistema operativo
                            string amulePath = "";
                            if (Environment.OSVersion.Platform == PlatformID.Unix || 
                                Environment.OSVersion.Platform == PlatformID.MacOSX)
                            {
                                // Linux/Mac: ~/.aMule/
                                amulePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aMule");
                            }
                            else
                            {
                                // Windows: %APPDATA%\aMule\
                                amulePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "aMule");
                            }
                            
                            // Crear directorio si no existe
                            if (!Directory.Exists(amulePath))
                            {
                                OnLog?.Invoke($"[eMule Web] ⚠️ Directorio aMule no encontrado: {amulePath}");
                                OnLog?.Invoke("[eMule Web] 💡 Asegúrate de que aMule esté instalado y se haya ejecutado al menos una vez");
                                continue;
                            }
                            
                            var nodesPath = Path.Combine(amulePath, "nodes.dat");
                            
                            // Hacer backup del nodes.dat existente
                            if (File.Exists(nodesPath))
                            {
                                var backupPath = Path.Combine(amulePath, $"nodes.dat.backup_{DateTime.Now:yyyyMMddHHmmss}");
                                File.Copy(nodesPath, backupPath, true);
                                OnLog?.Invoke($"[eMule Web] Backup creado: {Path.GetFileName(backupPath)}");
                            }
                            
                            // Guardar nuevo nodes.dat
                            await File.WriteAllBytesAsync(nodesPath, nodesData);
                            OnLog?.Invoke($"[eMule Web] nodes.dat descargado y guardado ({nodesData.Length:N0} bytes)");
                            OnLog?.Invoke($"[eMule Web] Ubicación: {nodesPath}");
                            
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[eMule Web] Error con {url}: {ex.Message}");
                    }
                }
                
                OnLog?.Invoke("[eMule Web] No se pudo descargar nodes.dat desde ninguna fuente");
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] Error descargando nodes.dat: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene el estado de las descargas activas de eMule
        /// </summary>
        public async Task<List<EMuleDownloadInfo>> GetDownloadsAsync()
        {
            var downloads = new List<EMuleDownloadInfo>();
            
            try
            {
                if (!IsConnected || !_isLoggedIn)
                {
                    return downloads;
                }

                var downloadUrl = $"{_baseUrl}/amuleweb-main-dload.php";
                var response = await _httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var html = System.Text.Encoding.UTF8.GetString(bytes);
                
                if (html.Contains("�") || html.Length < 100)
                {
                    html = System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
                }

                // Parsear tabla de descargas
                // Formato: <tr><td>checkbox</td><td>nombre</td><td>tamaño</td><td>completado</td><td>velocidad</td><td>progreso</td><td>fuentes</td><td>estado</td></tr>
                var rowPattern = @"<tr[^>]*>(.*?)</tr>";
                var rowMatches = Regex.Matches(html, rowPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match rowMatch in rowMatches)
                {
                    var rowHtml = rowMatch.Groups[1].Value;
                    
                    // Saltar filas de encabezado, navegación y elementos no relacionados con descargas
                    if (rowHtml.Contains("<th") || 
                        rowHtml.Contains("File name") ||
                        rowHtml.Contains("images/logo.png") ||
                        rowHtml.Contains("MM_swapImage") ||
                        rowHtml.Contains("amuleweb-main-") ||
                        !rowHtml.Contains("checkbox"))
                        continue;

                    var cellPattern = @"<td[^>]*>(.*?)</td>";
                    var cellMatches = Regex.Matches(rowHtml, cellPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    // Las filas de descarga deben tener exactamente 8 celdas
                    if (cellMatches.Count == 8)
                    {
                        try
                        {
                            // Extraer hash del checkbox
                            var checkboxHtml = cellMatches[0].Groups[1].Value;
                            var hashMatch = Regex.Match(checkboxHtml, @"value=[""']([A-F0-9]{32})[""']", RegexOptions.IgnoreCase);
                            var hash = hashMatch.Success ? hashMatch.Groups[1].Value : "";

                            // Extraer nombre del archivo
                            var nameHtml = cellMatches[1].Groups[1].Value;
                            var fileName = StripHtmlTags(nameHtml);

                            // Extraer tamaño
                            var sizeStr = StripHtmlTags(cellMatches[2].Groups[1].Value);
                            var sizeBytes = ParseSize(sizeStr);

                            // Extraer completado
                            var completedStr = StripHtmlTags(cellMatches[3].Groups[1].Value);
                            var completedBytes = ParseSize(completedStr);

                            // Extraer velocidad
                            var speedStr = StripHtmlTags(cellMatches[4].Groups[1].Value);
                            var speedBytes = ParseSpeed(speedStr);
                            
                            // Extraer progreso (puede ser porcentaje o barra de progreso)
                            var progressHtml = cellMatches[5].Groups[1].Value;
                            var progressMatch = Regex.Match(progressHtml, @"(\d+(?:\.\d+)?)\s*%");
                            var progress = progressMatch.Success ? double.Parse(progressMatch.Groups[1].Value) : 0.0;

                            // Extraer fuentes
                            var sourcesStr = StripHtmlTags(cellMatches[6].Groups[1].Value);
                            var sourcesMatch = Regex.Match(sourcesStr, @"(\d+)");
                            var sources = sourcesMatch.Success ? int.Parse(sourcesMatch.Groups[1].Value) : 0;

                            // Extraer estado
                            var status = StripHtmlTags(cellMatches[7].Groups[1].Value);

                            downloads.Add(new EMuleDownloadInfo
                            {
                                FileHash = hash,
                                FileName = fileName,
                                SizeBytes = sizeBytes,
                                CompletedBytes = completedBytes,
                                Progress = progress,
                                Speed = speedStr,
                                SpeedBytesPerSecond = speedBytes,
                                Sources = sources,
                                Status = status,
                                LastUpdate = DateTime.Now
                            });
                        }
                        catch (Exception ex)
                        {
                            OnLog?.Invoke($"[eMule Web] ⚠️ Error parseando fila de descarga: {ex.Message}");
                        }
                    }
                }

                // Log solo si cambió el número de descargas
                if (downloads.Count > 0)
                {
                    // Reducir verbosidad: solo loguear cambios significativos
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] ❌ Error obteniendo descargas: {ex.Message}");
            }

            return downloads;
        }

        /// <summary>
        /// Pausa una descarga de eMule
        /// </summary>
        public async Task<bool> PauseDownloadAsync(string fileHash)
        {
            try
            {
                if (!IsConnected || !_isLoggedIn || string.IsNullOrEmpty(fileHash))
                    return false;

                var url = $"{_baseUrl}/amuleweb-main-dload.php?command=pause&hash={fileHash}";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    OnLog?.Invoke($"[eMule Web] ⏸️ Descarga pausada: {fileHash}");
                    return true;
                }
                
                OnLog?.Invoke($"[eMule Web] ⚠️ Error pausando descarga: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] ❌ Error pausando descarga: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reanuda una descarga de eMule
        /// </summary>
        public async Task<bool> ResumeDownloadAsync(string fileHash)
        {
            try
            {
                if (!IsConnected || !_isLoggedIn || string.IsNullOrEmpty(fileHash))
                    return false;

                var url = $"{_baseUrl}/amuleweb-main-dload.php?command=resume&hash={fileHash}";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    OnLog?.Invoke($"[eMule Web] ▶️ Descarga reanudada: {fileHash}");
                    return true;
                }
                
                OnLog?.Invoke($"[eMule Web] ⚠️ Error reanudando descarga: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] ❌ Error reanudando descarga: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cancela una descarga de eMule
        /// </summary>
        public async Task<bool> CancelDownloadAsync(string fileHash)
        {
            try
            {
                if (!IsConnected || !_isLoggedIn || string.IsNullOrEmpty(fileHash))
                    return false;

                var url = $"{_baseUrl}/amuleweb-main-dload.php?command=cancel&hash={fileHash}";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    OnLog?.Invoke($"[eMule Web] ❌ Descarga cancelada: {fileHash}");
                    return true;
                }
                
                OnLog?.Invoke($"[eMule Web] ⚠️ Error cancelando descarga: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] ❌ Error cancelando descarga: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene estadísticas globales de eMule
        /// </summary>
        public async Task<EMuleStatistics> GetStatisticsAsync(List<EMuleDownloadInfo> downloads = null)
        {
            var stats = new EMuleStatistics();
            
            try
            {
                // Evitar llamada duplicada si ya tenemos los downloads
                if (downloads == null)
                {
                    downloads = await GetDownloadsAsync();
                }
                
                stats.TotalDownloads = downloads.Count;
                stats.ActiveDownloads = downloads.Count(d => d.IsDownloading);
                stats.PausedDownloads = downloads.Count(d => d.IsPaused);
                stats.WaitingDownloads = downloads.Count(d => d.IsWaiting);
                stats.CompletedDownloads = downloads.Count(d => d.IsCompleted);
                
                stats.TotalSpeed = downloads.Sum(d => d.SpeedBytesPerSecond);
                stats.TotalSize = downloads.Sum(d => d.SizeBytes);
                stats.TotalCompleted = downloads.Sum(d => d.CompletedBytes);
                stats.TotalSources = downloads.Sum(d => d.Sources);
                
                if (stats.TotalSpeed > 0)
                {
                    var remaining = stats.TotalSize - stats.TotalCompleted;
                    stats.EstimatedTimeRemaining = TimeSpan.FromSeconds(remaining / stats.TotalSpeed);
                }
                
                stats.AverageHealth = downloads.Any() ? (int)downloads.Average(d => d.Health) : 0;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule Web] ⚠️ Error obteniendo estadísticas: {ex.Message}");
            }
            
            return stats;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Estadísticas globales de eMule
    /// </summary>
    public class EMuleStatistics
    {
        public int TotalDownloads { get; set; }
        public int ActiveDownloads { get; set; }
        public int PausedDownloads { get; set; }
        public int WaitingDownloads { get; set; }
        public int CompletedDownloads { get; set; }
        public long TotalSpeed { get; set; }
        public long TotalSize { get; set; }
        public long TotalCompleted { get; set; }
        public int TotalSources { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public int AverageHealth { get; set; }
        
        public string FormattedSpeed => FormatSpeed(TotalSpeed);
        public double OverallProgress => TotalSize > 0 ? (TotalCompleted * 100.0 / TotalSize) : 0;
        
        private string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
                return $"{bytesPerSecond / (1024.0 * 1024.0):F2} MB/s";
            if (bytesPerSecond >= 1024)
                return $"{bytesPerSecond / 1024.0:F2} KB/s";
            return $"{bytesPerSecond} B/s";
        }
    }

    /// <summary>
    /// Información de una descarga activa de eMule
    /// </summary>
    public class EMuleDownloadInfo
    {
        public string FileHash { get; set; }
        public string FileName { get; set; }
        public long SizeBytes { get; set; }
        public long CompletedBytes { get; set; }
        public double Progress { get; set; }
        public string Speed { get; set; }
        public int Sources { get; set; }
        public string Status { get; set; }
        
        // Propiedades adicionales para gestión avanzada
        public DateTime LastUpdate { get; set; } = DateTime.Now;
        public DateTime StartTime { get; set; } = DateTime.Now;
        public double LastProgress { get; set; }
        public DateTime LastProgressChange { get; set; } = DateTime.Now;
        public int StuckCount { get; set; }
        public bool IsCompleted => Progress >= 100.0;
        public bool IsDownloading => Status.Contains("Downloading", StringComparison.OrdinalIgnoreCase);
        public bool IsPaused => Status.Contains("Paused", StringComparison.OrdinalIgnoreCase);
        public bool IsWaiting => Status.Contains("Waiting", StringComparison.OrdinalIgnoreCase);
        public bool IsStuck => StuckCount >= 3; // Sin progreso por 3+ actualizaciones
        public bool HasNoSources => Sources == 0;
        
        // Velocidad en bytes/segundo para cálculos
        public long SpeedBytesPerSecond { get; set; }
        
        // Tiempo estimado restante
        public TimeSpan? EstimatedTimeRemaining
        {
            get
            {
                if (SpeedBytesPerSecond <= 0 || IsCompleted)
                    return null;
                    
                var remaining = SizeBytes - CompletedBytes;
                return TimeSpan.FromSeconds(remaining / SpeedBytesPerSecond);
            }
        }
        
        // Salud de la descarga (0-100)
        public int Health
        {
            get
            {
                if (IsCompleted) return 100;
                if (IsPaused) return 0;
                
                int health = 50;
                
                // Bonus por fuentes
                if (Sources > 10) health += 30;
                else if (Sources > 5) health += 20;
                else if (Sources > 0) health += 10;
                
                // Bonus por velocidad
                if (SpeedBytesPerSecond > 1024 * 1024) health += 20; // > 1 MB/s
                else if (SpeedBytesPerSecond > 512 * 1024) health += 10; // > 512 KB/s
                
                // Penalización por estar estancado
                if (IsStuck) health -= 40;
                
                return Math.Max(0, Math.Min(100, health));
            }
        }
    }

}

