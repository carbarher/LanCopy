using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Gestor de VPN para cambiar IP automáticamente cuando el servidor Soulseek bloquea la conexión
    /// Soporta: Windscribe, Mullvad, ProtonVPN (Linux), OpenVPN
    /// </summary>
    public class VPNManager
    {
        private readonly Action<string> logCallback;
        private bool isVpnEnabled = false;
        private bool isConnected = false;
        private string currentLocation = "";
        private string vpnType = ""; // windscribe, mullvad, protonvpn
        private string vpnCliPath = "";
        private readonly string[] locations = new[] 
        { 
            "US", "ES", "FR", "DE", "NL", "SE", "CA", "UK"
        };
        private int currentLocationIndex = 0;

        public VPNManager(Action<string> logCallback)
        {
            this.logCallback = logCallback;
        }

        /// <summary>
        /// Detecta qué VPN CLI está instalado (Windscribe, Mullvad, ProtonVPN)
        /// </summary>
        public async Task<bool> DetectVPNAsync()
        {
            // 1. Buscar Windscribe (Windows)
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var windscribePaths = new[]
                {
                    @"C:\Program Files\Windscribe\windscribe-cli.exe",
                    @"C:\Program Files (x86)\Windscribe\windscribe-cli.exe"
                };
                
                foreach (var path in windscribePaths)
                {
                    if (File.Exists(path))
                    {
                        vpnType = "windscribe";
                        vpnCliPath = path;
                        logCallback?.Invoke($"Windscribe CLI detectado: {path}");
                        return true;
                    }
                }
            }
            
            // 2. Buscar Mullvad (Windows)
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var mullvadPath = @"C:\Program Files\Mullvad VPN\resources\mullvad.exe";
                if (File.Exists(mullvadPath))
                {
                    vpnType = "mullvad";
                    vpnCliPath = mullvadPath;
                    logCallback?.Invoke($"Mullvad CLI detectado: {mullvadPath}");
                    return true;
                }
            }
            
            // 2b. Buscar Mullvad en PATH (Linux/Mac)
            try
            {
                var result = await RunCommand("mullvad", "version");
                if (!string.IsNullOrEmpty(result) && result.Contains("mullvad", StringComparison.OrdinalIgnoreCase))
                {
                    vpnType = "mullvad";
                    vpnCliPath = "mullvad";
                    logCallback?.Invoke($"Mullvad CLI detectado");
                    return true;
                }
            }
            catch { }
            
            // 3. Buscar ProtonVPN (Linux/Mac)
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                string[] commands = { "protonvpn", "protonvpn-cli", "pvpn" };
                foreach (var cmd in commands)
                {
                    try
                    {
                        var result = await RunCommand(cmd, "--version");
                        if (!string.IsNullOrEmpty(result))
                        {
                            vpnType = "protonvpn";
                            vpnCliPath = cmd;
                            logCallback?.Invoke($"ProtonVPN CLI detectado: {cmd}");
                            return true;
                        }
                    }
                    catch { }
                }
            }
            
            logCallback?.Invoke("No se detectó ninguna VPN CLI");
            logCallback?.Invoke("Instala Windscribe: https://windscribe.com/download");
            logCallback?.Invoke("O Mullvad: https://mullvad.net/download");
            return false;
        }

        /// <summary>
        /// Conecta a VPN y cambia IP automáticamente
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            if (!isVpnEnabled)
            {
                logCallback?.Invoke("Cambio de IP deshabilitado en configuración");
                return false;
            }

            // Detectar VPN si no se ha detectado aún
            if (string.IsNullOrEmpty(vpnType))
            {
                await DetectVPNAsync();
            }

            if (string.IsNullOrEmpty(vpnType))
            {
                logCallback?.Invoke("No hay VPN CLI disponible");
                return false;
            }

            try
            {
                // Obtener IP actual
                var ipAntes = await GetPublicIPAsync();
                logCallback?.Invoke($"IP actual: {ipAntes}");
                
                // Seleccionar ubicación aleatoria
                currentLocationIndex = (currentLocationIndex + 1) % locations.Length;
                currentLocation = locations[currentLocationIndex];
                
                logCallback?.Invoke($"Cambiando IP con {vpnType.ToUpper()}...");
                logCallback?.Invoke($"   Conectando a: {currentLocation}");
                
                string result = "";
                
                // Conectar según el tipo de VPN
                switch (vpnType)
                {
                    case "windscribe":
                        result = await RunCommand(vpnCliPath, $"connect {currentLocation}");
                        break;
                        
                    case "mullvad":
                        await RunCommand("mullvad", $"relay set location {currentLocation.ToLower()}");
                        result = await RunCommand("mullvad", "connect");
                        break;
                        
                    case "protonvpn":
                        result = await RunCommand(vpnCliPath, $"connect --cc {currentLocation}");
                        break;
                        
                    default:
                        logCallback?.Invoke($"Tipo de VPN no soportado: {vpnType}");
                        return false;
                }
                
                // Esperar a que se establezca la conexión
                await Task.Delay(3000);
                
                // Verificar nueva IP
                var ipDespues = await GetPublicIPAsync();
                logCallback?.Invoke($"Nueva IP: {ipDespues}");
                
                if (ipAntes != ipDespues)
                {
                    logCallback?.Invoke($"IP cambiada exitosamente");
                    logCallback?.Invoke($"   Antes: {ipAntes}");
                    logCallback?.Invoke($"   Después: {ipDespues}");
                    isConnected = true;
                    return true;
                }
                else
                {
                    // La IP no cambió - verificar si es un problema real
                    logCallback?.Invoke($"La IP no cambió - posible problema con VPN");
                    
                    // Verificar estado de Windscribe específicamente
                    if (vpnType == "windscribe")
                    {
                        try
                        {
                            // Verificar si está logueado
                            var loginStatus = await RunCommand(vpnCliPath, "status");
                            if (loginStatus.Contains("Login required") || loginStatus.Contains("Not logged in"))
                            {
                                logCallback?.Invoke($"Windscribe no está logueado - ejecuta: windscribe login");
                                isConnected = false;
                                return false;
                            }
                            
                            // Verificar si hay datos disponibles
                            var accountStatus = await RunCommand(vpnCliPath, "account");
                            if (accountStatus.Contains("No data left") || accountStatus.Contains("0.00"))
                            {
                                logCallback?.Invoke($"Windscribe sin datos disponibles - límite mensual alcanzado");
                                isConnected = false;
                                return false;
                            }
                            
                            // Verificar conexión real
                            if (!loginStatus.Contains("Connected"))
                            {
                                logCallback?.Invoke($"Windscribe no está conectado realmente: {loginStatus}");
                                isConnected = false;
                                return false;
                            }
                        }
                        catch (Exception statusEx)
                        {
                            logCallback?.Invoke($"No se pudo verificar estado de Windscribe: {statusEx.Message}");
                        }
                    }
                    
                    logCallback?.Invoke($"IP no cambió pero VPN reporta conexión - puede tardar unos segundos");
                    isConnected = true;
                    return false; // Cambiar a false para indicar fallo
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Error cambiando IP: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Desconecta la VPN
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (!isConnected)
                return;

            try
            {
                logCallback?.Invoke("Desconectando VPN...");
                
                switch (vpnType)
                {
                    case "windscribe":
                        await RunCommand(vpnCliPath, "disconnect");
                        break;
                    case "mullvad":
                        await RunCommand("mullvad", "disconnect");
                        break;
                    case "protonvpn":
                        await RunCommand(vpnCliPath, "disconnect");
                        break;
                }
                
                isConnected = false;
                currentLocation = "";
                logCallback?.Invoke("VPN desconectada");
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Error desconectando VPN: {ex.Message}");
            }
        }

        /// <summary>
        /// Cambia a un servidor VPN diferente
        /// </summary>
        public async Task<bool> RotateServerAsync()
        {
            logCallback?.Invoke("Rotando servidor VPN...");
            return await ConnectAsync();
        }

        /// <summary>
        /// Habilita o deshabilita el uso automático de VPN
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            isVpnEnabled = enabled;
            logCallback?.Invoke($"VPN auto-connect: {(enabled ? "Habilitado" : "Deshabilitado")}");
        }

        /// <summary>
        /// Verifica el estado de la VPN y da instrucciones si hay problemas
        /// </summary>
        public async Task<bool> CheckVPNStatusAsync()
        {
            logCallback?.Invoke("\n=== DIAGNÓSTICO COMPLETO DE VPN ===");
            
            // 1. Forzar detección de VPN
            logCallback?.Invoke("Paso 1: Detectando VPN CLI...");
            bool vpnDetected = await DetectVPNAsync();
            
            if (!vpnDetected)
            {
                logCallback?.Invoke("No se detectó ninguna VPN CLI");
                logCallback?.Invoke("Solución: Instala Windscribe desde https://windscribe.com/download");
                return false;
            }
            
            if (!isVpnEnabled || string.IsNullOrEmpty(vpnType))
            {
                logCallback?.Invoke("VPN no está habilitada en SlskDown");
                return false;
            }
            
            logCallback?.Invoke($"VPN detectada: {vpnType}");
            logCallback?.Invoke($"Ruta CLI: {vpnCliPath}");
            logCallback?.Invoke("");
            
            // 2. Verificar estado específico
            logCallback?.Invoke("Paso 2: Verificando estado de VPN...");
            
            try
            {
                if (vpnType == "windscribe")
                {
                    // Verificar que el CLI funciona
                    logCallback?.Invoke("Probando CLI de Windscribe...");
                    var version = await RunCommand(vpnCliPath, "--version");
                    logCallback?.Invoke($"Versión: {version.Trim()}");
                    
                    // Verificar login
                    logCallback?.Invoke("Verificando estado de login...");
                    var status = await RunCommand(vpnCliPath, "status");
                    logCallback?.Invoke($"Estado: {status.Trim()}");
                    
                    if (status.Contains("Login required") || status.Contains("Not logged in"))
                    {
                        logCallback?.Invoke("");
                        logCallback?.Invoke("WINDSCRIBE NO ESTÁ LOGUEADO");
                        logCallback?.Invoke("SOLUCIÓN:");
                        logCallback?.Invoke("   1. Abre CMD (Windows + R, escribe 'cmd', Enter)");
                        logCallback?.Invoke("   2. Ejecuta: \"C:\\Program Files\\Windscribe\\windscribe-cli.exe\" login");
                        logCallback?.Invoke("   3. Ingresa tu usuario y contraseña");
                        logCallback?.Invoke("   4. Reinicia SlskDown");
                        logCallback?.Invoke("");
                        return false;
                    }
                    
                    // Verificar datos
                    var account = await RunCommand(vpnCliPath, "account");
                    logCallback?.Invoke($"Cuenta: {account.Trim()}");
                    
                    if (account.Contains("No data left") || account.Contains("0.00"))
                    {
                        logCallback?.Invoke("");
                        logCallback?.Invoke("WINDSCRIBE SIN DATOS DISPONIBLES");
                        logCallback?.Invoke("Has alcanzado el límite de 10GB del mes");
                        logCallback?.Invoke("OPCIONES:");
                        logCallback?.Invoke("   1. Esperar al próximo mes (reset automático)");
                        logCallback?.Invoke("   2. Actualizar a plan pago ($9/mes)");
                        logCallback?.Invoke("   3. Usar otra VPN (Mullvad, ProtonVPN)");
                        logCallback?.Invoke("");
                        return false;
                    }
                    
                    if (!status.Contains("Connected"))
                    {
                        logCallback?.Invoke("Windscribe no está conectado");
                        return false;
                    }
                    
                    logCallback?.Invoke("Windscribe funcionando correctamente");
                    
                    // Mostrar IP actual
                    var currentIP = await GetPublicIPAsync();
                    logCallback?.Invoke($"IP actual: {currentIP}");
                    
                    logCallback?.Invoke("");
                    logCallback?.Invoke("VPN ESTÁ LISTA PARA USARSE");
                    logCallback?.Invoke("SlskDown cambiará IP automáticamente cuando detecte bloqueo");
                    
                    return true;
                }
                
                logCallback?.Invoke($"Verificación de estado no implementada para {vpnType}");
                return true;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Error verificando estado de VPN: {ex.Message}");
                logCallback?.Invoke($"Detalles: {ex}");
                return false;
            }
            finally
            {
                logCallback?.Invoke("=== FIN DIAGNÓSTICO ===\n");
            }
        }

        /// <summary>
        /// Verifica si la VPN está conectada
        /// </summary>
        public bool IsConnected => isConnected;

        /// <summary>
        /// Obtiene la ubicación VPN actual
        /// </summary>
        public string CurrentServer => currentLocation;

        /// <summary>
        /// Obtiene la IP pública actual
        /// </summary>
        public async Task<string> GetPublicIPAsync()
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var ip = await client.GetStringAsync("https://api.ipify.org");
                    return ip?.Trim() ?? "Desconocida";
                }
            }
            catch
            {
                return "Error al obtener IP";
            }
        }

        /// <summary>
        /// Ejecuta un comando en el sistema
        /// </summary>
        private async Task<string> RunCommand(string command, string arguments)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                        return "";

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    await process.WaitForExitAsync();

                    return string.IsNullOrEmpty(error) ? output : error;
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
