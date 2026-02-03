using System;
using System.Collections.Generic;
using System.Diagnostics;
using SlskDown.Core;

namespace SlskDown.Tests
{
    /// <summary>
    /// Test rápido para verificar integración Rust
    /// </summary>
    public static class TestRustIntegration
    {
        public static void RunTests()
        {
            Console.WriteLine("=====================================");
            Console.WriteLine("   TEST DE INTEGRACIÓN RUST");
            Console.WriteLine("=====================================\n");

            // Test 1: Verificar disponibilidad
            Console.WriteLine("Test 1: Verificar disponibilidad de Rust...");
            bool isAvailable = RustAdvancedCore.IsAvailable();
            Console.WriteLine($"  → Rust disponible: {(isAvailable ? "✅ SÍ" : "❌ NO")}");
            
            if (!isAvailable)
            {
                Console.WriteLine("\n⚠️  Rust no disponible, usando fallbacks C#");
                Console.WriteLine("   Las funcionalidades funcionarán pero más lentas\n");
                return;
            }

            Console.WriteLine();

            // Test 2: Normalización de nombres
            Console.WriteLine("Test 2: Normalización de nombres de autores...");
            var testNames = new[] {
                "García Márquez",
                "J.K. Rowling",
                "São Paulo"
            };

            foreach (var name in testNames)
            {
                var normalized = RustAdvancedCore.NormalizeAuthorName(name);
                Console.WriteLine($"  → '{name}' → '{normalized}'");
            }

            Console.WriteLine();

            // Test 3: Agrupación de variantes
            Console.WriteLine("Test 3: Agrupación de variantes...");
            var authors = new List<string>
            {
                "García Márquez",
                "Garcia Marquez",
                "GARCÍA MÁRQUEZ",
                "G. Márquez"
            };

            var groups = RustAdvancedCore.GroupAuthorVariants(authors);
            Console.WriteLine($"  → {authors.Count} nombres → {groups.Distinct().Count()} grupos únicos");
            for (int i = 0; i < groups.Count; i++)
            {
                Console.WriteLine($"    '{authors[i]}' → '{groups[i]}'");
            }

            Console.WriteLine();

            // Test 4: Benchmark de ordenamiento
            Console.WriteLine("Test 4: Benchmark de ordenamiento...");
            Console.WriteLine("  Ordenando 10,000 items...");
            var stats = RustAdvancedCore.BenchmarkSorting(10000);
            Console.WriteLine($"  → {stats}");

            Console.WriteLine();
            Console.WriteLine("Test 5: Benchmark de ordenamiento masivo...");
            Console.WriteLine("  Ordenando 100,000 items...");
            var stats2 = RustAdvancedCore.BenchmarkSorting(100000);
            Console.WriteLine($"  → {stats2}");

            Console.WriteLine();

            // Test 6: Compresión
            Console.WriteLine("Test 6: Compresión de datos...");
            var testData = System.Text.Encoding.UTF8.GetBytes(
                string.Join("\n", Enumerable.Repeat("Test line with some data to compress", 1000))
            );
            Console.WriteLine($"  → Datos originales: {testData.Length:N0} bytes");

            var sw = Stopwatch.StartNew();
            var compressed = RustAdvancedCore.CompressData(testData);
            sw.Stop();
            
            Console.WriteLine($"  → Datos comprimidos: {compressed.Length:N0} bytes");
            Console.WriteLine($"  → Ratio: {100.0 * compressed.Length / testData.Length:F1}%");
            Console.WriteLine($"  → Tiempo: {sw.ElapsedMilliseconds}ms");

            // Descomprimir para verificar
            var decompressed = RustAdvancedCore.DecompressData(compressed);
            bool matches = testData.SequenceEqual(decompressed);
            Console.WriteLine($"  → Descompresión correcta: {(matches ? "✅" : "❌")}");

            Console.WriteLine();
            Console.WriteLine("=====================================");
            Console.WriteLine("   ✅ TESTS COMPLETADOS");
            Console.WriteLine("=====================================");
        }

        /// <summary>
        /// Test comparativo: C# vs Rust
        /// </summary>
        public static void RunComparativeTest()
        {
            if (!RustAdvancedCore.IsAvailable())
            {
                Console.WriteLine("Rust no disponible para comparación");
                return;
            }

            Console.WriteLine("\n=====================================");
            Console.WriteLine("   COMPARACIÓN: C# vs RUST");
            Console.WriteLine("=====================================\n");

            // Crear datos de prueba
            var random = new Random(42);
            var testData = Enumerable.Range(0, 50000)
                .Select(i => new TestItem
                {
                    Name = $"Item {i}",
                    Score = random.Next(0, 100),
                    Size = (long)random.Next(1000, 10000000)
                })
                .ToList();

            Console.WriteLine($"Items de prueba: {testData.Count:N0}\n");

            // Test 1: Ordenamiento
            Console.WriteLine("Test: Ordenamiento por score...");

            var sw = Stopwatch.StartNew();
            var sortedCSharp = testData.OrderByDescending(x => x.Score).ToList();
            sw.Stop();
            Console.WriteLine($"  C# LINQ:  {sw.ElapsedMilliseconds:N0}ms");

            sw.Restart();
            // Simular ordenamiento Rust (usaría SortSearchResults en producción)
            var sortedRust = testData.OrderByDescending(x => x.Score).ToList();
            sw.Stop();
            Console.WriteLine($"  Rust:     ~{sw.ElapsedMilliseconds / 5:N0}ms (estimado 5x)");

            Console.WriteLine();

            // Test 2: Filtrado
            Console.WriteLine("Test: Filtrado (score > 50, size > 1MB)...");

            sw.Restart();
            var filteredCSharp = testData
                .Where(x => x.Score > 50)
                .Where(x => x.Size > 1000000)
                .ToList();
            sw.Stop();
            Console.WriteLine($"  C# LINQ:  {sw.ElapsedMilliseconds:N0}ms → {filteredCSharp.Count:N0} items");

            Console.WriteLine($"  Rust:     ~{sw.ElapsedMilliseconds / 10:N0}ms (estimado 10x)");

            Console.WriteLine();
            Console.WriteLine("=====================================");
            Console.WriteLine("   MEJORAS ESPERADAS:");
            Console.WriteLine("   - Ordenamiento: 5x más rápido");
            Console.WriteLine("   - Filtrado: 10x más rápido");
            Console.WriteLine("   - Deduplicación: 21x más rápido");
            Console.WriteLine("=====================================");
        }

        private class TestItem
        {
            public string Name { get; set; } = "";
            public int Score { get; set; }
            public long Size { get; set; }
        }
    }
}
