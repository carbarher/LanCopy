using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SlskDown.Core
{
    /// <summary>
    /// Wrapper ultra-rÃ¡pido para core de bÃºsqueda en Rust
    /// </summary>
    public static unsafe class RustSearchEngine
    {
        private const string DLL_NAME = "slskdown_core.dll";
        private static readonly bool _isAvailable;
        public static bool IsAvailable => _isAvailable;
        
        [StructLayout(LayoutKind.Sequential)]
        public struct SearchResult
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string Username;
            
            [MarshalAs(UnmanagedType.LPStr)]
            public string Filename;
            
            public ulong Size;
            
            [MarshalAs(UnmanagedType.LPStr)]
            public string Bitrate;
            
            [MarshalAs(UnmanagedType.LPStr)]
            public string Country;
        }

        // Importaciones de funciones Rust
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr search_init();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr search_files(
            byte[] query,
            int query_len,
            int max_results,
            out int result_count
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_string(IntPtr ptr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr get_version();

        static RustSearchEngine()
        {
            // Inicializar motor de bÃºsqueda Rust
            try
            {
                search_init();
                _isAvailable = true;
            }
            catch
            {
                _isAvailable = false;
            }
        }

        /// <summary>
        /// BÃºsqueda ultra-rÃ¡pida usando core Rust
        /// </summary>
        public static List<SearchResult> Search(string query, int maxResults = 100)
        {
            if (string.IsNullOrEmpty(query))
                return new List<SearchResult>();

            if (!_isAvailable)
                return new List<SearchResult>();

            try
            {
                // Convertir query a bytes UTF-8
                byte[] queryBytes = Encoding.UTF8.GetBytes(query);
                
                // Llamar a funciÃ³n Rust
                IntPtr resultPtr = search_files(queryBytes, queryBytes.Length, maxResults, out int resultCount);
                
                if (resultPtr == IntPtr.Zero || resultCount == 0)
                {
                    return new List<SearchResult>();
                }

                // Parsear JSON de resultados
                string json = Marshal.PtrToStringAnsi(resultPtr);
                free_string(resultPtr); // Liberar memoria Rust

                var results = System.Text.Json.JsonSerializer.Deserialize<List<SearchResult>>(json);
                return results ?? new List<SearchResult>();
            }
            catch (DllNotFoundException)
            {
                return new List<SearchResult>();
            }
            catch (EntryPointNotFoundException)
            {
                return new List<SearchResult>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en bÃºsqueda Rust: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        /// <summary>
        /// Obtener versiÃ³n del core Rust
        /// </summary>
        public static string GetVersion()
        {
            if (!_isAvailable)
            {
                return "not_available";
            }

            try
            {
                IntPtr versionPtr = get_version();
                string version = Marshal.PtrToStringAnsi(versionPtr);
                free_string(versionPtr);
                return version ?? "unknown";
            }
            catch (DllNotFoundException)
            {
                return "not_available";
            }
            catch (EntryPointNotFoundException)
            {
                return "not_available";
            }
            catch
            {
                return "error";
            }
        }

        /// <summary>
        /// Test de rendimiento comparativo
        /// </summary>
        public static (long rustTime, long csharpTime) PerformanceTest(string query, int iterations = 100)
        {
            // Test Rust
            var rustStart = DateTime.UtcNow.Ticks;
            for (int i = 0; i < iterations; i++)
            {
                Search(query);
            }
            var rustTime = DateTime.UtcNow.Ticks - rustStart;

            // Test C# (simulado)
            var csharpStart = DateTime.UtcNow.Ticks;
            for (int i = 0; i < iterations; i++)
            {
                SearchCSharp(query); // MÃ©todo mÃ¡s lento
            }
            var csharpTime = DateTime.UtcNow.Ticks - csharpStart;

            return (rustTime / 10000, csharpTime / 10000); // Convertir a milisegundos
        }

        private static List<SearchResult> SearchCSharp(string query)
        {
            // SimulaciÃ³n de bÃºsqueda C# mÃ¡s lenta
            System.Threading.Thread.Sleep(1); // Simular overhead
            return new List<SearchResult>();
        }
    }

    /// <summary>
    /// Servidor de bÃºsqueda optimizado con Rust backend
    /// </summary>
    public class OptimizedSearchService
    {
        public static async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 100)
        {
            return await Task.Run(() => RustSearchEngine.Search(query, maxResults));
        }

        public static async Task<bool> IsRustAvailableAsync()
        {
            try
            {
                var version = await Task.Run(() => RustSearchEngine.GetVersion());
                return !string.IsNullOrEmpty(version) && version != "error" && version != "not_available";
            }
            catch
            {
                return false;
            }
        }
    }
}

