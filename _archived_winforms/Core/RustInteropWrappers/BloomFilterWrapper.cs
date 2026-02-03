using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SlskDown.Core.RustInterop
{
    /// <summary>
    /// Wrapper C# para Bloom Filter implementado en Rust
    /// Permite deduplicación ultrarrápida de archivos
    /// 1M archivos en ~1.2MB RAM con 0.01% falsos positivos
    /// Si la DLL Rust no está disponible, usa implementación nativa C#
    /// </summary>
    public class BloomFilterWrapper : IDisposable
    {
        private IntPtr _filterPtr;
        private bool _disposed;
        private bool _useNativeImplementation;
        private BloomFilterNative _nativeFilter;

        [DllImport("slskdown_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr bloom_create(UIntPtr expected_items, double false_positive_rate);

        [DllImport("slskdown_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void bloom_insert(IntPtr filter, ulong hash);

        [DllImport("slskdown_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int bloom_insert_string(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string s);

        [DllImport("slskdown_core.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool bloom_contains(IntPtr filter, ulong hash);

        [DllImport("slskdown_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int bloom_contains_string(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string s);

        [DllImport("slskdown_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern UIntPtr bloom_len(IntPtr filter);

        [DllImport("slskdown_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void bloom_clear(IntPtr filter);

        [DllImport("slskdown_core.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void bloom_destroy(IntPtr filter);

        public BloomFilterWrapper(int expectedItems, double falsePositiveRate = 0.01)
        {
            if (expectedItems <= 0)
                throw new ArgumentException("Expected items must be positive", nameof(expectedItems));
            if (falsePositiveRate <= 0 || falsePositiveRate >= 1)
                throw new ArgumentException("False positive rate must be between 0 and 1", nameof(falsePositiveRate));

            // Intentar usar DLL Rust, si falla usar implementación nativa C#
            try
            {
                _filterPtr = bloom_create(new UIntPtr((uint)expectedItems), falsePositiveRate);
                if (_filterPtr == IntPtr.Zero)
                    throw new Exception("Rust DLL returned null pointer");
                _useNativeImplementation = false;
            }
            catch (DllNotFoundException)
            {
                // DLL Rust no encontrada, usar implementación nativa C#
                _nativeFilter = new BloomFilterNative(expectedItems, falsePositiveRate);
                _useNativeImplementation = true;
            }
            catch (Exception)
            {
                // Error al cargar DLL Rust, usar implementación nativa C#
                _nativeFilter = new BloomFilterNative(expectedItems, falsePositiveRate);
                _useNativeImplementation = true;
            }
        }

        public void Insert(string item)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(item))
                return;

            if (_useNativeImplementation)
            {
                _nativeFilter.Add(item);
            }
            else
            {
                int result = bloom_insert_string(_filterPtr, item);
                if (result < 0)
                    throw new Exception("Failed to insert item into Bloom filter");
            }
        }

        public bool Contains(string item)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(item))
                return false;

            if (_useNativeImplementation)
            {
                return _nativeFilter.Contains(item);
            }
            else
            {
                int result = bloom_contains_string(_filterPtr, item);
                if (result < 0)
                    throw new Exception("Failed to check item in Bloom filter");

                return result == 1; // 1 = probablemente existe, 0 = definitivamente NO existe
            }
        }

        public int Count
        {
            get
            {
                ThrowIfDisposed();
                if (_useNativeImplementation)
                {
                    return _nativeFilter.Count;
                }
                else
                {
                    return (int)bloom_len(_filterPtr);
                }
            }
        }

        public void Clear()
        {
            ThrowIfDisposed();
            if (_useNativeImplementation)
            {
                _nativeFilter.Clear();
            }
            else
            {
                bloom_clear(_filterPtr);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BloomFilterWrapper));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_useNativeImplementation)
                {
                    _nativeFilter?.Dispose();
                }
                else if (_filterPtr != IntPtr.Zero)
                {
                    bloom_destroy(_filterPtr);
                    _filterPtr = IntPtr.Zero;
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~BloomFilterWrapper()
        {
            Dispose();
        }
    }
}
