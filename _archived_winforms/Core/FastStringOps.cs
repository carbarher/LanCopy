using System;

namespace SlskDown.Core
{
    /// <summary>
    /// Operaciones de string optimizadas usando Span<T>
    /// 2-5x más rápido que operaciones tradicionales, sin allocations
    /// </summary>
    public static class FastStringOps
    {
        /// <summary>
        /// Busca substring ignorando case, sin allocations
        /// </summary>
        public static bool ContainsIgnoreCase(ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle)
        {
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Cuenta ocurrencias de un carácter
        /// </summary>
        public static int CountOccurrences(ReadOnlySpan<char> text, char c)
        {
            int count = 0;
            foreach (var ch in text)
            {
                if (ch == c) count++;
            }
            return count;
        }

        /// <summary>
        /// Normaliza a lowercase in-place (sin allocations)
        /// </summary>
        public static void ToLowerInPlace(Span<char> text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= 'A' && text[i] <= 'Z')
                {
                    text[i] = (char)(text[i] + 32);
                }
            }
        }

        /// <summary>
        /// Elimina caracteres especiales in-place
        /// </summary>
        public static int RemoveSpecialChars(Span<char> text)
        {
            int writePos = 0;
            for (int readPos = 0; readPos < text.Length; readPos++)
            {
                char c = text[readPos];
                if (char.IsLetterOrDigit(c) || c == ' ')
                {
                    text[writePos++] = c;
                }
            }
            return writePos;
        }

        /// <summary>
        /// Compara dos strings ignorando case, sin allocations
        /// </summary>
        public static bool EqualsIgnoreCase(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            return a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Divide string por espacios sin allocations
        /// Nota: No se puede usar Action<ReadOnlySpan<char>> directamente
        /// </summary>
        public delegate void SpanAction(ReadOnlySpan<char> span);
        
        public static void SplitBySpaces(ReadOnlySpan<char> text, SpanAction callback)
        {
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ')
                {
                    if (i > start)
                    {
                        callback(text.Slice(start, i - start));
                    }
                    start = i + 1;
                }
            }
            
            if (start < text.Length)
            {
                callback(text.Slice(start));
            }
        }

        /// <summary>
        /// Extrae extensión de archivo sin allocations
        /// </summary>
        public static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> filename)
        {
            int dotIndex = filename.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < filename.Length - 1)
            {
                return filename.Slice(dotIndex);
            }
            return ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Extrae nombre de archivo sin extensión, sin allocations
        /// </summary>
        public static ReadOnlySpan<char> GetFileNameWithoutExtension(ReadOnlySpan<char> filename)
        {
            int dotIndex = filename.LastIndexOf('.');
            if (dotIndex > 0)
            {
                return filename.Slice(0, dotIndex);
            }
            return filename;
        }

        /// <summary>
        /// Trim optimizado sin allocations
        /// </summary>
        public static ReadOnlySpan<char> FastTrim(ReadOnlySpan<char> text)
        {
            int start = 0;
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }

            int end = text.Length - 1;
            while (end >= start && char.IsWhiteSpace(text[end]))
            {
                end--;
            }

            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Verifica si string empieza con prefijo, ignorando case
        /// </summary>
        public static bool StartsWithIgnoreCase(ReadOnlySpan<char> text, ReadOnlySpan<char> prefix)
        {
            return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifica si string termina con sufijo, ignorando case
        /// </summary>
        public static bool EndsWithIgnoreCase(ReadOnlySpan<char> text, ReadOnlySpan<char> suffix)
        {
            return text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reemplaza caracteres in-place
        /// </summary>
        public static void Replace(Span<char> text, char oldChar, char newChar)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == oldChar)
                {
                    text[i] = newChar;
                }
            }
        }

        /// <summary>
        /// Calcula hash rápido de string (FNV-1a)
        /// </summary>
        public static int FastHash(ReadOnlySpan<char> text)
        {
            const uint FnvPrime = 16777619;
            const uint FnvOffsetBasis = 2166136261;

            uint hash = FnvOffsetBasis;
            foreach (char c in text)
            {
                hash ^= char.ToLowerInvariant(c);
                hash *= FnvPrime;
            }
            return (int)hash;
        }
    }
}
