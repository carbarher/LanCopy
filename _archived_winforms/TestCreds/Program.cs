using System;
using System.Threading.Tasks;
using Soulseek;

class Program
{
    static async Task Main(string[] args)
    {
        var username = "carbar";
        var password = "Carlos66*";
        
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] === TEST DE CREDENCIALES SOULSEEK ===");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Usuario: {username}");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Password: {new string('*', password.Length)}");
        Console.WriteLine();
        
        var options = new SoulseekClientOptions(
            listenPort: 50123, // Puerto diferente para no interferir
            enableDistributedNetwork: false
        );
        
        using var client = new SoulseekClient(options);
        
        try
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Conectando a server.slsknet.org:2242...");
            
            var connectTask = client.ConnectAsync(username, password);
            var timeoutTask = Task.Delay(10000); // 10 segundos timeout
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ TIMEOUT (10 segundos)");
                Console.WriteLine("   El servidor no responde o las credenciales son incorrectas");
                Environment.Exit(1);
            }
            
            await connectTask; // Esperar a que termine para capturar excepciones
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ CONEXION EXITOSA!");
            Console.WriteLine($"   Estado: {client.State}");
            Console.WriteLine();
            Console.WriteLine("   ✅ Las credenciales son CORRECTAS");
            Console.WriteLine("   ✅ El servidor Soulseek esta ACCESIBLE");
            
            await Task.Delay(2000);
            client.Disconnect();
            
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ ERROR: {ex.GetType().Name}");
            Console.WriteLine($"   Mensaje: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
            
            Console.WriteLine();
            
            if (ex.Message.Contains("credentials") || ex.Message.Contains("password") || ex.Message.Contains("username"))
            {
                Console.WriteLine("   ⚠️ CREDENCIALES INCORRECTAS");
            }
            else if (ex.Message.Contains("timeout") || ex.Message.Contains("timed out"))
            {
                Console.WriteLine("   ⚠️ TIMEOUT - Posible bloqueo del servidor");
            }
            else
            {
                Console.WriteLine("   ⚠️ ERROR DESCONOCIDO");
            }
            
            Environment.Exit(1);
        }
    }
}
