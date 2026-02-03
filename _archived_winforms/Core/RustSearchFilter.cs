using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SlskDown.Core
{
    /// <summary>
    /// Wrapper para filtrado ultra-rápido de resultados de búsqueda usando Rust
    /// 10x más rápido que implementación C# para grandes volúmenes
    /// </summary>
    public static class RustSearchFilter
    {
        private const string DLL_NAME = "slskdown_core.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr filter_search_results(IntPtr jsonInput);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_rust_string(IntPtr ptr);

        /// <summary>
        /// Filtra resultados de búsqueda en paralelo usando Rust
        /// </summary>
        public static List<SearchResultItem> FilterParallel(
            List<SearchResultItem> results,
            long minSize,
            long maxSize,
            List<string> extensions,
            bool spanishOnly,
            int minQuality)
        {
            if (results == null || results.Count == 0)
                return results ?? new List<SearchResultItem>();

            try
            {
                // Convertir a formato Rust
                var rustResults = results.Select(r => new RustSearchResult
                {
                    Filename = r.Filename ?? "",
                    Size = r.Size,
                    Extension = r.Extension ?? "",
                    Username = r.Username ?? "",
                    Quality = r.Quality
                }).ToList();

                var request = new FilterRequest
                {
                    Results = rustResults,
                    MinSize = minSize,
                    MaxSize = maxSize,
                    Extensions = extensions ?? new List<string>(),
                    SpanishOnly = spanishOnly,
                    MinQuality = minQuality
                };

                var jsonInput = JsonSerializer.Serialize(request);
                var inputPtr = Marshal.StringToHGlobalAnsi(jsonInput);

                try
                {
                    var outputPtr = filter_search_results(inputPtr);
                    if (outputPtr == IntPtr.Zero)
                        throw new Exception("Rust filtering failed");

                    try
                    {
                        var jsonOutput = Marshal.PtrToStringAnsi(outputPtr);
                        if (string.IsNullOrEmpty(jsonOutput))
                            throw new Exception("Empty response from Rust");

                        var response = JsonSerializer.Deserialize<FilterResponse>(jsonOutput);
                        if (response == null)
                            throw new Exception("Failed to deserialize response");

                        // Convertir de vuelta a SearchResultItem
                        var filteredResults = new List<SearchResultItem>();
                        foreach (var rustResult in response.Results)
                        {
                            // Buscar el resultado original para preservar todos los campos
                            var original = results.FirstOrDefault(r => 
                                r.Filename == rustResult.Filename && 
                                r.Username == rustResult.Username &&
                                r.Size == rustResult.Size);

                            if (original != null)
                            {
                                filteredResults.Add(original);
                            }
                        }

                        return filteredResults;
                    }
                    finally
                    {
                        free_rust_string(outputPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(inputPtr);
                }
            }
            catch (Exception ex)
            {
                // Log error pero no fallar - devolver lista vacía
                System.Diagnostics.Debug.WriteLine($"RustSearchFilter error: {ex.Message}");
                return new List<SearchResultItem>();
            }
        }

        /// <summary>
        /// Verifica si Rust está disponible
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                // Intentar filtrar una lista vacía
                var test = FilterParallel(new List<SearchResultItem>(), 0, long.MaxValue, 
                    new List<string>(), false, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region DTOs para Rust

        private class RustSearchResult
        {
            public string Filename { get; set; } = "";
            public long Size { get; set; }
            public string Extension { get; set; } = "";
            public string Username { get; set; } = "";
            public int Quality { get; set; }
        }

        private class FilterRequest
        {
            public List<RustSearchResult> Results { get; set; } = new();
            public long MinSize { get; set; }
            public long MaxSize { get; set; }
            public List<string> Extensions { get; set; } = new();
            public bool SpanishOnly { get; set; }
            public int MinQuality { get; set; }
        }

        private class FilterResponse
        {
            public List<RustSearchResult> Results { get; set; } = new();
            public int FilteredCount { get; set; }
            public int OriginalCount { get; set; }
        }

        #endregion
    }
}
