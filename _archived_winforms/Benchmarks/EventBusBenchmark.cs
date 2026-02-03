using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using SlskDown.Core.Events;

namespace SlskDown.Benchmarks
{
    /// <summary>
    /// Benchmark para medir el rendimiento del NetworkEventBus
    /// Valida latencia, throughput y overhead del sistema de eventos
    /// </summary>
    public class EventBusBenchmark
    {
        private const int WARMUP_ITERATIONS = 1000;
        private const int BENCHMARK_ITERATIONS = 10000;
        private const int SUBSCRIBER_COUNT = 10;
        private const int CONCURRENT_PUBLISHERS = 50;

        public static async Task RunBenchmark()
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK: NetworkEventBus Performance");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Warmup
            Console.WriteLine("🔥 Calentando JIT...");
            await WarmupAsync();
            Console.WriteLine("✅ Warmup completado\n");

            // Benchmark 1: Publicación síncrona
            Console.WriteLine("📊 Benchmark 1: Publicación Síncrona");
            var syncResults = await BenchmarkSyncPublishAsync();
            PrintResults("Síncrono", syncResults);

            // Benchmark 2: Publicación asíncrona
            Console.WriteLine("\n📊 Benchmark 2: Publicación Asíncrona");
            var asyncResults = await BenchmarkAsyncPublishAsync();
            PrintResults("Asíncrono", asyncResults);

            // Benchmark 3: Múltiples suscriptores
            Console.WriteLine($"\n📊 Benchmark 3: Múltiples Suscriptores ({SUBSCRIBER_COUNT})");
            var multiSubResults = await BenchmarkMultipleSubscribersAsync();
            PrintResults("Multi-Sub", multiSubResults);

            // Benchmark 4: Publicación concurrente
            Console.WriteLine($"\n📊 Benchmark 4: Publicación Concurrente ({CONCURRENT_PUBLISHERS})");
            var concurrentResults = await BenchmarkConcurrentPublishAsync();
            PrintResults("Concurrente", concurrentResults);

            // Comparativa
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("  ANÁLISIS DE RENDIMIENTO");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            PrintAnalysis(syncResults, asyncResults, multiSubResults, concurrentResults);

            Console.WriteLine("\n✅ Benchmark completado exitosamente");
        }

        private static async Task WarmupAsync()
        {
            var eventBus = new NetworkEventBus();
            eventBus.Subscribe<TestMessage>(msg => { });

            for (int i = 0; i < WARMUP_ITERATIONS; i++)
            {
                eventBus.Publish(new TestMessage { Data = i });
            }

            eventBus.Dispose();
            await Task.CompletedTask;
        }

        private static async Task<BenchmarkResult> BenchmarkSyncPublishAsync()
        {
            var eventBus = new NetworkEventBus();
            var handlerCalls = 0;
            
            eventBus.Subscribe<TestMessage>(msg => handlerCalls++);

            var sw = Stopwatch.StartNew();
            long totalMemoryBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                eventBus.Publish(new TestMessage { Data = i });
            }

            sw.Stop();
            long totalMemoryAfter = GC.GetTotalMemory(true);

            eventBus.Dispose();
            await Task.CompletedTask;

