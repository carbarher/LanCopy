using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LanCopy;

/// <summary>
/// Herramienta de diagnóstico para verificar conectividad de LanCopy
/// Uso: dotnet run -- --diagnose [ip] [puerto]
/// </summary>
public static class Diagnostics
{
    public static async Task<int> RunAsync(string? remoteIp = null, int? remotePort = null)
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║  LanCopy Connectivity Diagnostics      ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        // 1. Verificar IP local
        Console.WriteLine("1️⃣  Local Network Configuration:");
        PrintLocalNetworkInfo();

        // 2. Verificar si el servidor está escuchando
        Console.WriteLine("\n2️⃣  Local Server Status:");
        await CheckLocalServerAsync();

        // 3. Prueba de conectividad remota
        if (!string.IsNullOrEmpty(remoteIp))
        {
            Console.WriteLine("\n3️⃣  Remote Connection Test:");
            await TestRemoteConnectionAsync(remoteIp, remotePort ?? 8742);
        }

        return 0;
    }

    private static void PrintLocalNetworkInfo()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            Console.WriteLine($"  Network: {ni.Name} ({ni.NetworkInterfaceType})");
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    Console.WriteLine($"    ✓ IPv4: {ua.Address}");
                else if (ua.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    Console.WriteLine($"    ✓ IPv6: {ua.Address}");
            }
        }
    }

    private static async Task CheckLocalServerAsync()
    {
        var port = 8742;
        Console.WriteLine($"  Checking if server is listening on :{port}...\n");

        try
        {
            using var client = new TcpClient();
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
            
            await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);
            Console.WriteLine($"  ✅ Server IS listening on localhost:{port}");
            client.Close();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"  ⏱️  Timeout: Server not responding on localhost:{port}");
            Console.WriteLine("     → Is LanCopy running? Check taskbar/system tray.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Server NOT listening on localhost:{port}");
            Console.WriteLine($"     Error: {ex.Message}");
            Console.WriteLine("     → Start LanCopy first");
        }
    }

    private static async Task TestRemoteConnectionAsync(string ip, int port)
    {
        Console.WriteLine($"  Testing connection to {ip}:{port}...\n");

        // 1. Ping check
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, 2000);
            if (reply.Status == IPStatus.Success)
                Console.WriteLine($"  ✅ Ping OK ({reply.RoundtripTime}ms)");
            else
                Console.WriteLine($"  ⚠️  Ping failed: {reply.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Ping error: {ex.Message}");
        }

        // 2. Port connectivity
        try
        {
            using var client = new TcpClient();
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            
            await client.ConnectAsync(ip, port, cts.Token);
            Console.WriteLine($"  ✅ Port {port} is open and accepting connections");
            
            // Try to read first line (handshake)
            using var stream = client.GetStream();
            var buffer = new byte[1024];
            using var readCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
            var bytesRead = await stream.ReadAsync(buffer, readCts.Token);
            Console.WriteLine($"  ✅ Received {bytesRead} bytes from server");
            
            client.Close();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"  ⏱️  Timeout: Server on {ip}:{port} not responding");
            Console.WriteLine("     → Check if LanCopy is running on that machine");
            Console.WriteLine("     → Check if port {port} is not blocked by firewall");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Cannot connect to {ip}:{port}");
            Console.WriteLine($"     Error: {ex.Message}");
            Console.WriteLine("     → Check IP address is correct");
            Console.WriteLine("     → Check firewall allows port {port}");
            Console.WriteLine("     → Check both machines are on the same network");
        }
    }
}
