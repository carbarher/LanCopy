using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace SlskDown
{
    public class HeadlessMode
    {
        private Action<string> logAction;
        private Func<string, Task> searchAction;
        private Func<Task> rescanSharesAction;
        private Func<string, Task> browseUserAction;
        private Func<Dictionary<string, object>> getStatsAction;
        
        public HeadlessMode(
            Action<string> logger,
            Func<string, Task> search,
            Func<Task> rescan,
            Func<string, Task> browse,
            Func<Dictionary<string, object>> stats)
        {
            logAction = logger;
            searchAction = search;
            rescanSharesAction = rescan;
            browseUserAction = browse;
            getStatsAction = stats;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // COMMAND LINE INTERFACE
        // ═══════════════════════════════════════════════════════════════
        
        public async Task<int> RunHeadless(string[] args)
        {
            var rootCommand = new RootCommand("SlskDown - Headless Mode");
            
            // Comando: search
            var searchCommand = new Command("search", "Buscar archivos")
            {
                new Argument<string>("query", "Término de búsqueda"),
                new Option<bool>("--auto-download", "Descargar automáticamente los resultados"),
                new Option<int>("--max-results", () => 100, "Número máximo de resultados")
            };
            
            searchCommand.SetHandler(async (string query, bool autoDownload, int maxResults) =>
            {
                logAction?.Invoke($"🔍 Buscando: {query}");
                await searchAction(query);
                
                if (autoDownload)
                {
                    logAction?.Invoke("📥 Descargando resultados automáticamente...");
                }
            }, 
            searchCommand.Arguments[0] as Argument<string>,
            searchCommand.Options[0] as Option<bool>,
            searchCommand.Options[1] as Option<int>);
            
            rootCommand.AddCommand(searchCommand);
            
            // Comando: rescan
            var rescanCommand = new Command("rescan", "Rescanear archivos compartidos");
            
            rescanCommand.SetHandler(async () =>
            {
                logAction?.Invoke("🔄 Rescaneando shares...");
                await rescanSharesAction();
                logAction?.Invoke("✅ Rescaneo completado");
            });
            
            rootCommand.AddCommand(rescanCommand);
            
            // Comando: browse
            var browseCommand = new Command("browse", "Browsear archivos de un usuario")
            {
                new Argument<string>("username", "Nombre de usuario")
            };
            
            browseCommand.SetHandler(async (string username) =>
            {
                logAction?.Invoke($"👤 Browseando usuario: {username}");
                await browseUserAction(username);
            }, browseCommand.Arguments[0] as Argument<string>);
            
            rootCommand.AddCommand(browseCommand);
            
            // Comando: stats
            var statsCommand = new Command("stats", "Mostrar estadísticas");
            
            statsCommand.SetHandler(() =>
            {
                var stats = getStatsAction();
                
                logAction?.Invoke("📊 Estadísticas:");
                foreach (var kvp in stats)
                {
                    logAction?.Invoke($"   {kvp.Key}: {kvp.Value}");
                }
            });
            
            rootCommand.AddCommand(statsCommand);
            
            // Ejecutar
            return await rootCommand.InvokeAsync(args);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // DAEMON MODE
        // ═══════════════════════════════════════════════════════════════
        
        public async Task RunDaemon()
        {
            logAction?.Invoke("🤖 Modo daemon iniciado");
            logAction?.Invoke("   Presiona Ctrl+C para detener");
            
            // Loop infinito esperando comandos
            while (true)
            {
                await Task.Delay(1000);
                
                // Aquí se procesarían comandos de un pipe o socket
                // Por ahora solo mantiene el proceso vivo
            }
        }
    }
}
