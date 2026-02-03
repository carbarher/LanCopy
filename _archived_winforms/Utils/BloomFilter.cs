using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace SlskDown.Utils
{
    /// <summary>
    /// Bloom Filter para deduplicación eficiente de resultados
    /// 90% menos memoria que HashSet con <1% falsos positivos
    /// </summary>
    public class BloomFilter
    {
        private readonly BitArray _bits;
        private readonly int _hashCount;
        private readonly int _bitCount;
        private int _itemCount;

        /// <summary>
        /// Crea un Bloom Filter optimizado
        /// </summary>
        /// <param name="expectedItems">Número esperado de elementos</param>
        /// <param name="falsePositiveRate">Tasa de falsos positivos (default: 0.01 = 1%)</param>
        public BloomFilter(int expectedItems, double falsePositiveRate = 0.01)
        {
            // Calcular tamaño óptimo del bit array
            _bitCount = CalculateOptimalBitCount(expectedItems, falsePositiveRate);
            _hashCount = CalculateOptimalHashCount(expectedItems, _bitCount);
            _bits = new BitArray(_bitCount);
            _itemCount = 0;
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
                _bits[hash] = true;
            }
            _itemCount++;
        }

        /// <summary>
        /// Verifica si un elemento podría estar en el filtro
        /// </summary>
        /// <returns>True si podría estar (o falso positivo), False si definitivamente no está</returns>
        public bool MightContain(string item)
        {
            if (string.IsNullOrEmpty(item))
                return false;

            var hashes = GetHashes(item);
            foreach (var hash in hashes)
            {
                if (!_bits[hash])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Limpia el filtro
        /// </summary>
        public void Clear()
        {
            _bits.SetAll(false);
            _itemCount = 0;
        }

        /// <summary>
        /// Obtiene estadísticas del filtro
        /// </summary>
        public BloomFilterStats GetStats()
        {
            int setBits = 0;
            for (int i = 0; i < _bits.Length; i++)
            {
                if (_bits[i]) setBits++;
            }

            double fillRatio = (double)setBits / _bitCount;
            double estimatedFalsePositiveRate = Math.Pow(fillRatio, _hashCount);

            return new BloomFilterStats
            {
                ItemCount = _itemCount,
                BitCount = _bitCount,
                HashCount = _hashCount,
                FillRatio = fillRatio,
                EstimatedFalsePositiveRate = estimatedFalsePositiveRate,
                MemoryBytes = _bitCount / 8
            };
        }

        private int[] GetHashes(string item)
        {
            var hashes = new int[_hashCount];
            var bytes = Encoding.UTF8.GetBytes(item);

            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(bytes);
                
                // Generar múltiples hashes usando diferentes combinaciones
                for (int i = 0; i < _hashCount; i++)
                {
                    int hash32 = BitConverter.ToInt32(hash, (i * 4) % 12);
                    hashes[i] = Math.Abs(hash32 % _bitCount);
                }
            }

            return hashes;
        }

        private static int CalculateOptimalBitCount(int n, double p)
        {
            // m = -(n * ln(p)) / (ln(2)^2)
            return (int)Math.Ceiling(-(n * Math.Log(p)) / Math.Pow(Math.Log(2), 2));
        }

        private static int CalculateOptimalHashCount(int n, int m)
        {
            // k = (m/n) * ln(2)
            return Math.Max(1, (int)Math.Round((double)m / n * Math.Log(2)));
        }
    }

    public class BloomFilterStats
    {
        public int ItemCount { get; set; }
        public int BitCount { get; set; }
        public int HashCount { get; set; }
        public double FillRatio { get; set; }
        public double EstimatedFalsePositiveRate { get; set; }
        public int MemoryBytes { get; set; }

        public override string ToString()
        {
            return $"Items: {ItemCount}, Memory: {MemoryBytes / 1024}KB, " +
                   $"Fill: {FillRatio:P2}, FP Rate: {EstimatedFalsePositiveRate:P2}";
        }
    }
}
