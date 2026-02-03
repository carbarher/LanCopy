using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using SlskDown.Core.Protocol;

namespace SlskDown.Benchmarks
{
    /// <summary>
    /// Benchmark para medir el rendimiento del SoulseekConnectionPool
    /// Compara la creación de conexiones con y sin pooling
    /// </summary>
    public class ConnectionPoolBenchmark
    {
        private const int WARMUP_ITERATIONS = 100;
        private const int BENCHMARK_ITERATIONS = 1000;
        private const int CONCURRENT_USERS = 10;

        public static async Task RunBenchmark()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK: SoulseekConnectionPool vs Creación Directa");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Warmup
            Console.WriteLine("🔥 Calentando JIT...");
            await WarmupAsync();
            Console.WriteLine("✅ Warmup completado\n");

            // Benchmark 1: Creación directa (sin pool)
            Console.WriteLine("📊 Benchmark 1: Creación Directa de Conexiones");
            Console.WriteLine("   (Sin pooling - baseline)");
            var directResults = await BenchmarkDirectConnectionsAsync();
            PrintResults("Sin Pool", directResults);

            // Benchmark 2: Con connection pool
            Console.WriteLine("\n📊 Benchmark 2: Connection Pool");
            Console.WriteLine("   (Con pooling y reutilización)");
            var poolResults = await BenchmarkConnectionPoolAsync();
            PrintResults("Con Pool", poolResults);

            // Benchmark 3: Acceso concurrente
            Console.WriteLine("\n📊 Benchmark 3: Acceso Concurrente");
            Console.WriteLine($"   ({CONCURRENT_USERS} usuarios simultáneos)");
            var concurrentResults = await BenchmarkConcurrentAccessAsync();
            PrintResults("Concurrente", concurrentResults);

            // Comparativa
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("  COMPARATIVA DE RESULTADOS");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            PrintComparison(directResults, poolResults, concurrentResults);

            Console.WriteLine("\n✅ Benchmark completado exitosamente");
        }

        private static async Task WarmupAsync()
        {
            var pool = new SoulseekConnectionPool();
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);

            for (int i = 0; i < WARMUP_ITERATIONS; i++)
            {
                var conn = await pool.GetOrCreateConnectionAsync(
                    "warmup_user",
                    endpoint,
                    async (ep) =>
                    {
                        await Task.Delay(1);
                        return new MockConnection();
                    }
                );
                conn.Dispose();
            }

