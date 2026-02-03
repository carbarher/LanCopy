using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SlskDown
{
    /// <summary>
    /// Script de prueba para Rust Pack 4
    /// Prueba todas las funcionalidades de forma aislada para detectar crashes
    /// </summary>
    public class TestRustPack4
    {
        private static int testsRun = 0;
        private static int testsPassed = 0;
        private static int testsFailed = 0;
        private static List<string> failedTests = new List<string>();

        public static void Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("🧪 TEST RUST PACK 4");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine();

            // Configurar manejador de excepciones no capturadas
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Console.WriteLine();
                Console.WriteLine("💥 CRASH DETECTADO:");
                Console.WriteLine($"   Tipo: {ex?.GetType().Name ?? "Unknown"}");
                Console.WriteLine($"   Mensaje: {ex?.Message ?? "No message"}");
                Console.WriteLine($"   Stack: {ex?.StackTrace ?? "No stack trace"}");
                Console.WriteLine();
                Console.WriteLine("❌ Rust Pack 4 NO es estable");
                Environment.Exit(1);
            };

            try
            {
                // Test 1: Verificar disponibilidad
                RunTest("Verificar disponibilidad", TestAvailability);

                // Test 2: LRU Cache básico
                RunTest("LRU Cache - Operaciones básicas", TestLruCacheBasic);

                // Test 3: LRU Cache - Eviction
                RunTest("LRU Cache - Eviction automática", TestLruCacheEviction);

                // Test 4: Parallel Sort - Lista pequeña
                RunTest("Parallel Sort - Lista pequeña (10 items)", () => TestParallelSort(10));

                // Test 5: Parallel Sort - Lista mediana
                RunTest("Parallel Sort - Lista mediana (100 items)", () => TestParallelSort(100));

                // Test 6: Parallel Sort - Lista grande
                RunTest("Parallel Sort - Lista grande (1000 items)", () => TestParallelSort(1000));

                // Test 7: Parallel Distinct - Lista pequeña
                RunTest("Parallel Distinct - Lista pequeña (10 items)", () => TestParallelDistinct(10));

                // Test 8: Parallel Distinct - Lista mediana
                RunTest("Parallel Distinct - Lista mediana (100 items)", () => TestParallelDistinct(100));

                // Test 9: Parallel Distinct - Lista grande
                RunTest("Parallel Distinct - Lista grande (1000 items)", () => TestParallelDistinct(1000));

                // Test 10: Parallel Filter
                RunTest("Parallel Filter - Búsqueda de patrón", TestParallelFilter);

                // Test 11: ID3v2 Parser (si hay archivos MP3)
                RunTest("ID3v2 Parser - Extracción de metadatos", TestId3Parser);

                // Resumen final
                PrintSummary();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"💥 ERROR FATAL: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                Console.WriteLine();
                Console.WriteLine("❌ Rust Pack 4 NO es estable");
                Environment.Exit(1);
            }
        }

        private static void RunTest(string testName, Action testAction)
        {
            testsRun++;
            Console.Write($"[{testsRun}] {testName}... ");

            var sw = Stopwatch.StartNew();
            try
            {
                testAction();
                sw.Stop();
                testsPassed++;
                Console.WriteLine($"✅ OK ({sw.ElapsedMilliseconds}ms)");
            }
            catch (AccessViolationException ex)
            {
                sw.Stop();
                testsFailed++;
                failedTests.Add(testName);
                Console.WriteLine($"💥 CRASH (AccessViolationException)");
                Console.WriteLine($"   Mensaje: {ex.Message}");
                throw; // Re-throw para que el manejador global lo capture
            }
            catch (Exception ex)
            {
                sw.Stop();
                testsFailed++;
                failedTests.Add(testName);
                Console.WriteLine($"❌ FAIL ({sw.ElapsedMilliseconds}ms)");
                Console.WriteLine($"   Error: {ex.Message}");
            }
        }

        private static void TestAvailability()
        {
            bool available = RustOptimizations.IsAvailable();
            if (!available)
            {
                throw new Exception("Rust Pack 4 no está disponible");
            }
        }

        private static void TestLruCacheBasic()
        {
            using (var cache = new RustOptimizations.LruCache(100))
            {
                // Put y Get
                cache.Put("key1", "value1");
                cache.Put("key2", "value2");
                cache.Put("key3", "value3");

                string val1 = cache.Get("key1");
                if (val1 != "value1")
                    throw new Exception($"Expected 'value1', got '{val1}'");

                string val2 = cache.Get("key2");
                if (val2 != "value2")
                    throw new Exception($"Expected 'value2', got '{val2}'");

                // Get de clave inexistente
                string valNull = cache.Get("nonexistent");
                if (valNull != null)
                    throw new Exception($"Expected null, got '{valNull}'");

                // Count
                int count = cache.Count;
                if (count != 3)
                    throw new Exception($"Expected count 3, got {count}");

                // Clear
                cache.Clear();
                count = cache.Count;
                if (count != 0)
                    throw new Exception($"Expected count 0 after clear, got {count}");
            }
        }

        private static void TestLruCacheEviction()
        {
            using (var cache = new RustOptimizations.LruCache(3))
            {
                cache.Put("key1", "value1");
                cache.Put("key2", "value2");
                cache.Put("key3", "value3");

                // Agregar una cuarta clave debe evict la primera
                cache.Put("key4", "value4");

                string val1 = cache.Get("key1");
                if (val1 != null)
                    throw new Exception("key1 debería haber sido evicted");

                string val4 = cache.Get("key4");
                if (val4 != "value4")
                    throw new Exception("key4 debería estar presente");
            }
        }

        private static void TestParallelSort(int size)
        {
            var list = new List<string>();
            var random = new Random(42);

            // Generar lista aleatoria
            for (int i = 0; i < size; i++)
            {
                list.Add($"Item_{random.Next(1000)}");
            }

            // Ordenar con Rust
            var sorted = RustOptimizations.ParallelSort(list);

            // Verificar que está ordenado
            for (int i = 1; i < sorted.Count; i++)
            {
                if (string.Compare(sorted[i - 1], sorted[i], StringComparison.OrdinalIgnoreCase) > 0)
                {
                    throw new Exception($"Lista no está ordenada: {sorted[i - 1]} > {sorted[i]}");
                }
            }

            // Verificar que tiene el mismo tamaño
            if (sorted.Count != list.Count)
            {
                throw new Exception($"Tamaño incorrecto: esperado {list.Count}, obtenido {sorted.Count}");
            }
        }

        private static void TestParallelDistinct(int size)
        {
            var list = new List<string>();

            // Generar lista con duplicados
            for (int i = 0; i < size; i++)
            {
                list.Add($"Item_{i % 10}"); // Solo 10 valores únicos
            }

            // Eliminar duplicados con Rust
            var distinct = RustOptimizations.ParallelDistinct(list);

            // Verificar que no hay duplicados
            var uniqueCheck = distinct.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (uniqueCheck != distinct.Count)
            {
                throw new Exception($"Hay duplicados: esperado {distinct.Count} únicos, encontrado {uniqueCheck}");
            }

            // Verificar que tiene máximo 10 elementos únicos
            if (distinct.Count > 10)
            {
                throw new Exception($"Demasiados elementos únicos: esperado ≤10, obtenido {distinct.Count}");
            }
        }

        private static void TestParallelFilter()
        {
            var list = new List<string>
            {
                "Homero",
                "Platón",
                "Aristóteles",
                "Sócrates",
                "Heródoto",
                "Tucídides"
            };

            // Filtrar con patrón
            var filtered = RustOptimizations.ParallelFilter(list, "o", caseSensitive: false);

            // Verificar que contiene solo los que tienen "o"
            foreach (var item in filtered)
            {
                if (!item.ToLower().Contains("o"))
                {
                    throw new Exception($"Item '{item}' no contiene 'o'");
                }
            }

            // Verificar que tiene al menos algunos resultados
            if (filtered.Count == 0)
            {
                throw new Exception("Filter no devolvió resultados");
            }
        }

        private static void TestId3Parser()
        {
            // Buscar archivos MP3 en el directorio actual
            var mp3Files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.mp3", SearchOption.AllDirectories)
                .Take(5)
                .ToList();

            if (mp3Files.Count == 0)
            {
                Console.Write("(sin archivos MP3, saltando) ");
                return;
            }

            foreach (var mp3File in mp3Files)
            {
                // Extraer metadatos
                var metadata = RustOptimizations.ExtractID3Metadata(mp3File);

                if (metadata != null)
                {
                    // Verificar que al menos tiene algún campo
                    if (string.IsNullOrEmpty(metadata.Artist) && 
                        string.IsNullOrEmpty(metadata.Title) &&
                        string.IsNullOrEmpty(metadata.Album))
                    {
                        throw new Exception($"Metadatos vacíos para {Path.GetFileName(mp3File)}");
                    }
                }

                // Extraer solo artista (más rápido)
                var artist = RustOptimizations.ExtractArtistFast(mp3File);
                // No fallar si no hay artista, algunos MP3s pueden no tener tags
            }
        }

        private static void PrintSummary()
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("📊 RESUMEN DE PRUEBAS");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"Total:    {testsRun}");
            Console.WriteLine($"✅ Pasadas: {testsPassed}");
            Console.WriteLine($"❌ Fallidas: {testsFailed}");
            Console.WriteLine();

            if (testsFailed > 0)
            {
                Console.WriteLine("Tests fallidos:");
                foreach (var test in failedTests)
                {
                    Console.WriteLine($"  - {test}");
                }
                Console.WriteLine();
                Console.WriteLine("❌ Rust Pack 4 NO es estable");
                Environment.Exit(1);
            }
            else
            {
                Console.WriteLine("✅ Rust Pack 4 es ESTABLE");
                Console.WriteLine();
                Console.WriteLine("Todas las pruebas pasaron correctamente.");
                Console.WriteLine("Rust Pack 4 puede ser activado de forma segura.");
                Environment.Exit(0);
            }
        }
    }
}
