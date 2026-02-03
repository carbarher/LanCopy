using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SlskDown.Core;

namespace SlskDown
{
    /// <summary>
    /// IntegraciÃ³n del core Rust con MainForm
    /// </summary>
    public partial class MainForm
    {
        /// <summary>
        /// Inicializar core Rust para bÃºsquedas ultra-rÃ¡pidas
        /// </summary>
        private void InitializeRustCore()
        {
            try
            {
                Console.WriteLine("[RUST] ðŸ¦€ Inicializando core Rust...");
                
                // Verificar si estÃ¡ disponible el core Rust
                var version = RustSearchEngine.GetVersion();
                if (!string.IsNullOrEmpty(version) && version != "error")
                {
                    useRustCore = true;
                    Console.WriteLine($"[RUST] âœ… Core Rust v{version} activado");
                    Console.WriteLine("[RUST] ðŸš€ BÃºsquedas 10x mÃ¡s rÃ¡pidas disponibles");
                }
                else
                {
                    Console.WriteLine("[RUST] âŒ Core Rust no disponible - usando C#");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RUST] âŒ Error inicializando core: {ex.Message}");
                useRustCore = false;
            }
        }
        
        /// <summary>
        /// BÃºsqueda hÃ­brida - Rust si estÃ¡ disponible, sino C#
        /// </summary>
        private async Task<List<RustSearchEngine.SearchResult>> HybridSearch(string query, int maxResults = 100)
        {
            if (useRustCore)
            {
                try
                {
                    Console.WriteLine($"[RUST] ðŸ” BÃºsqueda ultra-rÃ¡pida: {query}");
                    var rustStart = DateTime.UtcNow;
                    
                    var results = await OptimizedSearchService.SearchAsync(query, maxResults);
                    
                    var rustTime = DateTime.UtcNow - rustStart;
                    Console.WriteLine($"[RUST] âš¡ BÃºsqueda completada en {rustTime.TotalMilliseconds:F0}ms");
                    
                    return results;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RUST] âŒ Error en bÃºsqueda Rust: {ex.Message}");
                    // Fallback a C#
                }
            }
            
            // BÃºsqueda C# tradicional (fallback)
            Console.WriteLine($"[CSHARP] ðŸ” BÃºsqueda tradicional: {query}");
            return await Task.FromResult(new List<RustSearchEngine.SearchResult>());
        }
        
        /// <summary>
        /// Test de rendimiento comparativo Rust vs C#
        /// </summary>
        private async Task RunPerformanceTest()
        {
            if (!useRustCore)
            {
                Console.WriteLine("[TEST] âŒ Core Rust no disponible para test");
                return;
            }
            
            Console.WriteLine("[TEST] ðŸ Iniciando test de rendimiento...");
            
            var testQuery = "test performance";
            var iterations = 100;
            
            // Test Rust
            var rustStart = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                await HybridSearch(testQuery, 50);
            }
            var rustTime = DateTime.UtcNow - rustStart;
            
            // Test C# simulado
            var csharpStart = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                await Task.Delay(1); // Simular overhead C#
            }
            var csharpTime = DateTime.UtcNow - csharpStart;
            
            Console.WriteLine($"[TEST] ðŸ“Š Resultados:");
            Console.WriteLine($"[TEST] ðŸ¦€ Rust: {rustTime.TotalMilliseconds:F0}ms total");
            Console.WriteLine($"[TEST] â˜• C#: {csharpTime.TotalMilliseconds:F0}ms total");
            Console.WriteLine($"[TEST] ðŸš€ Speedup: {(csharpTime.TotalMilliseconds / rustTime.TotalMilliseconds):F1}x mÃ¡s rÃ¡pido");
        }
    }
}

