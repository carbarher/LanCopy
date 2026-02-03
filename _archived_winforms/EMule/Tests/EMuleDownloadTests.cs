using System;
using System.Threading;
using System.Threading.Tasks;
using SlskDown.Core;
using SlskDown.EMule;

namespace SlskDown.EMule.Tests
{
    /// <summary>
    /// Tests para validar funcionalidad de descargas desde eMule
    /// </summary>
    public class EMuleDownloadTests
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Tests de Descargas eMule ===\n");

            try
            {
                await TestDownloadInitiation();
                await TestDownloadProgress();
                await TestDownloadCancellation();
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

        private static async Task TestDownloadInitiation()
        {
            Console.WriteLine("📥 Test 1: Iniciar Descarga");
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

                await client.ConnectAsync(credentials);

                if (!client.IsConnected)
                {
                    Console.WriteLine("❌ No se pudo conectar\n");
                    return;
                }

                Console.WriteLine("Ingresa los datos del archivo a descargar:");
                Console.Write("  Hash ed2k (32 caracteres hex): ");
                var fileHash = Console.ReadLine();
                
                Console.Write("  Nombre del archivo: ");
                var fileName = Console.ReadLine();
                
                Console.Write("  Tamaño (bytes): ");
                var fileSize = long.Parse(Console.ReadLine() ?? "0");

                Console.WriteLine($"\nIniciando descarga de: {fileName}");

                var destinationPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "eMule_Downloads",
                    fileName
                );

                // Crear directorio si no existe
                var dir = System.IO.Path.GetDirectoryName(destinationPath);
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                var result = await client.DownloadAsync(
                    fileHash,
                    fileName,
                    fileSize,
                    destinationPath,
                    null,
                    CancellationToken.None
                );

                Console.WriteLine($"✅ Descarga iniciada: {result}");
                Console.WriteLine("   Monitorea el progreso en aMule GUI\n");

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

        private static async Task TestDownloadProgress()
        {
            Console.WriteLine("📊 Test 2: Monitoreo de Progreso");
            Console.WriteLine("---------------------------------");

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

                await client.ConnectAsync(credentials);

                if (!client.IsConnected)
                {
                    Console.WriteLine("❌ No se pudo conectar\n");
                    return;
                }

                Console.WriteLine("Ingresa el hash del archivo a monitorear:");
                Console.Write("  Hash ed2k: ");
                var fileHash = Console.ReadLine();
                
                Console.Write("  Nombre del archivo: ");
                var fileName = Console.ReadLine();

                Console.WriteLine($"\nMonitoreando descarga de: {fileName}");
                Console.WriteLine("(Presiona Ctrl+C para detener)\n");

                var progress = new Progress<DownloadProgress>(p =>
                {
                    Console.Write($"\r   Progreso: {p.PercentComplete:F1}% | ");
                    Console.Write($"{FormatSize(p.BytesTransferred)} / {FormatSize(p.TotalBytes)} | ");
                    Console.Write($"Velocidad: {FormatSize((long)p.TransferRate)}/s   ");
                });

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                await client.DownloadAsync(
                    fileHash,
                    fileName,
                    0, // Tamaño desconocido
                    "",
                    progress,
                    cts.Token
                );

                Console.WriteLine("\n✅ Descarga completada\n");

                await client.DisconnectAsync();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n⚠️ Monitoreo cancelado por usuario\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}\n");
                throw;
            }
            finally
            {
                client.Dispose();
            }
        }

        private static async Task TestDownloadCancellation()
        {
            Console.WriteLine("🛑 Test 3: Cancelación de Descarga");
            Console.WriteLine("-----------------------------------");

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

                await client.ConnectAsync(credentials);

                if (!client.IsConnected)
                {
                    Console.WriteLine("❌ No se pudo conectar\n");
                    return;
                }

                Console.WriteLine("Iniciando descarga de prueba...");
                Console.WriteLine("Se cancelará automáticamente después de 5 segundos\n");

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                var progress = new Progress<DownloadProgress>(p =>
                {
                    Console.WriteLine($"   Progreso: {p.PercentComplete:F1}%");
                });

                try
                {
                    await client.DownloadAsync(
                        "0123456789ABCDEF0123456789ABCDEF", // Hash de prueba
                        "test_file.pdf",
                        1000000,
                        "test_download.pdf",
                        progress,
                        cts.Token
                    );

                    Console.WriteLine("❌ La descarga no fue cancelada\n");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("✅ Descarga cancelada correctamente\n");
                }

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
