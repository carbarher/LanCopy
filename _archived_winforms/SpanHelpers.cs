using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SlskDown
{
    /// <summary>
    /// MEJORA #43: Utilidades con Span<T> y ArrayPool para reducir allocations
    /// </summary>
    public static class SpanHelpers
    {
        private static readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;
        private static readonly ArrayPool<char> charPool = ArrayPool<char>.Shared;

        /// <summary>
        /// Verifica si un filename termina con una extensión (case-insensitive, sin allocations)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EndsWithExtension(ReadOnlySpan<char> filename, ReadOnlySpan<char> extension)
        {
            return filename.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifica si un filename contiene un substring (case-insensitive, sin allocations)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsIgnoreCase(ReadOnlySpan<char> text, ReadOnlySpan<char> value)
        {
            return text.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extrae el nombre de archivo sin extensión (sin allocations)
        /// </summary>
        public static ReadOnlySpan<char> GetFileNameWithoutExtension(ReadOnlySpan<char> filename)
        {
            int lastDot = filename.LastIndexOf('.');
            if (lastDot > 0)
            {
                return filename.Slice(0, lastDot);
            }
            return filename;
        }

        /// <summary>
        /// Extrae la extensión de un archivo (sin allocations)
        /// </summary>
        public static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> filename)
        {
            int lastDot = filename.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < filename.Length - 1)
            {
                return filename.Slice(lastDot);
            }
            return ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Parsea autor y título desde un filename (sin allocations)
        /// Formato esperado: "Titulo - Autor.ext"
        /// </summary>
        public static bool TryParseAuthorTitle(ReadOnlySpan<char> filename, 
            out ReadOnlySpan<char> title, out ReadOnlySpan<char> author)
        {
            title = ReadOnlySpan<char>.Empty;
            author = ReadOnlySpan<char>.Empty;

            // Buscar " - " como separador
            int separatorIndex = filename.IndexOf(" - ".AsSpan());
            if (separatorIndex <= 0)
            {
                return false;
            }

            // Extraer título (antes del separador)
            title = filename.Slice(0, separatorIndex).Trim();

            // Extraer autor (después del separador, sin extensión)
            var afterSeparator = filename.Slice(separatorIndex + 3); // +3 para saltar " - "
            int lastDot = afterSeparator.LastIndexOf('.');
            if (lastDot > 0)
            {
                author = afterSeparator.Slice(0, lastDot).Trim();
            }
            else
            {
                author = afterSeparator.Trim();
            }

            return title.Length > 0 && author.Length > 0;
        }

        /// <summary>
        /// Alquila un buffer de bytes del pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] RentByteBuffer(int minimumSize)
        {
            return bytePool.Rent(minimumSize);
        }

        /// <summary>
        /// Devuelve un buffer de bytes al pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnByteBuffer(byte[] buffer, bool clearArray = false)
        {
            bytePool.Return(buffer, clearArray);
        }

        /// <summary>
        /// Alquila un buffer de chars del pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char[] RentCharBuffer(int minimumSize)
        {
            return charPool.Rent(minimumSize);
        }

        /// <summary>
        /// Devuelve un buffer de chars al pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnCharBuffer(char[] buffer, bool clearArray = false)
        {
            charPool.Return(buffer, clearArray);
        }

        /// <summary>
        /// Wrapper para usar ArrayPool con using statement
        /// </summary>
        public readonly struct PooledByteBuffer : IDisposable
        {
            private readonly byte[] buffer;
            private readonly int length;

            public PooledByteBuffer(int minimumSize)
            {
                buffer = bytePool.Rent(minimumSize);
                length = minimumSize;
            }

            public Span<byte> Span => buffer.AsSpan(0, length);
            public Memory<byte> Memory => buffer.AsMemory(0, length);

            public void Dispose()
            {
                if (buffer != null)
                {
                    bytePool.Return(buffer);
                }
            }
        }

        /// <summary>
        /// Wrapper para usar ArrayPool de chars con using statement
        /// </summary>
        public readonly struct PooledCharBuffer : IDisposable
        {
            private readonly char[] buffer;
            private readonly int length;

            public PooledCharBuffer(int minimumSize)
            {
                buffer = charPool.Rent(minimumSize);
                length = minimumSize;
            }

            public Span<char> Span => buffer.AsSpan(0, length);
            public Memory<char> Memory => buffer.AsMemory(0, length);

            public void Dispose()
            {
                if (buffer != null)
                {
                    charPool.Return(buffer);
                }
            }
        }
    }
}
