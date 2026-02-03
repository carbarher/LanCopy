using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SlskDown.Core;

namespace SlskDown.EMule
{
    /// <summary>
    /// Cliente eMule que implementa INetworkClient
    /// Gestiona conexión a la red ed2k/Kad mediante aMule daemon
    /// </summary>
    public class EMuleClient : INetworkClient
    {
        private Process _amuleProcess;
        private TcpClient _ecClient;
        private NetworkStream _ecStream;
        private NetworkConnectionState _state = NetworkConnectionState.Disconnected;
        private readonly object _stateLock = new object();
        private CancellationTokenSource _cts;
        private DateTime _connectedAt;
        private NetworkStatistics _stats = new NetworkStatistics();
        private byte[] _lastReceivedBody; // Para acceder al body raw

        public string NetworkName => "eMule/ed2k";

        public NetworkConnectionState State
        {
            get { lock (_stateLock) { return _state; } }
            private set
            {
                NetworkConnectionState oldState;
                lock (_stateLock)
                {
                    oldState = _state;
                    _state = value;
                }
                StateChanged?.Invoke(this, new NetworkStateChangedEventArgs
                {
                    PreviousState = oldState,
                    CurrentState = value
                });
            }
        }

        public bool IsConnected => State == NetworkConnectionState.Connected || State == NetworkConnectionState.LoggedIn;

        public event EventHandler<NetworkStateChangedEventArgs> StateChanged;
        public event Action<string> OnLog;

        /// <summary>
        /// Configuración del cliente eMule
        /// </summary>
        public EMuleConfig Config { get; set; } = new EMuleConfig();

        public async Task ConnectAsync(NetworkCredentials credentials, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                throw new InvalidOperationException("Ya está conectado a eMule");
            }

            State = NetworkConnectionState.Connecting;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // Paso 1: Iniciar aMule daemon si no está corriendo
                await StartAmuleDaemonAsync();

                // Paso 2: Conectar al puerto EC (External Connections)
                await ConnectToECPortAsync(credentials);

                // Paso 3: Autenticar
                await AuthenticateAsync(credentials);

                // Paso 4: Conectar a red ed2k/Kad
                await ConnectToNetworkAsync();

