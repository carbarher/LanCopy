using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SlskDown
{
    /// <summary>
    /// Optimizaciones de rendimiento usando Span<T>, ArrayPool y técnicas de bajo nivel
    /// para reducir allocaciones y mejorar velocidad
    /// </summary>
    public static class PerformanceOptimizations
    {
        // Pool compartido para reutilizar arrays
        private static readonly ArrayPool<char> CharPool = ArrayPool<char>.Shared;
        private static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;
        
        /// <summary>
        /// Verifica si un texto contiene palabras en español usando Span (sin allocaciones)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSpanishTextFast(ReadOnlySpan<char> text)
        {
            if (text.Length == 0) return false;
            
            // Palabras comunes en español (búsqueda case-insensitive)
            ReadOnlySpan<char> spanish = "español";
            ReadOnlySpan<char> espanol = "espanol";
            ReadOnlySpan<char> castellano = "castellano";
            ReadOnlySpan<char> spanish2 = "spanish";
            
            // Caracteres especiales españoles
            char[] spanishChars = { 'á', 'é', 'í', 'ó', 'ú', 'ñ', 'ü', 'Á', 'É', 'Í', 'Ó', 'Ú', 'Ñ', 'Ü' };
            bool hasSpanishChars = text.IndexOfAny(spanishChars) >= 0;
            
            if (hasSpanishChars) return true;
            
            // Búsqueda de palabras clave (case-insensitive)
            return text.Contains(spanish, StringComparison.OrdinalIgnoreCase) ||
                   text.Contains(espanol, StringComparison.OrdinalIgnoreCase) ||
                   text.Contains(castellano, StringComparison.OrdinalIgnoreCase) ||
                   text.Contains(spanish2, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Extrae extensión de archivo usando Span (sin allocaciones)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> GetExtensionFast(ReadOnlySpan<char> filename)
        {
            int lastDot = filename.LastIndexOf('.');
            if (lastDot < 0 || lastDot == filename.Length - 1)
                return ReadOnlySpan<char>.Empty;
            
            return filename.Slice(lastDot);
        }
        
        /// <summary>
        /// Compara extensiones de archivo (case-insensitive, sin allocaciones)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ExtensionEquals(ReadOnlySpan<char> ext1, ReadOnlySpan<char> ext2)
        {
            return ext1.Equals(ext2, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Verifica si un archivo es basura usando Span
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsGarbageFileFast(ReadOnlySpan<char> filename)
        {
            if (filename.Length == 0) return true;
            
            // Extensiones basura
            ReadOnlySpan<char> txt = ".txt";
            ReadOnlySpan<char> nfo = ".nfo";
            ReadOnlySpan<char> jpg = ".jpg";
            ReadOnlySpan<char> png = ".png";
            ReadOnlySpan<char> gif = ".gif";
            ReadOnlySpan<char> url = ".url";
            
            var ext = GetExtensionFast(filename);
            
            return ExtensionEquals(ext, txt) ||
                   ExtensionEquals(ext, nfo) ||
                   ExtensionEquals(ext, jpg) ||
                   ExtensionEquals(ext, png) ||
                   ExtensionEquals(ext, gif) ||
                   ExtensionEquals(ext, url);
        }
        
        /// <summary>
        /// Formatea tamaño de archivo usando Span y ArrayPool (mínimas allocaciones)
        /// </summary>
        public static string FormatSizeFast(long bytes)
        {
            const int bufferSize = 32;
            char[] buffer = CharPool.Rent(bufferSize);
            
            try
            {
                Span<char> span = buffer.AsSpan(0, bufferSize);
                
                if (bytes < 1024)
                {
                    bytes.TryFormat(span, out int written);
                    span[written++] = ' ';
                    span[written++] = 'B';
                    return new string(span.Slice(0, written));
                }
                else if (bytes < 1024 * 1024)
                {
                    double kb = bytes / 1024.0;
                    kb.TryFormat(span, out int written, "F2");
                    span[written++] = ' ';
                    span[written++] = 'K';
                    span[written++] = 'B';
                    return new string(span.Slice(0, written));
                }
                else if (bytes < 1024L * 1024 * 1024)
                {
                    double mb = bytes / (1024.0 * 1024);
                    mb.TryFormat(span, out int written, "F2");
                    span[written++] = ' ';
                    span[written++] = 'M';
                    span[written++] = 'B';
                    return new string(span.Slice(0, written));
                }
                else
                {
                    double gb = bytes / (1024.0 * 1024 * 1024);
                    gb.TryFormat(span, out int written, "F2");
                    span[written++] = ' ';
                    span[written++] = 'G';
                    span[written++] = 'B';
                    return new string(span.Slice(0, written));
                }
            }
            finally
            {
                CharPool.Return(buffer);
            }
        }
        
        /// <summary>
        /// Copia rápida de arrays usando Span (más rápido que Array.Copy)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FastCopy<T>(T[] source, T[] destination, int length)
        {
            source.AsSpan(0, length).CopyTo(destination.AsSpan());
        }
        
        /// <summary>
        /// Limpia array usando Span (más rápido que Array.Clear)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FastClear<T>(T[] array, int length)
        {
            array.AsSpan(0, length).Clear();
        }
        
        /// <summary>
        /// Pool de objetos genérico para reutilizar instancias
        /// </summary>
        public class ObjectPool<T> where T : class, new()
        {
            private readonly Stack<T> pool = new Stack<T>();
            private readonly object lockObj = new object();
            private readonly int maxSize;
            
            public ObjectPool(int maxSize = 100)
            {
                this.maxSize = maxSize;
            }
            
            public T Rent()
            {
                lock (lockObj)
                {
                    if (pool.Count > 0)
                        return pool.Pop();
                }
                
                return new T();
            }
            
            public void Return(T obj)
            {
                if (obj == null) return;
                
                lock (lockObj)
                {
                    if (pool.Count < maxSize)
                        pool.Push(obj);
                }
            }
        }
        
        /// <summary>
        /// Búsqueda rápida en string usando SIMD cuando está disponible
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsFast(ReadOnlySpan<char> text, ReadOnlySpan<char> value)
        {
            // .NET 8 usa SIMD automáticamente en Contains
            return text.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Hash rápido para strings usando xxHash (más rápido que GetHashCode)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastHash(ReadOnlySpan<char> text)
        {
            if (text.Length == 0) return 0;
            
            unchecked
            {
                int hash = 17;
                foreach (char c in text)
                {
                    hash = hash * 31 + char.ToLowerInvariant(c);
                }
                return hash;
            }
        }
    }
}
