using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SlskDown.EMule
{
    /// <summary>
    /// Gestiona nodos bootstrap para la red eMule/Kad
    /// Inspirado en nodes.dat de aMule
    /// </summary>
    public class BootstrapNodeManager
    {
        private readonly List<BootstrapNode> _nodes;
        private readonly string _nodesFilePath;
        private readonly Action<string> _onLog;

        public BootstrapNodeManager(string nodesFilePath, Action<string> onLog = null)
        {
            _nodes = new List<BootstrapNode>();
            _nodesFilePath = nodesFilePath;
            _onLog = onLog;
        }

        public class BootstrapNode
        {
            public IPAddress IP { get; set; }
            public ushort Port { get; set; }
            public byte KadVersion { get; set; }
            public byte[] KadID { get; set; }
            public DateTime LastSeen { get; set; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }

            public double Reliability
            {
                get
                {
                    var total = SuccessCount + FailureCount;
                    return total == 0 ? 0.5 : SuccessCount / (double)total;
                }
            }

            public override string ToString()
            {
                return $"{IP}:{Port} (v{KadVersion}, reliability: {Reliability:P0}, last seen: {LastSeen:g})";
            }
        }

        /// <summary>
        /// Obtiene el mejor nodo disponible basado en confiabilidad y última vez visto
        /// </summary>
        public BootstrapNode GetBestNode()
        {
            if (_nodes.Count == 0)
            {
                return null;
            }

            return _nodes
                .Where(n => n.Reliability >= 0.3) // Al menos 30% de confiabilidad
                .OrderByDescending(n => n.Reliability)
                .ThenByDescending(n => n.LastSeen)
                .FirstOrDefault() ?? _nodes.OrderByDescending(n => n.LastSeen).First();
        }

        /// <summary>
        /// Obtiene múltiples nodos buenos para intentos paralelos
        /// </summary>
        public List<BootstrapNode> GetTopNodes(int count = 5)
        {
            return _nodes
                .Where(n => n.Reliability >= 0.3)
                .OrderByDescending(n => n.Reliability)
                .ThenByDescending(n => n.LastSeen)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Registra un intento exitoso de conexión con un nodo
        /// </summary>
        public void RecordSuccess(IPAddress ip, ushort port)
        {
            var node = _nodes.FirstOrDefault(n => n.IP.Equals(ip) && n.Port == port);
            if (node != null)
            {
                node.SuccessCount++;
                node.LastSeen = DateTime.Now;
                _onLog?.Invoke($"[Bootstrap] ✅ Nodo exitoso: {node}");
            }
        }

        /// <summary>
        /// Registra un intento fallido de conexión con un nodo
        /// </summary>
        public void RecordFailure(IPAddress ip, ushort port)
        {
            var node = _nodes.FirstOrDefault(n => n.IP.Equals(ip) && n.Port == port);
            if (node != null)
            {
                node.FailureCount++;
                _onLog?.Invoke($"[Bootstrap] ❌ Nodo fallido: {node}");
            }
        }

        /// <summary>
        /// Carga nodos desde archivo nodes.dat (formato binario de aMule)
        /// </summary>
        public async Task LoadNodesAsync()
        {
            if (!File.Exists(_nodesFilePath))
            {
                _onLog?.Invoke($"[Bootstrap] ⚠️ Archivo nodes.dat no encontrado: {_nodesFilePath}");
                await LoadDefaultNodesAsync();
                return;
            }

            try
            {
                using var fs = File.OpenRead(_nodesFilePath);
                using var reader = new BinaryReader(fs);

                // Leer versión del archivo
                var version = reader.ReadUInt32();
                _onLog?.Invoke($"[Bootstrap] 📄 Leyendo nodes.dat versión {version}");

                // Leer número de nodos
                var count = reader.ReadUInt32();
                _onLog?.Invoke($"[Bootstrap] 📊 Cargando {count} nodos...");

                for (int i = 0; i < count && fs.Position < fs.Length; i++)
                {
                    try
                    {
                        var node = new BootstrapNode
                        {
                            KadID = reader.ReadBytes(16),
                            IP = new IPAddress(reader.ReadBytes(4)),
                            Port = reader.ReadUInt16(),
                            KadVersion = reader.ReadByte(),
                            LastSeen = DateTime.Now,
                            SuccessCount = 0,
                            FailureCount = 0
                        };

                        _nodes.Add(node);
                    }
                    catch (Exception ex)
                    {
                        _onLog?.Invoke($"[Bootstrap] ⚠️ Error leyendo nodo {i}: {ex.Message}");
                    }
                }

                _onLog?.Invoke($"[Bootstrap] ✅ Cargados {_nodes.Count} nodos desde archivo");
            }
            catch (Exception ex)
            {
                _onLog?.Invoke($"[Bootstrap] ❌ Error cargando nodes.dat: {ex.Message}");
                await LoadDefaultNodesAsync();
            }
        }

        /// <summary>
        /// Carga nodos por defecto si no hay archivo nodes.dat
        /// </summary>
        private async Task LoadDefaultNodesAsync()
        {
            _onLog?.Invoke("[Bootstrap] 🌐 Cargando nodos por defecto...");

            // Nodos públicos conocidos de la red eMule/Kad
            var defaultNodes = new[]
            {
                ("91.200.42.46", 4672),
                ("91.200.42.47", 4672),
                ("91.200.42.48", 4672),
                ("91.200.42.49", 4672),
                ("212.83.184.152", 4672),
                ("212.83.187.167", 4672),
                ("195.245.244.243", 4672),
                ("80.208.228.241", 4672)
            };

            foreach (var (ip, port) in defaultNodes)
            {
                try
                {
                    _nodes.Add(new BootstrapNode
                    {
                        IP = IPAddress.Parse(ip),
                        Port = (ushort)port,
                        KadVersion = 8,
                        KadID = new byte[16], // ID vacío para nodos por defecto
                        LastSeen = DateTime.Now.AddDays(-30), // Marcar como antiguos
                        SuccessCount = 0,
                        FailureCount = 0
                    });
                }
                catch (Exception ex)
                {
                    _onLog?.Invoke($"[Bootstrap] ⚠️ Error parseando nodo {ip}:{port}: {ex.Message}");
                }
            }

            _onLog?.Invoke($"[Bootstrap] ✅ Cargados {_nodes.Count} nodos por defecto");

            // Guardar nodos por defecto en archivo
            await SaveNodesAsync();
        }

        /// <summary>
        /// Guarda nodos en archivo nodes.dat
        /// </summary>
        public async Task SaveNodesAsync()
        {
            try
            {
                // Crear directorio si no existe
                var directory = Path.GetDirectoryName(_nodesFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var fs = File.Create(_nodesFilePath);
                using var writer = new BinaryWriter(fs);

                // Escribir versión
                writer.Write((uint)1);

                // Escribir número de nodos
                writer.Write((uint)_nodes.Count);

                // Escribir cada nodo
                foreach (var node in _nodes)
                {
                    writer.Write(node.KadID ?? new byte[16]);
                    writer.Write(node.IP.GetAddressBytes());
                    writer.Write(node.Port);
                    writer.Write(node.KadVersion);
                }

                _onLog?.Invoke($"[Bootstrap] 💾 Guardados {_nodes.Count} nodos en {_nodesFilePath}");
            }
            catch (Exception ex)
            {
                _onLog?.Invoke($"[Bootstrap] ❌ Error guardando nodes.dat: {ex.Message}");
            }
        }

        /// <summary>
        /// Agrega un nuevo nodo descubierto
        /// </summary>
        public void AddNode(IPAddress ip, ushort port, byte kadVersion, byte[] kadID = null)
        {
            // Verificar si ya existe
            var existing = _nodes.FirstOrDefault(n => n.IP.Equals(ip) && n.Port == port);
            if (existing != null)
            {
                // Actualizar información
                existing.KadVersion = kadVersion;
                if (kadID != null && kadID.Length == 16)
                {
                    existing.KadID = kadID;
                }
                existing.LastSeen = DateTime.Now;
                return;
            }

            // Agregar nuevo nodo
            var node = new BootstrapNode
            {
                IP = ip,
                Port = port,
                KadVersion = kadVersion,
                KadID = kadID ?? new byte[16],
                LastSeen = DateTime.Now,
                SuccessCount = 0,
                FailureCount = 0
            };

            _nodes.Add(node);
            _onLog?.Invoke($"[Bootstrap] ➕ Nuevo nodo agregado: {node}");
        }

        /// <summary>
        /// Limpia nodos antiguos y poco confiables
        /// </summary>
        public void CleanupOldNodes(TimeSpan maxAge, double minReliability = 0.1)
        {
            var cutoff = DateTime.Now - maxAge;
            var before = _nodes.Count;

            _nodes.RemoveAll(n =>
                n.LastSeen < cutoff && n.Reliability < minReliability);

            var removed = before - _nodes.Count;
            if (removed > 0)
            {
                _onLog?.Invoke($"[Bootstrap] 🗑️ Eliminados {removed} nodos antiguos/poco confiables");
            }
        }

        /// <summary>
        /// Obtiene estadísticas de los nodos
        /// </summary>
        public string GetStatistics()
        {
            if (_nodes.Count == 0)
            {
                return "No hay nodos cargados";
            }

            var reliable = _nodes.Count(n => n.Reliability >= 0.7);
            var moderate = _nodes.Count(n => n.Reliability >= 0.3 && n.Reliability < 0.7);
            var unreliable = _nodes.Count(n => n.Reliability < 0.3);
            var avgReliability = _nodes.Average(n => n.Reliability);
            var recentNodes = _nodes.Count(n => (DateTime.Now - n.LastSeen).TotalDays < 7);

            return $"Total: {_nodes.Count} nodos\n" +
                   $"  Confiables (≥70%): {reliable}\n" +
                   $"  Moderados (30-70%): {moderate}\n" +
                   $"  No confiables (<30%): {unreliable}\n" +
                   $"  Confiabilidad promedio: {avgReliability:P0}\n" +
                   $"  Vistos últimos 7 días: {recentNodes}";
        }

        public int NodeCount => _nodes.Count;
    }
}
