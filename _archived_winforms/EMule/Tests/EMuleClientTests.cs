using System;
using System.Threading.Tasks;
using SlskDown.Core;
using SlskDown.EMule;

namespace SlskDown.EMule.Tests
{
    /// <summary>
    /// Tests manuales para validar integración con aMule daemon
    /// Ejecutar después de instalar y configurar amuled
    /// </summary>
    public class EMuleClientTests
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Tests de Integración eMule ===\n");

            try
            {
                await TestConnection();
                await TestAuthentication();
                await TestSearch();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error fatal: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\n=== Tests completados ===");
            Console.WriteLine("Presiona cualquier tecla para salir...");
            Console.ReadKey();
        }

        private static async Task TestConnection()
        {
            Console.WriteLine("📡 Test 1: Conexión a aMule daemon");
            Console.WriteLine("----------------------------------");

            var client = new EMuleClient
            {
                Config = new EMuleConfig
                {
                    ManageDaemon = false, // Asumimos que amuled ya está corriendo
                    EnableKad = true,
                    ECPort = 4712
                }
            };

            // Suscribirse a eventos de estado
            client.StateChanged += (sender, e) =>
            {
                Console.WriteLine($"   Estado: {e.PreviousState} → {e.CurrentState}");
                if (!string.IsNullOrEmpty(e.Message))
                {
                    Console.WriteLine($"   Mensaje: {e.Message}");
                }
            };

            try
            {
                Console.WriteLine("Conectando a localhost:4712...");

                var credentials = new NetworkCredentials
                {
                    Server = "127.0.0.1",
                    Port = 4712,
                    Password = GetECPassword()
                };

                await client.ConnectAsync(credentials);

                if (client.IsConnected)
                {
                    Console.WriteLine("✅ Conexión exitosa");

                    var stats = client.GetStatistics();
                    Console.WriteLine($"   Red: {client.NetworkName}");
                    Console.WriteLine($"   Estado: {client.State}");
                    Console.WriteLine($"   Uptime: {stats.Uptime.TotalSeconds:F0}s");
                }
                else
                {
                    Console.WriteLine("❌ Conexión fallida");
                }

                await client.DisconnectAsync();
                Console.WriteLine("✅ Desconexión exitosa\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}\n");
                throw;
            }
            finally
            {
                client.Dispose();
            }
        }

        private static async Task TestAuthentication()
        {
            Console.WriteLine("🔐 Test 2: Autenticación EC");
            Console.WriteLine("---------------------------");

            var client = new EMuleClient
            {
                Config = new EMuleConfig
                {
                    ManageDaemon = false,
                    ECPort = 4712
                }
            };

            try
            {
                var credentials = new NetworkCredentials
                {
                    Server = "127.0.0.1",
                    Port = 4712,
                    Password = GetECPassword()
                };

                Console.WriteLine("Autenticando...");
                await client.ConnectAsync(credentials);

                if (client.State == NetworkConnectionState.LoggedIn)
                {
                    Console.WriteLine("✅ Autenticación exitosa");
                    Console.WriteLine($"   Estado final: {client.State}\n");
                }
                else
                {
                    Console.WriteLine($"❌ Autenticación fallida. Estado: {client.State}\n");
                }

                await client.DisconnectAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"❌ Autenticación rechazada: {ex.Message}");
                Console.WriteLine("   Verifica la contraseña EC en ~/.aMule/amule.conf\n");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}\n");
                throw;
            }
            finally
            {
                client.Dispose();
            }
        }

        private static async Task TestSearch()
        {
            Console.WriteLine("🔍 Test 3: Búsqueda básica");
            Console.WriteLine("--------------------------");

            var client = new EMuleClient
            {
                Config = new EMuleConfig
                {
                    ManageDaemon = false,
                    EnableKad = true,
                    ECPort = 4712
                }
            };

            try
            {
                var credentials = new NetworkCredentials
                {
                    Server = "127.0.0.1",
                    Port = 4712,
                    Password = GetECPassword()
                };

                await client.ConnectAsync(credentials);

                if (!client.IsConnected)
                {
                    Console.WriteLine("❌ No se pudo conectar para realizar búsqueda\n");
                    return;
                }

                var searchProvider = new EMuleSearchProvider(client);

                // Suscribirse a eventos de resultados
                int resultsCount = 0;
                searchProvider.ResultsReceived += (sender, e) =>
                {
                    resultsCount += e.Results.Count;
                    foreach (var result in e.Results)
                    {
                        Console.WriteLine($"   📄 {result.FileName} ({FormatSize(result.SizeBytes)})");
                    }
                };

                searchProvider.SearchCompleted += (sender, e) =>
                {
                    Console.WriteLine($"\n   Búsqueda {e.Status}: {e.TotalResults} resultados en {e.Duration.TotalSeconds:F1}s");
                };

                Console.WriteLine("Buscando 'machine learning'...");

                var request = new SearchRequest
                {
                    Query = "machine learning",
                    Filters = new SearchFilters
                    {
                        FileType = FileType.Document,
                        MinSizeBytes = 100 * 1024 // 100 KB
                    },
                    MaxResults = 50,
                    Timeout = TimeSpan.FromSeconds(15)
                };

                var response = await searchProvider.SearchAsync(request);

                if (response.Status == SearchStatus.Completed)
                {
                    Console.WriteLine($"✅ Búsqueda completada: {response.Results.Count} resultados");
                }
                else
                {
                    Console.WriteLine($"⚠️ Búsqueda {response.Status}");
                    if (!string.IsNullOrEmpty(response.ErrorMessage))
                    {
                        Console.WriteLine($"   Error: {response.ErrorMessage}");
                    }
                }

                Console.WriteLine();

                await client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}\n");
                throw;
            }
            finally
            {
                client.Dispose();
            }
        }

        private static string GetECPassword()
        {
            Console.Write("Ingresa la contraseña EC de aMule: ");
            var password = Console.ReadLine();
            return password ?? "";
        }

        private static string FormatSize(long bytes)
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
    }
}
