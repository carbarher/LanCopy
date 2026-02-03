using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace SlskDown
{
    /// <summary>
    /// Bloom Filter para verificación ultra-rápida de existencia de archivos
    /// Beneficio: 100-1000x más rápido que búsqueda en disco
    /// False positive rate: ~0.1% con configuración óptima
    /// </summary>
    public class BloomFilter
    {
        private readonly BitArray bits;
        private readonly int hashCount;
        private readonly int size;
        private long itemCount;
        
        // Métricas
        private long totalChecks;
        private long positiveResults;
        
        public BloomFilter(int expectedItems = 1_000_000, double falsePositiveRate = 0.001)
        {
            // Calcular tamaño óptimo del bit array
            size = CalculateOptimalSize(expectedItems, falsePositiveRate);
            hashCount = CalculateOptimalHashCount(expectedItems, size);
            
            bits = new BitArray(size, false);
            itemCount = 0;
            
            Console.WriteLine($"[BloomFilter] Inicializado: {size:N0} bits ({size / 8 / 1024:N0} KB), {hashCount} hashes");
        }
        
        /// <summary>
        /// Agrega un elemento al filtro
        /// </summary>
        public void Add(string item)
        {
            if (string.IsNullOrEmpty(item))
                return;
            
            var hashes = GetHashes(item);
            foreach (var hash in hashes)
            {
                bits[hash] = true;
            }
            
            itemCount++;
        }
        
        /// <summary>
        /// Verifica si un elemento PUEDE estar en el conjunto
        /// </summary>
        /// <returns>
        /// true: El elemento PUEDE estar (con pequeña probabilidad de falso positivo)
        /// false: El elemento DEFINITIVAMENTE NO está
        /// </returns>
        public bool MightContain(string item)
        {
            totalChecks++;
            
            if (string.IsNullOrEmpty(item))
                return false;
            
            var hashes = GetHashes(item);
            foreach (var hash in hashes)
            {
                if (!bits[hash])
                {
                    return false; // Definitivamente NO está
                }
            }
            
            positiveResults++;
            return true; // PUEDE estar
        }
        
        /// <summary>
        /// Limpia el filtro
        /// </summary>
        public void Clear()
        {
            bits.SetAll(false);
            itemCount = 0;
            totalChecks = 0;
            positiveResults = 0;
        }
        
        /// <summary>
        /// Obtiene múltiples hashes para un item
        /// </summary>
        private int[] GetHashes(string item)
        {
            var hashes = new int[hashCount];
            var bytes = Encoding.UTF8.GetBytes(item.ToLowerInvariant());
            
            using (var md5 = MD5.Create())
            using (var sha1 = SHA1.Create())
            {
                var hash1 = BitConverter.ToInt32(md5.ComputeHash(bytes), 0);
                var hash2 = BitConverter.ToInt32(sha1.ComputeHash(bytes), 0);
                
                for (int i = 0; i < hashCount; i++)
                {
                    // Double hashing: h(i) = h1 + i * h2
                    var combinedHash = hash1 + (i * hash2);
                    hashes[i] = Math.Abs(combinedHash % size);
                }
            }
            
            return hashes;
        }
        
        /// <summary>
        /// Calcula tamaño óptimo del bit array
        /// m = -(n * ln(p)) / (ln(2)^2)
        /// </summary>
        private int CalculateOptimalSize(int n, double p)
        {
            var m = -(n * Math.Log(p)) / Math.Pow(Math.Log(2), 2);
            return (int)Math.Ceiling(m);
        }
        
        /// <summary>
        /// Calcula número óptimo de funciones hash
        /// k = (m/n) * ln(2)
        /// </summary>
        private int CalculateOptimalHashCount(int n, int m)
        {
            var k = (m / (double)n) * Math.Log(2);
            return Math.Max(1, (int)Math.Round(k));
        }
        
        /// <summary>
        /// Obtiene estadísticas del filtro
        /// </summary>
        public BloomFilterStats GetStats()
        {
            var fillRatio = 0.0;
            var setBits = 0;
            
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i]) setBits++;
            }
            
            fillRatio = (double)setBits / bits.Length;
            
            // Estimar false positive rate actual
            var estimatedFPR = Math.Pow(fillRatio, hashCount);
            
            return new BloomFilterStats
            {
                Size = size,
                HashCount = hashCount,
                ItemCount = itemCount,
                FillRatio = fillRatio,
                EstimatedFalsePositiveRate = estimatedFPR,
                TotalChecks = totalChecks,
                PositiveResults = positiveResults,
                MemoryUsageKB = size / 8 / 1024
            };
        }
        
        /// <summary>
        /// Genera reporte de estadísticas
        /// </summary>
        public string GetStatsReport()
        {
            var stats = GetStats();
            
            return $@"
🔍 BLOOM FILTER - ESTADÍSTICAS
═══════════════════════════════════════
📊 Configuración:
├── Tamaño: {stats.Size:N0} bits ({stats.MemoryUsageKB:N0} KB)
├── Funciones hash: {stats.HashCount}
├── Elementos agregados: {stats.ItemCount:N0}
└── Ratio de llenado: {stats.FillRatio:P2}

📈 Rendimiento:
├── Verificaciones totales: {stats.TotalChecks:N0}
├── Resultados positivos: {stats.PositiveResults:N0}
├── Tasa de positivos: {(stats.TotalChecks > 0 ? (double)stats.PositiveResults / stats.TotalChecks : 0):P2}
└── FPR estimado: {stats.EstimatedFalsePositiveRate:P4}

⚡ Beneficio: {(stats.TotalChecks > 0 ? stats.TotalChecks - stats.PositiveResults : 0):N0} accesos a disco evitados
💾 Memoria: {stats.MemoryUsageKB:N0} KB ({stats.MemoryUsageKB / 1024.0:F2} MB)
";
        }
    }
    
    /// <summary>
    /// Estadísticas del Bloom Filter
    /// </summary>
    public class BloomFilterStats
    {
        public int Size { get; set; }
        public int HashCount { get; set; }
        public long ItemCount { get; set; }
        public double FillRatio { get; set; }
        public double EstimatedFalsePositiveRate { get; set; }
        public long TotalChecks { get; set; }
        public long PositiveResults { get; set; }
        public long MemoryUsageKB { get; set; }
    }
}
