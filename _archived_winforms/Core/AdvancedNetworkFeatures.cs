using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORAS #13, #14, #15: Funcionalidades avanzadas de red
    /// - NAT-PMP Port Forwarding
    /// - Network Interface Binding
    /// - Distributed Network Optimization
    /// </summary>
    public class AdvancedNetworkFeatures
    {
        private readonly Action<string> log;

        public AdvancedNetworkFeatures(Action<string> log = null)
        {
            this.log = log;
        }

        #region MEJORA #13: NAT-PMP Port Forwarding

        /// <summary>
        /// Intenta configurar port forwarding usando NAT-PMP
        /// </summary>
        public async Task<bool> TryNatPmpPortForwardingAsync(int port, int lifetimeSeconds = 3600)
        {
            try
            {
                log?.Invoke($"🔧 Intentando NAT-PMP port forwarding para puerto {port}...");

                // Obtener gateway
                var gateway = GetDefaultGateway();
                if (gateway == null)
                {
                    log?.Invoke("⚠️ No se pudo obtener gateway por defecto");
                    return false;
                }

                log?.Invoke($"🌐 Gateway detectado: {gateway}");

                // NAT-PMP usa puerto UDP 5351
                using (var client = new UdpClient())
                {
                    client.Connect(gateway, 5351);

                    // Construir request NAT-PMP
                    // Formato: version(1) + opcode(1) + reserved(2) + internal_port(2) + external_port(2) + lifetime(4)
                    var request = new byte[12];
                    request[0] = 0; // Version
                    request[1] = 1; // Opcode: Map UDP (2 para TCP)
                    request[2] = 0; // Reserved
                    request[3] = 0; // Reserved
                    
                    // Internal port (big endian)
                    request[4] = (byte)(port >> 8);
                    request[5] = (byte)(port & 0xFF);
                    
                    // External port (same as internal)
                    request[6] = (byte)(port >> 8);
                    request[7] = (byte)(port & 0xFF);
                    
                    // Lifetime (big endian)
                    request[8] = (byte)(lifetimeSeconds >> 24);
                    request[9] = (byte)((lifetimeSeconds >> 16) & 0xFF);
                    request[10] = (byte)((lifetimeSeconds >> 8) & 0xFF);
                    request[11] = (byte)(lifetimeSeconds & 0xFF);

                    await client.SendAsync(request, request.Length);

                    // Esperar respuesta (timeout 2s)
                    var receiveTask = client.ReceiveAsync();
                    if (await Task.WhenAny(receiveTask, Task.Delay(2000)) == receiveTask)
                    {
                        var response = receiveTask.Result;
                        
                        // Verificar respuesta
                        if (response.Buffer.Length >= 16 && response.Buffer[3] == 0)
                        {
                            log?.Invoke($"✅ NAT-PMP port forwarding configurado: puerto {port}");
                            return true;
                        }
                        else
                        {
                            log?.Invoke($"⚠️ NAT-PMP rechazado por el router");
                            return false;
                        }
                    }
                    else
                    {
                        log?.Invoke("⚠️ NAT-PMP timeout (router no soporta NAT-PMP)");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"❌ Error en NAT-PMP: {ex.Message}");
                return false;
            }
        }

        private IPAddress GetDefaultGateway()
        {
            try
            {
                var gateway = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                    .Select(g => g?.Address)
                    .Where(a => a != null && a.AddressFamily == AddressFamily.InterNetwork)
                    .FirstOrDefault();

                return gateway;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region MEJORA #14: Network Interface Binding

        /// <summary>
        /// Obtiene todas las interfaces de red disponibles
        /// </summary>
        public List<NetworkInterfaceInfo> GetAvailableNetworkInterfaces()
        {
            var interfaces = new List<NetworkInterfaceInfo>();

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    var ipProps = ni.GetIPProperties();
                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                    if (ipv4 != null)
                    {
                        interfaces.Add(new NetworkInterfaceInfo
                        {
                            Id = ni.Id,
                            Name = ni.Name,
                            Description = ni.Description,
                            IPAddress = ipv4.Address.ToString(),
                            Type = ni.NetworkInterfaceType.ToString(),
                            Speed = ni.Speed,
                            IsVPN = IsVpnInterface(ni)
                        });
                    }
                }

                log?.Invoke($"🌐 Interfaces de red detectadas: {interfaces.Count}");
            }
            catch (Exception ex)
            {
                log?.Invoke($"❌ Error obteniendo interfaces: {ex.Message}");
            }

            return interfaces;
        }

        private bool IsVpnInterface(NetworkInterface ni)
        {
            var name = ni.Name.ToLowerInvariant();
            var desc = ni.Description.ToLowerInvariant();

            return name.Contains("vpn") || name.Contains("tun") || name.Contains("tap") ||
                   desc.Contains("vpn") || desc.Contains("virtual") || desc.Contains("tunnel");
        }

        /// <summary>
        /// Crea un socket vinculado a una interfaz específica
        /// </summary>
        public Socket CreateBoundSocket(string interfaceId, int port)
        {
            try
            {
                var ni = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.Id == interfaceId);

                if (ni == null)
                {
                    log?.Invoke($"⚠️ Interfaz {interfaceId} no encontrada");
                    return null;
                }

                var ipProps = ni.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                if (ipv4 == null)
                {
                    log?.Invoke($"⚠️ Interfaz {ni.Name} no tiene dirección IPv4");
                    return null;
                }

                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(ipv4.Address, port));

                log?.Invoke($"✅ Socket vinculado a {ni.Name} ({ipv4.Address}:{port})");
                return socket;
            }
            catch (Exception ex)
            {
                log?.Invoke($"❌ Error vinculando socket: {ex.Message}");
                return null;
            }
        }

        public class NetworkInterfaceInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string IPAddress { get; set; }
            public string Type { get; set; }
            public long Speed { get; set; }
            public bool IsVPN { get; set; }

            public string DisplayText => $"{Name} ({IPAddress}) - {Type}" + (IsVPN ? " [VPN]" : "");
            public string SpeedText => Speed > 0 ? $"{Speed / 1_000_000} Mbps" : "Unknown";
        }

        #endregion

        #region MEJORA #15: Distributed Network Optimization

        /// <summary>
        /// Configuración optimizada para red distribuida
        /// </summary>
        public class DistributedNetworkConfig
        {
            public bool Enabled { get; set; } = false;
            public int ChildLimit { get; set; } = 100; // Máximo de peers hijos
            public int ParentTimeout { get; set; } = 30; // Timeout para parent en segundos
            public bool PreferFastPeers { get; set; } = true; // Priorizar peers rápidos
            public int MinPeerSpeed { get; set; } = 100; // KB/s mínimo para considerar peer
            public bool EnableBranchLevel { get; set; } = true; // Usar branch level para optimizar
            public int MaxBranchLevel { get; set; } = 3; // Máximo nivel de ramificación
        }

        /// <summary>
        /// Optimiza configuración de red distribuida según condiciones
        /// </summary>
        public DistributedNetworkConfig OptimizeDistributedNetwork(
            int currentConnections,
            double averageLatency,
            long uploadSpeed)
        {
            var config = new DistributedNetworkConfig();

            // Habilitar solo si tenemos buena conectividad
            if (currentConnections >= 5 && averageLatency < 200 && uploadSpeed > 100_000)
            {
                config.Enabled = true;
                log?.Invoke("✅ Red distribuida habilitada (buena conectividad)");
            }
            else
            {
                config.Enabled = false;
                log?.Invoke("⚠️ Red distribuida deshabilitada (conectividad limitada)");
                return config;
            }

            // Ajustar límite de hijos según upload speed
            if (uploadSpeed > 1_000_000) // >1 MB/s
            {
                config.ChildLimit = 150;
            }
            else if (uploadSpeed > 500_000) // >500 KB/s
            {
                config.ChildLimit = 100;
            }
            else
            {
                config.ChildLimit = 50;
            }

            // Ajustar timeout según latencia
            if (averageLatency < 50)
            {
                config.ParentTimeout = 20; // Conexión rápida
            }
            else if (averageLatency < 150)
            {
                config.ParentTimeout = 30; // Conexión normal
            }
            else
            {
                config.ParentTimeout = 45; // Conexión lenta
            }

            log?.Invoke($"🔧 Red distribuida optimizada:");
            log?.Invoke($"   👥 Límite de hijos: {config.ChildLimit}");
            log?.Invoke($"   ⏱️ Timeout: {config.ParentTimeout}s");
            log?.Invoke($"   🌳 Branch level máximo: {config.MaxBranchLevel}");

            return config;
        }

        /// <summary>
        /// Estadísticas de red distribuida
        /// </summary>
        public class DistributedNetworkStats
        {
            public int TotalPeers { get; set; }
            public int ParentPeers { get; set; }
            public int ChildPeers { get; set; }
            public int BranchLevel { get; set; }
            public double AverageResponseTime { get; set; }
            public long TotalSearches { get; set; }
            public long DistributedSearches { get; set; }

            public double DistributedSearchPercentage => 
                TotalSearches > 0 ? (DistributedSearches * 100.0 / TotalSearches) : 0;

            public string DisplayText =>
                $"🌐 Red Distribuida:\n" +
                $"   Total peers: {TotalPeers} (↑{ParentPeers} parents, ↓{ChildPeers} children)\n" +
                $"   Branch level: {BranchLevel}\n" +
                $"   Búsquedas distribuidas: {DistributedSearchPercentage:F1}%\n" +
                $"   Tiempo respuesta: {AverageResponseTime:F0}ms";
        }

        #endregion
    }
}