                _connectedAt = DateTime.UtcNow;
                State = NetworkConnectionState.LoggedIn;
            }
            catch (Exception ex)
            {
                State = NetworkConnectionState.Failed;
                throw new Exception($"Error conectando a eMule: {ex.Message}", ex);
            }
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected)
            {
                return;
            }

            try
            {
                // Desconectar de la red
                await SendDisconnectCommandAsync();

                // Cerrar conexión EC
                _ecStream?.Close();
                _ecClient?.Close();

                // Detener daemon si lo iniciamos nosotros
                if (Config.ManageDaemon && _amuleProcess != null && !_amuleProcess.HasExited)
                {
                    _amuleProcess.Kill();
                    _amuleProcess.WaitForExit(5000);
                }

                State = NetworkConnectionState.Disconnected;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error desconectando de eMule: {ex.Message}", ex);
            }
        }

        public NetworkStatistics GetStatistics()
        {
            _stats.Uptime = IsConnected ? DateTime.UtcNow - _connectedAt : TimeSpan.Zero;
            _stats.LastConnected = _connectedAt;
            return _stats;
        }

        private async Task StartAmuleDaemonAsync()
        {
            if (!Config.ManageDaemon)
            {
                // Asumimos que el daemon ya está corriendo externamente
                return;
            }

            var amulePath = Config.AmuleDaemonPath ?? FindAmuleDaemon();
            if (string.IsNullOrEmpty(amulePath) || !File.Exists(amulePath))
            {
                throw new FileNotFoundException("No se encontró amuled. Instala aMule o especifica la ruta en Config.AmuleDaemonPath");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = amulePath,
                Arguments = "-f", // Fork to background
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _amuleProcess = Process.Start(startInfo);
            if (_amuleProcess == null)
            {
                throw new Exception("No se pudo iniciar amuled");
            }

            // Esperar a que el daemon esté listo (puerto EC disponible)
            await Task.Delay(2000);
        }

        private async Task ConnectToECPortAsync(NetworkCredentials credentials)
        {
            var host = credentials.Server ?? "127.0.0.1";
            var port = credentials.Port > 0 ? credentials.Port : 4712; // Puerto EC por defecto

            _ecClient = new TcpClient();
            await _ecClient.ConnectAsync(host, port);
            _ecStream = _ecClient.GetStream();
        }

        private async Task AuthenticateAsync(NetworkCredentials credentials)
        {
            // Implementar protocolo EC de autenticación de aMule (2 pasos)
            // Ver: https://github.com/amule-project/amule/blob/master/src/ExternalConn.cpp
            
            OnLog?.Invoke($"[eMule] Iniciando autenticación con salt (2 pasos)...");

            // PASO 1: Enviar AUTH_REQ vacío para solicitar el salt
            // Probar sin tags primero (AUTH_REQ completamente vacío)
            var authReqPacket = BuildECPacket(
                ECOpCode.EC_OP_AUTH_REQ,
                new List<ECTag>() // Sin tags
            );

            OnLog?.Invoke($"[eMule] Paso 1: Enviando AUTH_REQ (solicitando salt)...");
            await SendECPacketAsync(authReqPacket);

            // PASO 2: Recibir AUTH_SALT con el salt
            var saltResponse = await ReceiveECPacketAsync();
            OnLog?.Invoke($"[eMule] Paso 2: Respuesta recibida: OpCode={saltResponse.OpCode} (0x{(byte)saltResponse.OpCode:X2})");

            if (saltResponse.OpCode != ECOpCode.EC_OP_AUTH_SALT)
            {
                throw new Exception($"Respuesta inesperada en paso 2: {saltResponse.OpCode}. Se esperaba EC_OP_AUTH_SALT (0x05).");
            }

            // Extraer el salt del body raw (8 bytes después del OpCode)
            if (_lastReceivedBody == null || _lastReceivedBody.Length < 8)
            {
                throw new Exception("No se pudo leer el salt del servidor.");
            }

            // El salt son los primeros 8 bytes del body (después del header de 3 bytes que ya se saltó)
            ulong salt = BitConverter.ToUInt64(_lastReceivedBody, 0);
            string saltHex = salt.ToString("x16"); // Convertir a hex lowercase
            OnLog?.Invoke($"[eMule] Salt recibido: 0x{saltHex}");

            // PASO 3: Calcular hash = MD5(password_lowercase + salt_hex_lowercase)
            string passwordLower = (credentials.Password ?? "").ToLower();
            string combinedString = passwordLower + saltHex;
            OnLog?.Invoke($"[eMule] Calculando hash: MD5('{passwordLower}' + '{saltHex}')");
            
            var hashBytes = ComputeMD5HashBytes(combinedString);
            var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            OnLog?.Invoke($"[eMule] Hash calculado: {hashHex}");

            // PASO 4: Enviar AUTH_PASSWD con el hash
            var authPasswdPacket = BuildECPacket(
                ECOpCode.EC_OP_AUTH_PASSWD,
                new List<ECTag>
                {
                    new ECTag(ECTagName.EC_TAG_PASSWD_HASH, hashBytes)
                }
            );

            OnLog?.Invoke($"[eMule] Paso 3: Enviando AUTH_PASSWD...");
            await SendECPacketAsync(authPasswdPacket);

            // PASO 5: Recibir AUTH_OK o AUTH_FAIL
            var authResponse = await ReceiveECPacketAsync();
            OnLog?.Invoke($"[eMule] Paso 4: Respuesta final: OpCode={authResponse.OpCode} (0x{(byte)authResponse.OpCode:X2})");

            if (authResponse.OpCode != ECOpCode.EC_OP_AUTH_OK)
            {
                string errorMsg = $"Autenticación fallida. OpCode recibido: {authResponse.OpCode} (0x{(byte)authResponse.OpCode:X2}).";
                if (authResponse.OpCode == ECOpCode.EC_OP_FAILED && authResponse.Tags.Count > 0)
                {
                    errorMsg += $" Error de aMule: {authResponse.Tags[0].Value}";
                }
                throw new UnauthorizedAccessException(errorMsg);
            }

            OnLog?.Invoke($"[eMule] ✅ Autenticación exitosa!");
        }

        private async Task ConnectToNetworkAsync()
        {
            // Enviar comando para conectar a ed2k
            var connectPacket = BuildECPacket(ECOpCode.EC_OP_CONNECT, new List<ECTag>());
            await SendECPacketAsync(connectPacket);

            // Opcional: Iniciar Kad
            if (Config.EnableKad)
            {
                var kadPacket = BuildECPacket(ECOpCode.EC_OP_KAD_START, new List<ECTag>());
                await SendECPacketAsync(kadPacket);
            }
        }

        private async Task SendDisconnectCommandAsync()
        {
            var packet = BuildECPacket(ECOpCode.EC_OP_DISCONNECT, new List<ECTag>());
            await SendECPacketAsync(packet);
        }

        /// <summary>
        /// Envía un paquete EC al daemon de aMule
        /// </summary>
        public async Task SendECPacketAsync(ECPacket packet)
        {
            if (_ecStream == null || !_ecClient.Connected)
            {
                throw new InvalidOperationException("No hay conexión EC activa");
            }

            var bytes = packet.ToBytes();
            
            // Logging detallado del paquete enviado
            OnLog?.Invoke($"[eMule] Enviando paquete:");
            OnLog?.Invoke($"[eMule]   OpCode: {packet.OpCode} (0x{(byte)packet.OpCode:X2})");
            OnLog?.Invoke($"[eMule]   Tags: {packet.Tags.Count}");
            OnLog?.Invoke($"[eMule]   Tamaño: {bytes.Length} bytes");
            OnLog?.Invoke($"[eMule]   Hex: {BitConverter.ToString(bytes).Replace("-", " ")}");
            
            await _ecStream.WriteAsync(bytes, 0, bytes.Length);
            await _ecStream.FlushAsync();
        }

        /// <summary>
        /// Recibe un paquete EC del daemon de aMule
        /// </summary>
        public async Task<ECPacket> ReceiveECPacketAsync()
        {
            if (_ecStream == null || !_ecClient.Connected)
            {
                throw new InvalidOperationException("No hay conexión EC activa");
            }

            // Leer flags (4 bytes, BIG-ENDIAN)
            var flagsBytes = new byte[4];
            var bytesRead = await _ecStream.ReadAsync(flagsBytes, 0, 4);
            if (bytesRead < 4)
            {
                throw new IOException("Conexión EC cerrada inesperadamente");
            }
            // Convertir de big-endian a uint
            uint flags = ((uint)flagsBytes[0] << 24) | ((uint)flagsBytes[1] << 16) | 
                         ((uint)flagsBytes[2] << 8) | flagsBytes[3];
            
            OnLog?.Invoke($"[eMule] Flags recibidos: 0x{flags:X8}");

            // Leer tamaño del cuerpo (4 bytes, BIG-ENDIAN)
            var sizeBytes = new byte[4];
            bytesRead = await _ecStream.ReadAsync(sizeBytes, 0, 4);
            if (bytesRead < 4)
            {
                throw new IOException("Conexión EC cerrada inesperadamente");
            }
            // Convertir de big-endian a uint
            uint bodySize = ((uint)sizeBytes[0] << 24) | ((uint)sizeBytes[1] << 16) | 
                            ((uint)sizeBytes[2] << 8) | sizeBytes[3];
            
            OnLog?.Invoke($"[eMule] Body size: {bodySize} bytes");

            // Validar tamaño razonable (máx 10 MB)
            if (bodySize > 10 * 1024 * 1024)
            {
                throw new InvalidDataException($"Tamaño de paquete EC inválido: {bodySize} bytes");
            }

            // Leer cuerpo
            var body = new byte[bodySize];
            int totalRead = 0;
            while (totalRead < bodySize)
            {
                bytesRead = await _ecStream.ReadAsync(body, totalRead, (int)(bodySize - totalRead));
                if (bytesRead == 0)
                {
                    throw new IOException("Conexión EC cerrada antes de completar lectura");
                }
                totalRead += bytesRead;
            }

            OnLog?.Invoke($"[eMule] Body recibido ({totalRead} bytes): {BitConverter.ToString(body).Replace("-", " ")}");
            
            // Guardar el body raw para acceso posterior
            _lastReceivedBody = body;
            
            var packet = ECPacket.FromBytes(flags, body);
            OnLog?.Invoke($"[eMule] Paquete parseado: OpCode={packet.OpCode}, Tags={packet.Tags.Count}");
            
            return packet;
        }

        private ECPacket BuildECPacket(ECOpCode opCode, List<ECTag> tags)
        {
            return new ECPacket
            {
                OpCode = opCode,
                Tags = tags
            };
        }

        private string ComputeMD5Hash(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private byte[] ComputeMD5HashBytes(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                return md5.ComputeHash(inputBytes);
            }
        }

        private string FindAmuleDaemon()
        {
            // Buscar amuled en rutas comunes
            var paths = new[]
            {
                "/usr/bin/amuled",
                "/usr/local/bin/amuled",
                "C:\\Program Files\\aMule\\amuled.exe",
                "C:\\Program Files (x86)\\aMule\\amuled.exe"
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Detiene todas las búsquedas activas en eMule
        /// </summary>
        public async Task StopAllSearchesAsync()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No hay conexión activa a eMule");
            }

            try
            {
                var stopPacket = BuildECPacket(ECOpCode.EC_OP_SEARCH_STOP, new List<ECTag>());
                await SendECPacketAsync(stopPacket);
                OnLog?.Invoke("[eMule] 🛑 Búsquedas activas detenidas");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[eMule] ⚠️ Error deteniendo búsquedas: {ex.Message}");
            }
        }

        /// <summary>
        /// Inicia una búsqueda en la red ed2k/Kad
        /// </summary>
        /// <param name="query">Término de búsqueda</param>
        /// <param name="searchType">Tipo de búsqueda (0=local, 1=global, 2=Kad)</param>
        /// <returns>Lista de resultados de búsqueda</returns>
        public async Task<List<EmuleSearchResult>> SearchAsync(string query, int searchType = 1)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No hay conexión activa a eMule");
            }

            try
            {
                // Detener búsquedas anteriores primero
                await StopAllSearchesAsync();
                await Task.Delay(500); // Dar tiempo a eMule para limpiar

                // Iniciar nueva búsqueda
                var searchPacket = BuildECPacket(
                    ECOpCode.EC_OP_SEARCH_START,
                    new List<ECTag>
                    {
                        new ECTag(ECTagName.EC_TAG_SEARCH_TYPE, (byte)searchType),
                        new ECTag(ECTagName.EC_TAG_SEARCH_NAME, query)
                    }
                );

                OnLog?.Invoke($"[eMule] 🔍 Iniciando búsqueda: '{query}'");
                await SendECPacketAsync(searchPacket);

                // Esperar resultados con polling
                var results = new List<EmuleSearchResult>();
                var maxPolls = 15; // Máximo 15 segundos
                var pollCount = 0;

                while (pollCount < maxPolls)
                {
                    await Task.Delay(1000);
                    pollCount++;

                    // Solicitar resultados
                    var resultsPacket = BuildECPacket(ECOpCode.EC_OP_SEARCH_RESULTS, new List<ECTag>());
                    await SendECPacketAsync(resultsPacket);

                    var response = await ReceiveECPacketAsync();
                    
                    if (response.OpCode == ECOpCode.EC_OP_SEARCH_RESULTS)
                    {
                        // Parsear resultados
                        var newResults = ParseSearchResults(response);
                        if (newResults.Count > 0)
                        {
                            results.AddRange(newResults);
                            OnLog?.Invoke($"[eMule] 📊 Poll {pollCount}: {newResults.Count} nuevos resultados (total: {results.Count})");
                        }
                    }

                    // Si llevamos 3 polls sin resultados nuevos, terminar
                    if (pollCount >= 3 && results.Count == 0)
                    {
                        break;
                    }
                }

                OnLog?.Invoke($"[eMule] ✅ Búsqueda completada: {results.Count} resultados");
                return results;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error en búsqueda eMule: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parsea los resultados de búsqueda desde un paquete EC
        /// </summary>
        private List<EmuleSearchResult> ParseSearchResults(ECPacket packet)
        {
            var results = new List<EmuleSearchResult>();

            foreach (var tag in packet.Tags)
            {
                try
                {
                    var fileName = tag.GetSubTag(ECTagName.EC_TAG_PARTFILE_NAME)?.StringValue;
                    var fileSize = tag.GetSubTag(ECTagName.EC_TAG_PARTFILE_SIZE_FULL)?.UInt64Value ?? 0;
                    var fileHash = tag.GetSubTag(ECTagName.EC_TAG_PARTFILE_HASH)?.StringValue;
                    var sourceCount = tag.GetSubTag(ECTagName.EC_TAG_PARTFILE_SOURCE_COUNT)?.UInt32Value ?? 0;

                    if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(fileHash))
                    {
                        results.Add(new EmuleSearchResult
                        {
                            FileName = fileName,
                            FileSize = (long)fileSize,
                            FileHash = fileHash,
                            FileType = Path.GetExtension(fileName)?.TrimStart('.') ?? "unknown",
                            SourceCount = (int)sourceCount,
                            CompleteSourceCount = (int)sourceCount
                        });
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"[eMule] ⚠️ Error parseando resultado: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Inicia descarga de un archivo desde eMule/ed2k
        /// </summary>
        public async Task<string> DownloadAsync(
            string fileHash,
            string fileName,
            long fileSize,
            string destinationPath,
            IProgress<DownloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No hay conexión activa a eMule");
            }

            try
            {
                // Construir paquete de descarga EC
                var downloadPacket = BuildECPacket(
                    ECOpCode.EC_OP_DOWNLOAD_SEARCH_RESULT,
                    new List<ECTag>
                    {
                        new ECTag(ECTagName.EC_TAG_PARTFILE_ED2K_LINK, BuildEd2kLink(fileHash, fileName, fileSize))
                    }
                );

                await SendECPacketAsync(downloadPacket);

                // Recibir confirmación
                var response = await ReceiveECPacketAsync();
                if (response.OpCode != ECOpCode.EC_OP_NOOP) // NOOP = OK
                {
                    throw new Exception($"eMule rechazó la descarga: {response.OpCode}");
                }

                // Monitorear progreso de descarga
                if (progress != null)
                {
                    _ = Task.Run(async () => await MonitorDownloadProgressAsync(fileHash, progress, cancellationToken), cancellationToken);
                }

                return destinationPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error iniciando descarga en eMule: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Construye un enlace ed2k para el archivo
        /// </summary>
        private string BuildEd2kLink(string fileHash, string fileName, long fileSize)
        {
            // Formato: ed2k://|file|nombre|tamaño|hash|/
            return $"ed2k://|file|{Uri.EscapeDataString(fileName)}|{fileSize}|{fileHash}|/";
        }

        /// <summary>
        /// Monitorea el progreso de una descarga
        /// </summary>
        private async Task MonitorDownloadProgressAsync(
            string fileHash,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Solicitar estado de descargas
                    var statusPacket = BuildECPacket(ECOpCode.EC_OP_GET_DLOAD_QUEUE, new List<ECTag>());
                    await SendECPacketAsync(statusPacket);

                    var response = await ReceiveECPacketAsync();
                    
                    // Buscar nuestro archivo en la respuesta
                    foreach (var tag in response.Tags)
                    {
                        if (tag.Name == ECTagName.EC_TAG_PARTFILE)
                        {
                            var hash = tag.GetSubTag(ECTagName.EC_TAG_PARTFILE_HASH)?.StringValue;
                            if (hash == fileHash)
                            {
                                var completed = tag.GetSubTag(ECTagName.EC_TAG_PARTFILE_SIZE_DONE)?.UInt64Value ?? 0;
                                var total = tag.GetSubTag(ECTagName.EC_TAG_PARTFILE_SIZE_FULL)?.UInt64Value ?? 0;
                                var speed = tag.GetSubTag(ECTagName.EC_TAG_PARTFILE_SPEED)?.UInt32Value ?? 0;

                                progress?.Report(new DownloadProgress
                                {
                                    BytesTransferred = (long)completed,
                                    TotalBytes = (long)total,
                                    TransferRate = speed,
                                    PercentComplete = total > 0 ? (double)completed / total * 100 : 0
                                });

                                // Si está completo, salir
                                if (completed >= total)
                                {
                                    return;
                                }
                            }
                        }
                    }

                    // Esperar antes de siguiente consulta
                    await Task.Delay(2000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Continuar monitoreando aunque haya errores temporales
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            DisconnectAsync().Wait(TimeSpan.FromSeconds(5));
            _ecStream?.Dispose();
            _ecClient?.Dispose();
            _amuleProcess?.Dispose();
        }
    }

    /// <summary>
    /// Progreso de descarga
    /// </summary>
    public class DownloadProgress
    {
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public uint TransferRate { get; set; }
        public double PercentComplete { get; set; }
    }

    /// <summary>
    /// Configuración del cliente eMule
    /// </summary>
    public class EMuleConfig
    {
        public string AmuleDaemonPath { get; set; }
        public bool ManageDaemon { get; set; } = true;
        public bool EnableKad { get; set; } = true;
        public int ECPort { get; set; } = 4712;
    }
}
