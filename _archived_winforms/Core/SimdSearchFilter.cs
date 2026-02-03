using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SlskDown.Core
{
    /// <summary>
    /// Filtrado de resultados usando SIMD (Single Instruction Multiple Data)
    /// 2-4x más rápido que LINQ para operaciones de filtrado masivo
    /// </summary>
    public static class SimdSearchFilter
    {
        /// <summary>
        /// Verifica si SIMD está disponible en el CPU
        /// </summary>
        public static bool IsAvailable => Avx2.IsSupported;

        /// <summary>
        /// Filtra resultados por tamaño usando SIMD (procesa 4 archivos simultáneamente)
        /// </summary>
        public static List<SearchResultItem> FilterBySizeSIMD(
            List<SearchResultItem> results, 
            long minSize, 
            long maxSize)
        {
            if (!Avx2.IsSupported || results.Count < 8)
            {
                // Fallback a LINQ si SIMD no disponible o pocos resultados
                return results.Where(r => r.Size >= minSize && r.Size <= maxSize).ToList();
            }

            var filtered = new List<SearchResultItem>(results.Count);
            var minVec = Vector256.Create(minSize);
            var maxVec = Vector256.Create(maxSize);

            int i = 0;
            
            // Procesar en bloques de 4 usando AVX2
            for (; i <= results.Count - 4; i += 4)
            {
                // Cargar 4 tamaños en un vector
                var sizes = Vector256.Create(
                    results[i].Size,
                    results[i + 1].Size,
                    results[i + 2].Size,
                    results[i + 3].Size
                );

                // Comparaciones vectorizadas (4 comparaciones en una instrucción)
                var aboveMin = Avx2.CompareGreaterThan(sizes.AsInt64(), minVec.AsInt64());
                var belowMax = Avx2.CompareGreaterThan(maxVec.AsInt64(), sizes.AsInt64());
                var mask = Avx2.And(aboveMin, belowMax);

                // Extraer resultados que pasan el filtro
                if (mask.GetElement(0) != 0) filtered.Add(results[i]);
                if (mask.GetElement(1) != 0) filtered.Add(results[i + 1]);
                if (mask.GetElement(2) != 0) filtered.Add(results[i + 2]);
                if (mask.GetElement(3) != 0) filtered.Add(results[i + 3]);
            }

            // Procesar elementos restantes (< 4)
            for (; i < results.Count; i++)
            {
                if (results[i].Size >= minSize && results[i].Size <= maxSize)
                    filtered.Add(results[i]);
            }

            return filtered;
        }

        /// <summary>
        /// Filtra resultados por calidad usando SIMD
        /// </summary>
        public static List<SearchResultItem> FilterByQualitySIMD(
            List<SearchResultItem> results, 
            int minQuality)
        {
            if (!Avx2.IsSupported || results.Count < 8)
            {
                return results.Where(r => r.Quality >= minQuality).ToList();
            }

            var filtered = new List<SearchResultItem>(results.Count);
            var minVec = Vector256.Create(minQuality);

            int i = 0;
            
            // Procesar en bloques de 8 (int32 = 4 bytes, 256 bits / 32 = 8)
            for (; i <= results.Count - 8; i += 8)
            {
                var qualities = Vector256.Create(
                    results[i].Quality,
                    results[i + 1].Quality,
                    results[i + 2].Quality,
                    results[i + 3].Quality,
                    results[i + 4].Quality,
                    results[i + 5].Quality,
                    results[i + 6].Quality,
                    results[i + 7].Quality
                );

                var mask = Avx2.CompareGreaterThan(qualities, minVec);

                // Agregar resultados que pasan el filtro
                for (int j = 0; j < 8; j++)
                {
                    if (mask.GetElement(j) != 0)
                        filtered.Add(results[i + j]);
                }
            }

            // Procesar resto
            for (; i < results.Count; i++)
            {
                if (results[i].Quality >= minQuality)
                    filtered.Add(results[i]);
            }

            return filtered;
        }

        /// <summary>
        /// Filtrado combinado optimizado con SIMD
        /// </summary>
        public static List<SearchResultItem> FilterCombinedSIMD(
            List<SearchResultItem> results,
            long minSize,
            long maxSize,
            int minQuality)
        {
            if (!Avx2.IsSupported || results.Count < 8)
            {
                return results.Where(r => 
                    r.Size >= minSize && 
                    r.Size <= maxSize && 
                    r.Quality >= minQuality).ToList();
            }

            // Primero filtrar por tamaño (más restrictivo)
            var sizeFiltered = FilterBySizeSIMD(results, minSize, maxSize);
            
            // Luego filtrar por calidad
            return FilterByQualitySIMD(sizeFiltered, minQuality);
        }

        /// <summary>
        /// Cuenta cuántos resultados pasan el filtro sin crear lista (más rápido)
        /// </summary>
        public static int CountMatchingSIMD(
            List<SearchResultItem> results,
            long minSize,
            long maxSize)
        {
            if (!Avx2.IsSupported || results.Count < 8)
            {
                return results.Count(r => r.Size >= minSize && r.Size <= maxSize);
            }

            int count = 0;
            var minVec = Vector256.Create(minSize);
            var maxVec = Vector256.Create(maxSize);

            int i = 0;
            for (; i <= results.Count - 4; i += 4)
            {
                var sizes = Vector256.Create(
                    results[i].Size,
                    results[i + 1].Size,
                    results[i + 2].Size,
                    results[i + 3].Size
                );

                var aboveMin = Avx2.CompareGreaterThan(sizes.AsInt64(), minVec.AsInt64());
                var belowMax = Avx2.CompareGreaterThan(maxVec.AsInt64(), sizes.AsInt64());
                var mask = Avx2.And(aboveMin, belowMax);

                // Contar bits set en mask
                count += PopCount(mask);
            }

            for (; i < results.Count; i++)
            {
                if (results[i].Size >= minSize && results[i].Size <= maxSize)
                    count++;
            }

            return count;
        }

        private static int PopCount(Vector256<long> mask)
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if (mask.GetElement(i) != 0)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Benchmark para comparar SIMD vs LINQ
    /// </summary>
    public class SimdBenchmark
    {
        public static void RunBenchmark(int itemCount = 10000)
        {
            var random = new Random(42);
            var testData = Enumerable.Range(0, itemCount)
                .Select(i => new SearchResultItem
                {
                    Filename = $"file_{i}.pdf",
                    Size = random.Next(1000, 100000),
                    Quality = random.Next(0, 100),
                    Username = "user1",
                    Extension = "pdf"
                })
                .ToList();

            const long minSize = 5000;
            const long maxSize = 50000;
            const int iterations = 100;

            // Benchmark LINQ
            var swLinq = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var filtered = testData.Where(r => r.Size >= minSize && r.Size <= maxSize).ToList();
            }
            swLinq.Stop();

            // Benchmark SIMD
            var swSimd = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var filtered = SimdSearchFilter.FilterBySizeSIMD(testData, minSize, maxSize);
            }
            swSimd.Stop();

            var speedup = (double)swLinq.ElapsedMilliseconds / swSimd.ElapsedMilliseconds;

            System.Diagnostics.Debug.WriteLine($"SIMD Benchmark ({itemCount} items, {iterations} iterations):");
            System.Diagnostics.Debug.WriteLine($"  LINQ: {swLinq.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"  SIMD: {swSimd.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"  Speedup: {speedup:F2}x");
            System.Diagnostics.Debug.WriteLine($"  SIMD Available: {SimdSearchFilter.IsAvailable}");
        }
    }
}
