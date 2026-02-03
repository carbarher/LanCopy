// <copyright file="GCHelper.cs" company="SlskDown">
//     Utilidades para gestión explícita de Garbage Collection
// </copyright>

using System;
using System.Diagnostics;

namespace SlskDown.Core
{
    /// <summary>
    /// Utilidades para gestión explícita de GC.
    /// Inspirado en las llamadas a gc.collect() de Nicotine+ después de operaciones pesadas.
    /// </summary>
    public static class GCHelper
    {
        /// <summary>
        /// Fuerza una recolección completa de GC.
        /// Útil después de liberar grandes estructuras de datos.
        /// </summary>
        public static void ForceCollect(string context = null)
        {
            var before = GC.GetTotalMemory(false);
            var sw = Stopwatch.StartNew();

            // Recolección completa de generación 2
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);

            sw.Stop();
            var after = GC.GetTotalMemory(false);
            var freed = before - after;

            var message = $"💾 GC forzado: {FormatBytes(freed)} liberados en {sw.ElapsedMilliseconds}ms";
            if (!string.IsNullOrEmpty(context))
                message += $" ({context})";

            Console.WriteLine(message);
        }

        /// <summary>
        /// Fuerza GC solo si la memoria usada supera un umbral.
        /// </summary>
        public static void CollectIfNeeded(long thresholdBytes = 500 * 1024 * 1024, string context = null)
        {
            var current = GC.GetTotalMemory(false);
            
            if (current > thresholdBytes)
            {
                ForceCollect(context ?? $"umbral {FormatBytes(thresholdBytes)} superado");
            }
        }

        /// <summary>
        /// Obtiene información de memoria actual.
        /// </summary>
        public static MemoryInfo GetMemoryInfo()
        {
            return new MemoryInfo
            {
                TotalMemory = GC.GetTotalMemory(false),
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                TotalAllocated = GC.GetTotalAllocatedBytes(precise: false)
            };
        }

        /// <summary>
        /// Registra estadísticas de memoria.
        /// </summary>
        public static void LogMemoryStats(string context = null)
        {
            var info = GetMemoryInfo();
            var prefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
            
            Console.WriteLine($"{prefix}📊 Memoria: {FormatBytes(info.TotalMemory)} | " +
                            $"GC: Gen0={info.Gen0Collections} Gen1={info.Gen1Collections} Gen2={info.Gen2Collections} | " +
                            $"Total Allocated: {FormatBytes(info.TotalAllocated)}");
        }

        /// <summary>
        /// Compacta el Large Object Heap.
        /// Útil después de liberar muchos objetos grandes.
        /// </summary>
        public static void CompactLOH()
        {
            var before = GC.GetTotalMemory(false);
            var sw = Stopwatch.StartNew();

            // Habilitar compactación de LOH
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = 
                System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();

            sw.Stop();
            var after = GC.GetTotalMemory(false);
            var freed = before - after;

            Console.WriteLine($"💾 LOH compactado: {FormatBytes(freed)} liberados en {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Formatea bytes en formato legible.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public class MemoryInfo
        {
            public long TotalMemory { get; set; }
            public int Gen0Collections { get; set; }
            public int Gen1Collections { get; set; }
            public int Gen2Collections { get; set; }
            public long TotalAllocated { get; set; }
        }
    }
}
