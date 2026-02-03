using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    public class StressTest
    {
        static StressTest()
        {
            TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
        }

        private const int MaxTrackedBackgroundExceptions = 25;
        private const int BackgroundExceptionLogThreshold = 10;

        private static int successfulSearches = 0;
        private static int failedSearches = 0;
        private static int totalResults = 0;
        private static int connectionErrors = 0;
        private static int reconnections = 0;
        private static int backgroundExceptions = 0;
        private static int backgroundExceptionsSuppressed = 0;
        private static List<long> searchTimes = new List<long>();
        private static object lockObj = new object();
        private static SoulseekClient client = null!;
        private static SoulseekClientOptions clientOptions = null!;
        private static SemaphoreSlim searchSemaphore = null!;
        private static string currentUsername = null!;
        private static string currentPassword = null!;
        private static SemaphoreSlim reconnectSemaphore = new SemaphoreSlim(1, 1);
        private static ConcurrentDictionary<string, int> backgroundExceptionMessages = new ConcurrentDictionary<string, int>();

        public static async Task RunStressTest(string username, string password, int numSearches, int durationSeconds)
        {
            Console.WriteLine($"=== PRUEBA DE CARGA DEL CLIENTE SOULSEEK ===");
            Console.WriteLine($"Usuario: {username}");
            Console.WriteLine($"Búsquedas concurrentes: {numSearches}");
            Console.WriteLine($"Duración: {durationSeconds} segundos");
            Console.WriteLine($"Iniciando en 3 segundos...\n");
            
            await Task.Delay(3000);
            
            // Guardar credenciales para reconexiones futuras
            currentUsername = username;
            currentPassword = password;

            // Resetear contadores
            successfulSearches = 0;
            failedSearches = 0;
            totalResults = 0;
            connectionErrors = 0;
            reconnections = 0;
            backgroundExceptions = 0;
            backgroundExceptionsSuppressed = 0;
            searchTimes.Clear();
            backgroundExceptionMessages = new ConcurrentDictionary<string, int>();
            
            // Limitar búsquedas concurrentes para evitar saturar el servidor
            int maxConcurrent = Math.Min(numSearches, 10);
            searchSemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
            Console.WriteLine($"Límite de búsquedas simultáneas: {maxConcurrent}\n");
            
            // Conectar al servidor Soulseek con opciones de timeout
            Console.WriteLine("Conectando a Soulseek...");
            
            clientOptions = new SoulseekClientOptions(
                serverConnectionOptions: new ConnectionOptions(
                    connectTimeout: 30000,  // 30 segundos
                    inactivityTimeout: 600000  // 10 minutos (aumentado para pruebas largas)
                )
            );

            client = new SoulseekClient(clientOptions);

            // Intentar conexión con reintentos
            int maxRetries = 3;
            bool connected = false;
            
            for (int attempt = 1; attempt <= maxRetries && !connected; attempt++)
            {
                try
                {
                    if (client.State.HasFlag(SoulseekClientStates.Connected))
                    {
                        Console.WriteLine("✓ Cliente ya estaba conectado\n");
                        connected = true;
                        break;
                    }

                    Console.WriteLine($"Intento {attempt}/{maxRetries}...");
                    await client.ConnectAsync(username, password);
                    if (client.State.HasFlag(SoulseekClientStates.Connected))
                    {
                        Console.WriteLine("✓ Conectado exitosamente\n");
                        connected = true;
                    }
                }
                catch (Exception ex)
                {
                    if (client.State.HasFlag(SoulseekClientStates.Connected) || IsAlreadyConnectedException(ex))
                    {
                        Console.WriteLine($"⚠️ Excepción recibida pero el cliente está conectado: {ex.Message}\n");
                        connected = true;
                        break;
                    }

                    Console.WriteLine($"✗ Error en intento {attempt}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"   Detalle: {ex.InnerException.Message}");
                    }

                    if (attempt < maxRetries)
                    {
                        ResetClientInstance();
                        int delay = attempt * 3000; // 3s, 6s
                        Console.WriteLine($"   Reintentando en {delay/1000} segundos...\n");
                        await Task.Delay(delay);
                    }
                }
            }
            
            if (!connected)
            {
                Console.WriteLine("\n❌ No se pudo conectar después de 3 intentos.");
                Console.WriteLine("Posibles causas:");
                Console.WriteLine("  - Credenciales incorrectas");
                Console.WriteLine("  - Servidor Soulseek caído o lento");
                Console.WriteLine("  - Firewall bloqueando la conexión");
                Console.WriteLine("  - ISP bloqueando puertos P2P");
                return;
            }

            var cts = new CancellationTokenSource();
            var tasks = new List<Task>();
            var startTime = DateTime.Now;

            // Crear tareas concurrentes de búsqueda
            for (int i = 0; i < numSearches; i++)
            {
                int searchId = i + 1;
                bool useSlowRamp = numSearches > 7;
                int baseDelay = useSlowRamp ? 1500 : 700;
                int maxDelay = useSlowRamp ? 20000 : 10000;
                int initialDelay = Math.Min(maxDelay, baseDelay * searchId);
                tasks.Add(PerformSearchTask(searchId, cts.Token, initialDelay));
            }

            // Tarea de monitoreo
            tasks.Add(MonitorProgress(durationSeconds, cts.Token));

            // Cancelar después del tiempo especificado
            cts.CancelAfter(TimeSpan.FromSeconds(durationSeconds));

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Esperado cuando se cancela
                Console.WriteLine("\n⏱️ Tiempo agotado, finalizando prueba...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n⚠️ Error durante la prueba: {ex.Message}");
            }

            var endTime = DateTime.Now;
            var totalTime = (endTime - startTime).TotalSeconds;

            // Desconectar y limpiar recursos
            try
            {
                if (client != null && client.State.HasFlag(SoulseekClientStates.Connected))
                {
                    Console.WriteLine("\n⏳ Desconectando del servidor...");
                    
                    // Dar tiempo para que las conexiones pendientes se cierren
                    await Task.Delay(2000);
                    
                    client.Disconnect();
                    Console.WriteLine("✓ Desconectado del servidor");
                }
                else
                {
                    Console.WriteLine("\n⚠️ Cliente ya desconectado");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n⚠️ Error al desconectar: {ex.Message}");
            }
            finally
            {
                // Liberar recursos con delay para evitar race conditions
                try
                {
                    searchSemaphore?.Dispose();
                    
                    // Esperar un poco antes de dispose del cliente
                    await Task.Delay(1000);
                    
                    client?.Dispose();
                    Console.WriteLine("✓ Recursos liberados correctamente");
                }
                catch (Exception ex)
                {
                    // Ignorar errores de limpieza - la prueba ya terminó
                    Console.WriteLine($"⚠️ Advertencia durante limpieza: {ex.Message}");
                }
            }

            // Mostrar resultados
            PrintResults(totalTime);
        }

        private static void ResetClientInstance()
        {
            try
            {
                if (client != null)
                {
                    if (client.State.HasFlag(SoulseekClientStates.Connected))
                    {
                        client.Disconnect();
                    }

                    client.Dispose();
                }
            }
            catch
            {
                // Ignorar errores durante la limpieza
            }

            client = new SoulseekClient(clientOptions ?? new SoulseekClientOptions());
        }

        private static bool IsAlreadyConnectedException(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            var message = ex.Message?.ToLowerInvariant() ?? string.Empty;
            return message.Contains("already connected")
                || message.Contains("already logged")
                || message.Contains("client is already connected")
                || message.Contains("already in a connected state");
        }

        private static void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            var flattened = e.Exception.Flatten();

            foreach (var inner in flattened.InnerExceptions)
            {
                var current = Interlocked.Increment(ref backgroundExceptions);
                if (current > MaxTrackedBackgroundExceptions)
                {
                    Interlocked.Increment(ref backgroundExceptionsSuppressed);
                    continue;
                }

                var message = $"{inner.GetType().Name}: {inner.Message}";
                backgroundExceptionMessages.AddOrUpdate(message, 1, (_, prev) => prev < int.MaxValue ? prev + 1 : prev);

                if (current <= BackgroundExceptionLogThreshold &&
                    (inner is SocketException || inner is TimeoutException || IsRemoteDisconnection(inner)))
                {
                    Console.WriteLine($"\n⚠️ Evento de red en segundo plano: {inner.GetType().Name} - {inner.Message}");
                }
            }

            // Marcar como observada para evitar que termine el proceso
            e.SetObserved();
        }

        private static bool IsRemoteDisconnection(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            var message = ex.Message?.ToLowerInvariant() ?? string.Empty;
            return message.Contains("remote connection closed")
                || message.Contains("operation timed out after")
                || message.Contains("failed to read")
                || message.Contains("io operation aborted")
                || message.Contains("connection must be connected")
                || message.Contains("connection aborted");
        }

        private static async Task PerformSearchTask(int searchId, CancellationToken ct, int initialDelayMs)
        {
            var random = new Random(searchId);
            var searchQueries = new[]
            {
                "rock music", "jazz", "classical", "electronic", "pop",
                "blues", "metal", "indie", "hip hop", "ambient",
                "techno", "house", "trance", "dubstep", "reggae"
            };

            if (initialDelayMs > 0)
            {
                try
                {
                    await Task.Delay(initialDelayMs, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Esperar permiso para buscar (limitar concurrencia)
                    await searchSemaphore.WaitAsync(ct);

                    try
                    {
                        // Verificar que el cliente siga conectado
                        if (!client.State.HasFlag(SoulseekClientStates.Connected))
                        {
                            Interlocked.Increment(ref connectionErrors);
                            Console.WriteLine($"[Búsqueda {searchId}] ⚠️ Cliente desconectado - Intentando reconectar...");
                            
                            // Intentar reconexión
                            bool reconnected = await TryReconnectAsync(ct);
                            if (!reconnected)
                            {
                                Console.WriteLine($"[Búsqueda {searchId}] ❌ No se pudo reconectar");
                                await Task.Delay(5000, ct);
                                continue;
                            }
                            
                            Console.WriteLine($"[Búsqueda {searchId}] ✓ Reconectado exitosamente");
                        }
                        
                        var query = searchQueries[random.Next(searchQueries.Length)];
                        var sw = Stopwatch.StartNew();
                        
                        var searchOptions = new SearchOptions(
                            searchTimeout: 10000,
                            maximumPeerQueueLength: 50,
                            filterResponses: true
                        );

                        var results = await client.SearchAsync(
                            SearchQuery.FromText(query),
                            options: searchOptions,
                            cancellationToken: ct
                        );

                    sw.Stop();

                    var responses = results.Responses;

                    if (responses != null && responses.Any())
                    {
                        Interlocked.Increment(ref successfulSearches);
                        var resultCount = responses.Sum(r => r.FileCount);
                        Interlocked.Add(ref totalResults, resultCount);
                        
                        lock (lockObj)
                        {
                            searchTimes.Add(sw.ElapsedMilliseconds);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref failedSearches);
                    }

                        // Pausa entre búsquedas (3-8 segundos para no saturar)
                        await Task.Delay(random.Next(3000, 8000), ct);
                    }
                    finally
                    {
                        // Liberar el semáforo
                        searchSemaphore.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref failedSearches);
                    
                    // Detectar errores de conexión
                    if (ex.Message.Contains("not connected") || ex.Message.Contains("connection") || ex.Message.Contains("disconnected"))
                    {
                        Interlocked.Increment(ref connectionErrors);
                        Console.WriteLine($"[Búsqueda {searchId}] ⚠️ Error de conexión: {ex.Message}");
                        
                        // Intentar reconexión automática
                        bool reconnected = await TryReconnectAsync(ct);
                        if (reconnected)
                        {
                            Console.WriteLine($"[Búsqueda {searchId}] ✓ Reconectado después de error");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Búsqueda {searchId}] Error: {ex.Message}");
                    }
                    
                    await Task.Delay(5000, ct); // Pausa más larga en caso de error
                }
            }
        }

        private static async Task<bool> TryReconnectAsync(CancellationToken ct)
        {
            // Usar semáforo para evitar múltiples reconexiones simultáneas
            await reconnectSemaphore.WaitAsync(ct);
            
            try
            {
                // Verificar si ya está conectado (otra tarea pudo reconectar)
                if (client.State.HasFlag(SoulseekClientStates.Connected))
                {
                    return true;
                }
                
                Console.WriteLine("🔄 Intentando reconexión al servidor...");
                
                // Intentar desconectar primero si está en estado inconsistente
                try
                {
                    client.Disconnect();
                    await Task.Delay(1000, ct);
                }
                catch { }
                
                // Intentar reconectar (máximo 3 intentos)
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        Console.WriteLine($"   Intento de reconexión {attempt}/3...");
                        await client.ConnectAsync(currentUsername, currentPassword);
                        
                        if (client.State.HasFlag(SoulseekClientStates.Connected))
                        {
                            Interlocked.Increment(ref reconnections);
                            Console.WriteLine("   ✓ Reconexión exitosa");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (IsAlreadyConnectedException(ex))
                        {
                            Console.WriteLine($"   ⚠️ Excepción indica sesión existente, verificando estado...");
                            if (client.State.HasFlag(SoulseekClientStates.Connected))
                            {
                                Interlocked.Increment(ref reconnections);
                                Console.WriteLine("   ✓ Reconexión confirmada");
                                return true;
                            }
                        }

                        Console.WriteLine($"   ✗ Intento {attempt} falló: {ex.Message}");
                        if (attempt < 3)
                        {
                            await Task.Delay(attempt * 2000, ct); // 2s, 4s
                        }
                    }
                }
                
                Console.WriteLine("   ❌ No se pudo reconectar después de 3 intentos");
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                reconnectSemaphore.Release();
            }
        }

        private static async Task MonitorProgress(int totalSeconds, CancellationToken ct)
        {
            var startTime = DateTime.Now;
            
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(5000, ct);
                
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                var remaining = totalSeconds - elapsed;
                
                if (remaining > 0)
                {
                    var status = client?.State.HasFlag(SoulseekClientStates.Connected) == true ? "✓" : "✗";
                    Console.WriteLine($"[{elapsed:F0}s] {status} Exitosas: {successfulSearches} | Fallidas: {failedSearches} | Errores conexión: {connectionErrors} | Resultados: {totalResults} | Restante: {remaining:F0}s");
                }
            }
        }

        private static void PrintResults(double totalTime)
        {
            Console.WriteLine("\n=== RESULTADOS DE LA PRUEBA ===");
            Console.WriteLine($"Tiempo total: {totalTime:F2} segundos");
            Console.WriteLine($"Búsquedas exitosas: {successfulSearches}");
            Console.WriteLine($"Búsquedas fallidas: {failedSearches}");
            Console.WriteLine($"Errores de conexión: {connectionErrors}");
            Console.WriteLine($"Reconexiones exitosas: {reconnections}");
            Console.WriteLine($"Excepciones de fondo registradas: {Math.Min(backgroundExceptions, MaxTrackedBackgroundExceptions)}");
            if (backgroundExceptionsSuppressed > 0)
            {
                Console.WriteLine($"Excepciones de fondo adicionales suprimidas: {backgroundExceptionsSuppressed}");
            }
            Console.WriteLine($"Total de búsquedas: {successfulSearches + failedSearches}");
            Console.WriteLine($"Total de resultados obtenidos: {totalResults}");
            Console.WriteLine($"Tasa de éxito: {(successfulSearches * 100.0 / Math.Max(1, successfulSearches + failedSearches)):F2}%");
            Console.WriteLine($"Búsquedas por segundo: {(successfulSearches + failedSearches) / totalTime:F2}");
            Console.WriteLine($"Resultados por búsqueda: {(totalResults / Math.Max(1.0, successfulSearches)):F2}");

            if (searchTimes.Count > 0)
            {
                lock (lockObj)
                {
                    var sortedTimes = searchTimes.OrderBy(t => t).ToList();
                    var avg = sortedTimes.Average();
                    var min = sortedTimes.First();
                    var max = sortedTimes.Last();
                    var p50 = sortedTimes[sortedTimes.Count / 2];
                    var p95 = sortedTimes[(int)(sortedTimes.Count * 0.95)];
                    var p99 = sortedTimes[(int)(sortedTimes.Count * 0.99)];

                    Console.WriteLine("\n=== TIEMPOS DE BÚSQUEDA (ms) ===");
                    Console.WriteLine($"Mínimo: {min} ms");
                    Console.WriteLine($"Promedio: {avg:F2} ms");
                    Console.WriteLine($"Mediana (P50): {p50} ms");
                    Console.WriteLine($"P95: {p95} ms");
                    Console.WriteLine($"P99: {p99} ms");
                    Console.WriteLine($"Máximo: {max} ms");
                }
            }

            Console.WriteLine("\n=== PRUEBA COMPLETADA ===\n");
        }

        // Método de prueba rápida
        public static async Task QuickTest(string username, string password)
        {
            await RunStressTest(username, password, 5, 30); // 5 búsquedas durante 30 segundos
        }

        // Método de prueba moderada
        public static async Task ModerateTest(string username, string password)
        {
            await RunStressTest(username, password, 10, 60); // 10 búsquedas durante 60 segundos
        }

        // Método de prueba intensiva
        public static async Task IntensiveTest(string username, string password)
        {
            await RunStressTest(username, password, 20, 120); // 20 búsquedas durante 120 segundos
        }

        // Método de prueba extrema (reducido para evitar problemas)
        public static async Task ExtremeTest(string username, string password)
        {
            await RunStressTest(username, password, 15, 120); // 15 búsquedas durante 120 segundos (más conservador)
        }
    }
}
