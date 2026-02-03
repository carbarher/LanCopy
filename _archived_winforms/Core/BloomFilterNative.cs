using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace SlskDown.Core
{
    /// <summary>
    /// Bloom Filter nativo en C# sin dependencias de Rust
    /// Implementación optimizada para detección de duplicados
    /// </summary>
    public class BloomFilterNative : IDisposable
    {
        private readonly BitArray _bitArray;
        private readonly int _hashFunctionCount;
        private readonly int _bitArraySize;
        private int _itemCount;

        public BloomFilterNative(int expectedItems, double falsePositiveRate)
        {
            // Calcular tamaño óptimo del bit array
            _bitArraySize = CalculateOptimalSize(expectedItems, falsePositiveRate);
            _hashFunctionCount = CalculateOptimalHashCount(expectedItems, _bitArraySize);
            _bitArray = new BitArray(_bitArraySize);
            _itemCount = 0;
        }

        private static int CalculateOptimalSize(int n, double p)
        {
            // m = -(n * ln(p)) / (ln(2)^2)
            return (int)Math.Ceiling(-(n * Math.Log(p)) / (Math.Log(2) * Math.Log(2)));
        }

        private static int CalculateOptimalHashCount(int n, int m)
        {
            // k = (m/n) * ln(2)
            return (int)Math.Ceiling((m / (double)n) * Math.Log(2));
        }

        public void Add(string item)
        {
            if (string.IsNullOrEmpty(item))
                return;

            var hashes = GetHashes(item);
            foreach (var hash in hashes)
            {
                _bitArray[hash] = true;
            }
            _itemCount++;
        }

        public bool Contains(string item)
        {
            if (string.IsNullOrEmpty(item))
                return false;

            var hashes = GetHashes(item);
            foreach (var hash in hashes)
            {
                if (!_bitArray[hash])
                    return false;
            }
            return true;
        }

        public void Clear()
        {
            _bitArray.SetAll(false);
            _itemCount = 0;
        }

        public int Count => _itemCount;

        private int[] GetHashes(string item)
        {
            var hashes = new int[_hashFunctionCount];
            var bytes = Encoding.UTF8.GetBytes(item);

            // Usar MD5 para generar múltiples hashes
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(bytes);
                
                // Generar k hashes usando double hashing
                var hash1 = BitConverter.ToInt32(hash, 0);
                var hash2 = BitConverter.ToInt32(hash, 4);

                for (int i = 0; i < _hashFunctionCount; i++)
                {
                    var combinedHash = hash1 + (i * hash2);
                    hashes[i] = Math.Abs(combinedHash % _bitArraySize);
                }
            }

            return hashes;
        }

        public double EstimatedFalsePositiveRate()
        {
            if (_itemCount == 0)
                return 0;

            // p = (1 - e^(-kn/m))^k
            var exponent = -(_hashFunctionCount * _itemCount) / (double)_bitArraySize;
            return Math.Pow(1 - Math.Exp(exponent), _hashFunctionCount);
        }

        public void Dispose()
        {
            // BitArray no requiere dispose explícito
        }
    }
}
