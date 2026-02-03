using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Helpers para operaciones con Span<T> de alto rendimiento
    /// Evita allocations innecesarias
    /// </summary>
    public static class SpanHelpers
    {
        /// <summary>
        /// Divide string usando Span (sin allocations)
        /// </summary>
        public static void SplitString(ReadOnlySpan<char> input, char separator, Action<ReadOnlySpan<char>> action)
        {
            int start = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == separator)
                {
                    if (i > start)
                    {
                        action(input.Slice(start, i - start));
                    }
                    start = i + 1;
                }
            }
            
            if (start < input.Length)
            {
                action(input.Slice(start));
            }
        }
        
        /// <summary>
        /// Trim sin allocations
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> TrimFast(this ReadOnlySpan<char> span)
        {
            return span.Trim();
        }
        
        /// <summary>
        /// StartsWith case-insensitive sin allocations
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool StartsWithIgnoreCase(this ReadOnlySpan<char> span, ReadOnlySpan<char> value)
        {
            return span.StartsWith(value, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// EndsWith case-insensitive sin allocations
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EndsWithIgnoreCase(this ReadOnlySpan<char> span, ReadOnlySpan<char> value)
        {
            return span.EndsWith(value, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Contains case-insensitive sin allocations
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsIgnoreCase(this ReadOnlySpan<char> span, ReadOnlySpan<char> value)
        {
            return span.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Copia rápida de arrays usando Span
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FastCopy<T>(T[] source, T[] destination, int length)
        {
            source.AsSpan(0, length).CopyTo(destination);
        }
        
        /// <summary>
        /// Limpia array de forma segura
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SecureClear<T>(T[] array)
        {
            array.AsSpan().Clear();
        }
        
        /// <summary>
        /// Convierte bytes a hex string sin allocations intermedias
        /// </summary>
        public static string ToHexString(ReadOnlySpan<byte> bytes)
        {
            return Convert.ToHexString(bytes);
        }
        
        /// <summary>
        /// Parse int desde span sin allocations
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseInt(ReadOnlySpan<char> span, out int result)
        {
            return int.TryParse(span, out result);
        }
        
        /// <summary>
        /// Parse long desde span sin allocations
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseLong(ReadOnlySpan<char> span, out long result)
        {
            return long.TryParse(span, out result);
        }
        
        /// <summary>
        /// Compara spans de bytes de forma rápida
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SequenceEqual<T>(ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : IEquatable<T>
        {
            return first.SequenceEqual(second);
        }
        
        /// <summary>
        /// Renta buffer temporal del pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] RentBuffer(int minimumLength)
        {
            return ArrayPool<byte>.Shared.Rent(minimumLength);
        }
        
        /// <summary>
        /// Devuelve buffer al pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnBuffer(byte[] buffer, bool clearArray = false)
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray);
        }
    }
}