            pool.Dispose();
        }

        private static async Task<BenchmarkResult> BenchmarkDirectConnectionsAsync()
        {
            var sw = Stopwatch.StartNew();
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var connections = new List<MockConnection>();
            long totalMemoryBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                // Simular creación directa de conexión
                await Task.Delay(1); // Simular overhead de conexión TCP
                var conn = new MockConnection();
                connections.Add(conn);
                
                // Simular uso y cierre
                conn.Dispose();
            }

            sw.Stop();
            long totalMemoryAfter = GC.GetTotalMemory(true);

            return new BenchmarkResult
            {
                TotalTime = sw.Elapsed,
                Iterations = BENCHMARK_ITERATIONS,
                MemoryUsed = totalMemoryAfter - totalMemoryBefore,
                CacheHits = 0,
                CacheMisses = BENCHMARK_ITERATIONS
            };
        }

        private static async Task<BenchmarkResult> BenchmarkConnectionPoolAsync()
        {
            var pool = new SoulseekConnectionPool(maxConnectionsPerUser: 5);
            var sw = Stopwatch.StartNew();
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            long totalMemoryBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var conn = await pool.GetOrCreateConnectionAsync(
                    "benchmark_user",
                    endpoint,
                    async (ep) =>
                    {
                        await Task.Delay(1); // Simular overhead de conexión TCP
                        return new MockConnection();
                    }
                );
                
                // Simular uso
                await Task.Delay(0);
                
                // Devolver al pool
                conn.Dispose();
            }

            sw.Stop();
            long totalMemoryAfter = GC.GetTotalMemory(true);
            var stats = pool.GetStatistics();

            pool.Dispose();

            return new BenchmarkResult
            {
                TotalTime = sw.Elapsed,
                Iterations = BENCHMARK_ITERATIONS,
                MemoryUsed = totalMemoryAfter - totalMemoryBefore,
                CacheHits = stats.CacheHits,
                CacheMisses = stats.CacheMisses
            };
        }

        private static async Task<BenchmarkResult> BenchmarkConcurrentAccessAsync()
        {
            var pool = new SoulseekConnectionPool(maxConnectionsPerUser: 5);
            var sw = Stopwatch.StartNew();
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            long totalMemoryBefore = GC.GetTotalMemory(true);

            var tasks = new List<Task>();
            var iterationsPerUser = BENCHMARK_ITERATIONS / CONCURRENT_USERS;

            for (int u = 0; u < CONCURRENT_USERS; u++)
            {
                var userId = $"user_{u}";
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < iterationsPerUser; i++)
                    {
                        var conn = await pool.GetOrCreateConnectionAsync(
                            userId,
                            endpoint,
                            async (ep) =>
                            {
                                await Task.Delay(1);
                                return new MockConnection();
                            }
                        );
                        await Task.Delay(0);
                        conn.Dispose();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();
            long totalMemoryAfter = GC.GetTotalMemory(true);
            var stats = pool.GetStatistics();

            pool.Dispose();

            return new BenchmarkResult
            {
                TotalTime = sw.Elapsed,
                Iterations = BENCHMARK_ITERATIONS,
                MemoryUsed = totalMemoryAfter - totalMemoryBefore,
                CacheHits = stats.CacheHits,
                CacheMisses = stats.CacheMisses
            };
        }

        private static void PrintResults(string name, BenchmarkResult result)
        {
            Console.WriteLine($"   Tiempo total:        {result.TotalTime.TotalMilliseconds:F2} ms");
            Console.WriteLine($"   Iteraciones:         {result.Iterations:N0}");
            Console.WriteLine($"   Tiempo por op:       {result.TimePerOperation.TotalMicroseconds:F2} μs");
            Console.WriteLine($"   Ops por segundo:     {result.OperationsPerSecond:F0}");
            Console.WriteLine($"   Memoria usada:       {result.MemoryUsed / 1024.0:F2} KB");
            Console.WriteLine($"   Cache hits:          {result.CacheHits:N0}");
            Console.WriteLine($"   Cache misses:        {result.CacheMisses:N0}");
            Console.WriteLine($"   Hit rate:            {result.HitRate:P1}");
        }

        private static void PrintComparison(BenchmarkResult direct, BenchmarkResult pool, BenchmarkResult concurrent)
        {
            var speedup = direct.TotalTime.TotalMilliseconds / pool.TotalTime.TotalMilliseconds;
            var memoryReduction = (1.0 - (pool.MemoryUsed / (double)direct.MemoryUsed)) * 100;

            Console.WriteLine($"\n📈 Mejora de Rendimiento:");
            Console.WriteLine($"   Speedup:             {speedup:F2}x más rápido");
            Console.WriteLine($"   Reducción memoria:   {memoryReduction:F1}%");
            Console.WriteLine($"   Hit rate (pool):     {pool.HitRate:P1}");
            Console.WriteLine($"   Hit rate (concurr):  {concurrent.HitRate:P1}");

            Console.WriteLine($"\n⚡ Throughput:");
            Console.WriteLine($"   Sin pool:            {direct.OperationsPerSecond:F0} ops/s");
            Console.WriteLine($"   Con pool:            {pool.OperationsPerSecond:F0} ops/s");
            Console.WriteLine($"   Concurrente:         {concurrent.OperationsPerSecond:F0} ops/s");

            Console.WriteLine($"\n💾 Eficiencia de Memoria:");
            Console.WriteLine($"   Sin pool:            {direct.MemoryUsed / 1024.0:F2} KB");
            Console.WriteLine($"   Con pool:            {pool.MemoryUsed / 1024.0:F2} KB");
            Console.WriteLine($"   Concurrente:         {concurrent.MemoryUsed / 1024.0:F2} KB");

            // Validar objetivos
            Console.WriteLine($"\n🎯 Validación de Objetivos:");
            Console.WriteLine($"   ✓ Objetivo speedup 2-3x:     {(speedup >= 2.0 ? "✅ ALCANZADO" : "❌ NO ALCANZADO")} ({speedup:F2}x)");
            Console.WriteLine($"   ✓ Hit rate > 90%:            {(pool.HitRate > 0.9 ? "✅ ALCANZADO" : "❌ NO ALCANZADO")} ({pool.HitRate:P1})");
            Console.WriteLine($"   ✓ Reducción memoria > 50%:   {(memoryReduction > 50 ? "✅ ALCANZADO" : "❌ NO ALCANZADO")} ({memoryReduction:F1}%)");
        }

        private class BenchmarkResult
        {
            public TimeSpan TotalTime { get; set; }
            public int Iterations { get; set; }
            public long MemoryUsed { get; set; }
            public int CacheHits { get; set; }
            public int CacheMisses { get; set; }

            public TimeSpan TimePerOperation => TimeSpan.FromTicks(TotalTime.Ticks / Iterations);
            public double OperationsPerSecond => Iterations / TotalTime.TotalSeconds;
            public double HitRate => (CacheHits + CacheMisses) > 0 
                ? CacheHits / (double)(CacheHits + CacheMisses) 
                : 0.0;
        }

        private class MockConnection : IDisposable
        {
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }
    }
}
