using System;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown
{
    class TestRunner
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   HERRAMIENTA DE PRUEBA DE CARGA - CLIENTE SOULSEEK      ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

            // Procesar argumentos
            var positionalArgs = args.Where(a => !a.StartsWith("--", StringComparison.OrdinalIgnoreCase)).ToList();
            bool skipPause = args.Any(a => string.Equals(a, "--no-pause", StringComparison.OrdinalIgnoreCase));
            string? optionArg = args.FirstOrDefault(a => a.StartsWith("--option=", StringComparison.OrdinalIgnoreCase));
            string? optionProvided = optionArg != null ? optionArg[(optionArg.IndexOf('=') + 1)..] : null;

            // Credenciales de Soulseek
            string username = "carbar";
            string password = "";

            if (positionalArgs.Count >= 2)
            {
                username = positionalArgs[0];
                password = positionalArgs[1];
            }
            else
            {
                Console.Write("Usuario Soulseek (Enter para 'carbar'): ");
                var inputUser = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(inputUser))
                    username = inputUser;

                Console.Write("Contraseña: ");
                password = ReadPassword();
                Console.WriteLine();
            }

            Console.WriteLine($"\nUsuario: {username}\n");
            Console.WriteLine("Selecciona el tipo de prueba:");
            Console.WriteLine("1. Prueba Rápida    (5 búsquedas, 30 segundos, máx 5 simultáneas)");
            Console.WriteLine("2. Prueba Moderada  (10 búsquedas, 60 segundos, máx 10 simultáneas)");
            Console.WriteLine("3. Prueba Intensiva (20 búsquedas, 120 segundos, máx 10 simultáneas)");
            Console.WriteLine("4. Prueba Extrema   (15 búsquedas, 120 segundos, máx 10 simultáneas)");
            Console.WriteLine("5. Prueba Personalizada");
            Console.WriteLine("0. Salir\n");
            Console.WriteLine("NOTA: Las búsquedas se limitan automáticamente para no saturar el servidor.\n");

            if (optionProvided == null && positionalArgs.Count >= 3)
            {
                optionProvided = positionalArgs[2];
            }

            string? option;
            if (!string.IsNullOrWhiteSpace(optionProvided))
            {
                option = optionProvided.Trim();
                Console.WriteLine($"Opción seleccionada automáticamente: {option}");
            }
            else
            {
                Console.Write("Opción: ");
                option = Console.ReadLine()?.Trim();
            }

            if (string.IsNullOrEmpty(option))
            {
                Console.WriteLine("\n❌ Debes seleccionar una opción (1-5 o 0 para salir).");
                if (!skipPause)
                {
                    Console.WriteLine("\nPresiona cualquier tecla para salir...");
                    Console.ReadKey();
                }
                return;
            }

            try
            {
                switch (option)
                {
                    case "1":
                        Console.WriteLine("\n🚀 Iniciando Prueba Rápida...\n");
                        await StressTest.QuickTest(username, password);
                        break;
                    case "2":
                        Console.WriteLine("\n🚀 Iniciando Prueba Moderada...\n");
                        await StressTest.ModerateTest(username, password);
                        break;
                    case "3":
                        Console.WriteLine("\n🚀 Iniciando Prueba Intensiva...\n");
                        await StressTest.IntensiveTest(username, password);
                        break;
                    case "4":
                        Console.WriteLine("\n🚀 Iniciando Prueba Extrema...\n");
                        await StressTest.ExtremeTest(username, password);
                        break;
                    case "5":
                        Console.WriteLine("\n🔧 Configuración Personalizada\n");
                        Console.Write("Número de búsquedas concurrentes: ");
                        int searches = int.Parse(Console.ReadLine() ?? "5");
                        Console.Write("Duración en segundos: ");
                        int duration = int.Parse(Console.ReadLine() ?? "30");
                        Console.WriteLine($"\n🚀 Iniciando prueba personalizada ({searches} búsquedas, {duration}s)...\n");
                        await StressTest.RunStressTest(username, password, searches, duration);
                        break;
                    case "0":
                        Console.WriteLine("\n👋 Saliendo...");
                        return;
                    default:
                        Console.WriteLine($"\n❌ Opción '{option}' no válida. Debes seleccionar 1, 2, 3, 4, 5 o 0.");
                        Console.WriteLine("\nPresiona cualquier tecla para salir...");
                        Console.ReadKey();
                        return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error durante la prueba: {ex.Message}");
                Console.WriteLine($"Detalles: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Error interno: {ex.InnerException.Message}");
                }
            }

            if (!skipPause)
            {
                Console.WriteLine("\nPresiona cualquier tecla para salir...");
                Console.ReadKey();
            }
        }

        static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            }
            while (key.Key != ConsoleKey.Enter);
            return password;
        }
    }
}