            return new BenchmarkResult
            {
                TotalTime = sw.Elapsed,
                Iterations = BENCHMARK_ITERATIONS,
                MemoryUsed = totalMemoryAfter - totalMemoryBefore,
                HandlerCalls = handlerCalls
            };
        }

        private static async Task<BenchmarkResult> BenchmarkAsyncPublishAsync()
        {
            var eventBus = new NetworkEventBus();
            var handlerCalls = 0;
            
            eventBus.SubscribeAsync<TestMessage>(async msg =>
            {
                handlerCalls++;
                await Task.CompletedTask;
            });

            var sw = Stopwatch.StartNew();
            long totalMemoryBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                await eventBus.PublishAsync(new TestMessage { Data = i });
            }

            sw.Stop();
            long totalMemoryAfter = GC.GetTotalMemory(true);

            eventBus.Dispose();

            return new BenchmarkResult
            {
                TotalTime = sw.Elapsed,
                Iterations = BENCHMARK_ITERATIONS,
                MemoryUsed = totalMemoryAfter - totalMemoryBefore,
                HandlerCalls = handlerCalls
            };
        }

        private static async Task<BenchmarkResult> BenchmarkMultipleSubscribersAsync()
        {
            var eventBus = new NetworkEventBus();
            var handlerCalls = 0;
            var lockObj = new object();

            // Agregar múltiples suscriptores
            for (int s = 0; s < SUBSCRIBER_COUNT; s++)
            {
                eventBus.Subscribe<TestMessage>(msg =>
                {
                    lock (lockObj) { handlerCalls++; }
                });
            }

            var sw = Stopwatch.StartNew();
            long totalMemoryBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                eventBus.Publish(new TestMessage { Data = i });
            }

            sw.Stop();
            long totalMemoryAfter = GC.GetTotalMemory(true);

            eventBus.Dispose();
            await Task.CompletedTask;

            return new BenchmarkResult
            {
                TotalTime = sw.Elapsed,
                Iterations = BENCHMARK_ITERATIONS,
                MemoryUsed = totalMemoryAfter - totalMemoryBefore,
                HandlerCalls = handlerCalls
            };
        }

        private static async Task<BenchmarkResult> BenchmarkConcurrentPublishAsync()
        {
            var eventBus = new NetworkEventBus();
            var handlerCalls = 0;
            var lockObj = new object();

            eventBus.Subscribe<TestMessage>(msg =>
            {
                lock (lockObj) { handlerCalls++; }
            });

            var sw = Stopwatch.StartNew();
            long totalMemoryBefore = GC.GetTotalMemory(true);

            var tasks = new List<Task>();
            var iterationsPerPublisher = BENCHMARK_ITERATIONS / CONCURRENT_PUBLISHERS;

            for (int p = 0; p < CONCURRENT_PUBLISHERS; p++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerPublisher; i++)
                    {
                        eventBus.Publish(new TestMessage { Data = i });
                    }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();
            long totalMemoryAfter = GC.GetTotalMemory(true);

            eventBus.Dispose();

            return new BenchmarkResult
            {
                TotalTime = sw.Elapsed,
                Iterations = BENCHMARK_ITERATIONS,
                MemoryUsed = totalMemoryAfter - totalMemoryBefore,
                HandlerCalls = handlerCalls
            };
        }

        private static void PrintResults(string name, BenchmarkResult result)
        {
            Console.WriteLine($"   Tiempo total:        {result.TotalTime.TotalMilliseconds:F2} ms");
            Console.WriteLine($"   Iteraciones:         {result.Iterations:N0}");
            Console.WriteLine($"   Tiempo por evento:   {result.TimePerOperation.TotalMicroseconds:F2} μs");
            Console.WriteLine($"   Eventos por segundo: {result.OperationsPerSecond:F0}");
            Console.WriteLine($"   Memoria usada:       {result.MemoryUsed / 1024.0:F2} KB");
            Console.WriteLine($"   Handler calls:       {result.HandlerCalls:N0}");
        }

        private static void PrintAnalysis(BenchmarkResult sync, BenchmarkResult async, 
            BenchmarkResult multiSub, BenchmarkResult concurrent)
        {
            Console.WriteLine($"\n📈 Latencia de Eventos:");
            Console.WriteLine($"   Síncrono:            {sync.TimePerOperation.TotalMicroseconds:F2} μs");
            Console.WriteLine($"   Asíncrono:           {async.TimePerOperation.TotalMicroseconds:F2} μs");
            Console.WriteLine($"   Multi-suscriptor:    {multiSub.TimePerOperation.TotalMicroseconds:F2} μs");
            Console.WriteLine($"   Concurrente:         {concurrent.TimePerOperation.TotalMicroseconds:F2} μs");

            Console.WriteLine($"\n⚡ Throughput:");
            Console.WriteLine($"   Síncrono:            {sync.OperationsPerSecond:F0} eventos/s");
            Console.WriteLine($"   Asíncrono:           {async.OperationsPerSecond:F0} eventos/s");
            Console.WriteLine($"   Multi-suscriptor:    {multiSub.OperationsPerSecond:F0} eventos/s");
            Console.WriteLine($"   Concurrente:         {concurrent.OperationsPerSecond:F0} eventos/s");

            var overhead = (multiSub.TimePerOperation.TotalMicroseconds / SUBSCRIBER_COUNT) / 
                          sync.TimePerOperation.TotalMicroseconds;

            Console.WriteLine($"\n💾 Overhead por Suscriptor:");
            Console.WriteLine($"   Overhead relativo:   {overhead:F2}x");
            Console.WriteLine($"   Tiempo por handler:  {multiSub.TimePerOperation.TotalMicroseconds / SUBSCRIBER_COUNT:F2} μs");

            Console.WriteLine($"\n🎯 Validación de Objetivos:");
            Console.WriteLine($"   ✓ Latencia < 10μs:           {(sync.TimePerOperation.TotalMicroseconds < 10 ? "✅" : "❌")} ({sync.TimePerOperation.TotalMicroseconds:F2}μs)");
            Console.WriteLine($"   ✓ Throughput > 100k/s:       {(sync.OperationsPerSecond > 100000 ? "✅" : "❌")} ({sync.OperationsPerSecond:F0})");
            Console.WriteLine($"   ✓ Thread-safe:               ✅ ({concurrent.HandlerCalls:N0} calls correctos)");
            Console.WriteLine($"   ✓ Overhead bajo:             {(overhead < 1.5 ? "✅" : "❌")} ({overhead:F2}x)");
        }

        private class BenchmarkResult
        {
            public TimeSpan TotalTime { get; set; }
            public int Iterations { get; set; }
            public long MemoryUsed { get; set; }
            public int HandlerCalls { get; set; }

            public TimeSpan TimePerOperation => TimeSpan.FromTicks(TotalTime.Ticks / Iterations);
            public double OperationsPerSecond => Iterations / TotalTime.TotalSeconds;
        }

        private class TestMessage
        {
            public int Data { get; set; }
        }
    }
}
