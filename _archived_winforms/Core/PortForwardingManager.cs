using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Open.Nat;

namespace SlskDown.Core
{
    /// <summary>
    /// Gestor de port forwarding automático usando UPnP/NAT-PMP
    /// </summary>
    public class PortForwardingManager
    {
        private NatDevice device;
        private Mapping currentMapping;
        private bool isEnabled;
        private readonly object lockObj = new object();
        
        public event Action<string> OnLog;
        public event Action<bool> OnStatusChanged;
        
        public bool IsEnabled
        {
            get { lock (lockObj) return isEnabled; }
            private set { lock (lockObj) isEnabled = value; }
        }
        
        public string ExternalIP { get; private set; }
        public int MappedPort { get; private set; }
        
        /// <summary>
        /// Descubre dispositivo NAT (router)
        /// </summary>
        public async Task<bool> DiscoverDevice(int timeoutSeconds = 5)
        {
            try
            {
                Log("Buscando dispositivo NAT (router)...");
                
                var discoverer = new NatDiscoverer();
                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                
                device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp | PortMapper.Pmp, cts);
                
                if (device != null)
                {
                    ExternalIP = (await device.GetExternalIPAsync()).ToString();
                    Log($"✓ Dispositivo NAT encontrado: {device.GetType().Name}");
                    Log($"✓ IP Externa: {ExternalIP}");
                    return true;
                }
                
                Log("✗ No se encontró dispositivo NAT");
                return false;
            }
            catch (NatDeviceNotFoundException)
            {
                Log("✗ No se encontró dispositivo NAT (UPnP/NAT-PMP no disponible)");
                return false;
            }
            catch (Exception ex)
            {
                Log($"✗ Error descubriendo dispositivo NAT: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Crea un port mapping (abre puerto)
        /// </summary>
        public async Task<bool> CreatePortMapping(int port, string description = "Soulseek p2p")
        {
            if (device == null)
            {
                Log("✗ Dispositivo NAT no inicializado. Llama a DiscoverDevice() primero.");
                return false;
            }
            
            try
            {
                Log($"Creando port mapping para puerto {port}...");
                
                // Crear mapping TCP
                currentMapping = new Mapping(Open.Nat.Protocol.Tcp, port, port, description);
                await device.CreatePortMapAsync(currentMapping);
                
                MappedPort = port;
                IsEnabled = true;
                
                Log($"✓ Port mapping creado: Puerto {port} (TCP)");
                Log($"✓ Accesible desde: {ExternalIP}:{port}");
                
                OnStatusChanged?.Invoke(true);
                return true;
            }
            catch (MappingException ex)
            {
                Log($"✗ Error creando port mapping: {ex.Message}");
                
                // Intentar con un puerto alternativo
                if (ex.ErrorCode == 718) // ConflictInMappingEntry
                {
                    Log($"Puerto {port} ya está en uso, intentando con puerto aleatorio...");
                    var randomPort = new Random().Next(49152, 65535);
                    return await CreatePortMapping(randomPort, description);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log($"✗ Error inesperado: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Elimina el port mapping (cierra puerto)
        /// </summary>
        public async Task<bool> DeletePortMapping()
        {
            if (device == null || currentMapping == null)
            {
                Log("No hay mapping activo para eliminar");
                return false;
            }
            
            try
            {
                Log($"Eliminando port mapping del puerto {currentMapping.PublicPort}...");
                
                await device.DeletePortMapAsync(currentMapping);
                
                Log($"✓ Port mapping eliminado");
                
                IsEnabled = false;
                MappedPort = 0;
                currentMapping = null;
                
                OnStatusChanged?.Invoke(false);
                return true;
            }
            catch (Exception ex)
            {
                Log($"✗ Error eliminando port mapping: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Renueva el port mapping (mantiene el puerto abierto)
        /// </summary>
        public async Task<bool> RenewPortMapping()
        {
            if (!IsEnabled || currentMapping == null)
                return false;
            
            try
            {
                // Eliminar y recrear
                await device.DeletePortMapAsync(currentMapping);
                await device.CreatePortMapAsync(currentMapping);
                
                Log($"✓ Port mapping renovado");
                return true;
            }
            catch (Exception ex)
            {
                Log($"✗ Error renovando port mapping: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Obtiene todos los port mappings activos
        /// </summary>
        public async Task<string[]> GetAllMappings()
        {
            if (device == null)
                return Array.Empty<string>();
            
            try
            {
                var mappings = await device.GetAllMappingsAsync();
                return mappings
                    .Select(m => $"{m.Description}: {m.PrivateIP}:{m.PrivatePort} -> {m.PublicPort} ({m.Protocol})")
                    .ToArray();
            }
            catch (Exception ex)
            {
                Log($"Error obteniendo mappings: {ex.Message}");
                return Array.Empty<string>();
            }
        }
        
        /// <summary>
        /// Verifica si el puerto está accesible desde internet
        /// </summary>
        public async Task<bool> TestPortAccessibility(int port)
        {
            try
            {
                Log($"Probando accesibilidad del puerto {port}...");
                
                // Intentar conectar desde IP externa (esto es una simulación)
                // En producción, necesitarías un servicio externo que pruebe la conexión
                
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var result = client.BeginConnect(ExternalIP, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                    
                    if (success)
                    {
                        client.EndConnect(result);
                        Log($"✓ Puerto {port} es accesible");
                        return true;
                    }
                    else
                    {
                        Log($"✗ Puerto {port} no es accesible (timeout)");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Error probando puerto: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Configuración automática completa
        /// </summary>
        public async Task<(bool Success, int Port)> AutoConfigure(int preferredPort = 0)
        {
            // Descubrir dispositivo
            if (!await DiscoverDevice())
                return (false, 0);
            
            // Determinar puerto
            var port = preferredPort > 0 ? preferredPort : new Random().Next(49152, 65535);
            
            // Crear mapping
            if (!await CreatePortMapping(port))
                return (false, 0);
            
            // Verificar accesibilidad (opcional)
            // await TestPortAccessibility(port);
            
            return (true, port);
        }
        
        /// <summary>
        /// Limpieza al cerrar la aplicación
        /// </summary>
        public async Task Cleanup()
        {
            if (IsEnabled)
            {
                await DeletePortMapping();
            }
        }
        
        private void Log(string message)
        {
            OnLog?.Invoke($"[PortForwarding] {message}");
        }
    }
}
