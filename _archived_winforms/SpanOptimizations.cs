using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace SlskDown
{
    /// <summary>
    /// Optimizaciones usando Span<T> y Memory<T> para reducir allocations
    /// </summary>
    public static class SpanOptimizations
    {
        /// <summary>
        /// Lee archivo usando Span<T> para evitar allocations
        /// </summary>
        public static bool TryReadFileHeader(string filePath, Span<byte> header)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var bytesRead = stream.Read(header);
                return bytesRead == header.Length;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si un archivo es ZIP usando Span<T>
        /// </summary>
        public static bool IsZipFile(string filePath)
        {
            Span<byte> header = stackalloc byte[4];
            if (!TryReadFileHeader(filePath, header))
                return false;

            return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
        }

        /// <summary>
        /// Verifica si un archivo es PDF usando Span<T>
        /// </summary>
        public static bool IsPdfFile(string filePath)
        {
            Span<byte> header = stackalloc byte[5];
            if (!TryReadFileHeader(filePath, header))
                return false;

            return header[0] == (byte)'%' && header[1] == (byte)'P' && 
                   header[2] == (byte)'D' && header[3] == (byte)'F' && header[4] == (byte)'-';
        }

        /// <summary>
        /// Verifica si un archivo es EPUB usando Span<T>
        /// </summary>
        public static bool IsEpubFile(string filePath)
        {
            return IsZipFile(filePath); // EPUB es un ZIP
        }

        /// <summary>
        /// Compara dos strings ignorando case sin allocations
        /// </summary>
        public static bool EqualsIgnoreCase(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            return a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifica si un string contiene otro ignorando case sin allocations
        /// </summary>
        public static bool ContainsIgnoreCase(ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle)
        {
            if (needle.Length > haystack.Length)
                return false;

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                if (haystack.Slice(i, needle.Length).Equals(needle, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Extrae extensión de archivo sin allocations
        /// </summary>
        public static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> fileName)
        {
            var lastDot = fileName.LastIndexOf('.');
            if (lastDot < 0 || lastDot == fileName.Length - 1)
                return ReadOnlySpan<char>.Empty;

            return fileName.Slice(lastDot);
        }

        /// <summary>
        /// Verifica si una extensión es válida para documentos
        /// </summary>
        public static bool IsDocumentExtension(ReadOnlySpan<char> extension)
        {
            if (extension.IsEmpty)
                return false;

            // Normalizar a lowercase
            Span<char> lower = stackalloc char[extension.Length];
            extension.ToLowerInvariant(lower);

            return lower.SequenceEqual(".epub") ||
                   lower.SequenceEqual(".mobi") ||
                   lower.SequenceEqual(".azw3") ||
                   lower.SequenceEqual(".pdf") ||
                   lower.SequenceEqual(".fb2") ||
                   lower.SequenceEqual(".doc") ||
                   lower.SequenceEqual(".docx") ||
                   lower.SequenceEqual(".txt") ||
                   lower.SequenceEqual(".rtf");
        }

        /// <summary>
        /// Copia datos de forma eficiente usando Memory<T>
        /// </summary>
        public static void CopyMemory(ReadOnlyMemory<byte> source, Memory<byte> destination)
        {
            source.Span.CopyTo(destination.Span);
        }

        /// <summary>
        /// Lee archivo en chunks usando ArrayPool para evitar allocations grandes
        /// </summary>
        public static long ProcessFileInChunks(string filePath, Action<ReadOnlySpan<byte>> processChunk, int chunkSize = 8192)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
            try
            {
                using var stream = File.OpenRead(filePath);
                long totalProcessed = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, chunkSize)) > 0)
                {
                    processChunk(buffer.AsSpan(0, bytesRead));
                    totalProcessed += bytesRead;
                }

                return totalProcessed;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Busca patrón en archivo usando Span<T> sin cargar todo en memoria
        /// </summary>
        public static bool FileContainsPattern(string filePath, ReadOnlySpan<byte> pattern, int maxBytesToSearch = 1024 * 1024)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                using var stream = File.OpenRead(filePath);
                long totalRead = 0;
                int bytesRead;
                Span<byte> overlap = stackalloc byte[pattern.Length];
                int overlapSize = 0;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0 && totalRead < maxBytesToSearch)
                {
                    var searchSpan = buffer.AsSpan(0, bytesRead);
                    
                    // Buscar en el buffer actual
                    if (searchSpan.IndexOf(pattern) >= 0)
                        return true;

                    // Guardar overlap para siguiente iteración
                    if (bytesRead >= pattern.Length)
                    {
                        searchSpan.Slice(bytesRead - pattern.Length + 1).CopyTo(overlap);
                        overlapSize = pattern.Length - 1;
                    }

                    totalRead += bytesRead;
                }

                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Calcula hash simple de string usando Span<T>
        /// </summary>
        public static int GetFastHashCode(ReadOnlySpan<char> text)
        {
            if (text.IsEmpty)
                return 0;

            int hash = 17;
            foreach (var c in text)
            {
                hash = hash * 31 + char.ToLowerInvariant(c);
            }

            return hash;
        }

        /// <summary>
        /// Divide string por separador sin allocations (retorna índices)
        /// </summary>
        public static void SplitString(ReadOnlySpan<char> text, char separator, Span<Range> ranges, out int count)
        {
            count = 0;
            int start = 0;

            for (int i = 0; i < text.Length && count < ranges.Length; i++)
            {
                if (text[i] == separator)
                {
                    ranges[count++] = new Range(start, i);
                    start = i + 1;
                }
            }

            // Agregar último segmento
            if (start < text.Length && count < ranges.Length)
            {
                ranges[count++] = new Range(start, text.Length);
            }
        }

        /// <summary>
        /// Trim de string sin allocations
        /// </summary>
        public static ReadOnlySpan<char> TrimSpan(ReadOnlySpan<char> text)
        {
            return text.Trim();
        }

        /// <summary>
        /// Reemplaza caracteres en string usando Span<T>
        /// </summary>
        public static void ReplaceChar(Span<char> text, char oldChar, char newChar)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == oldChar)
                    text[i] = newChar;
            }
        }

        /// <summary>
        /// Convierte bytes a hex string usando Span<T>
        /// </summary>
        public static string ToHexString(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
                return string.Empty;

            Span<char> chars = stackalloc char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i].TryFormat(chars.Slice(i * 2), out _, "x2");
            }

            return new string(chars);
        }

        /// <summary>
        /// Parsea número de Span<char> sin allocations
        /// </summary>
        public static bool TryParseInt(ReadOnlySpan<char> text, out int value)
        {
            return int.TryParse(text, out value);
        }

        /// <summary>
        /// Formatea tamaño de archivo usando Span<T>
        /// </summary>
        public static bool TryFormatFileSize(long bytes, Span<char> destination, out int charsWritten)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
            {
                var gb = (double)bytes / GB;
                return gb.TryFormat(destination, out charsWritten, "F2") && 
                       " GB".AsSpan().TryCopyTo(destination.Slice(charsWritten));
            }
            else if (bytes >= MB)
            {
                var mb = (double)bytes / MB;
                return mb.TryFormat(destination, out charsWritten, "F2") && 
                       " MB".AsSpan().TryCopyTo(destination.Slice(charsWritten));
            }
            else if (bytes >= KB)
            {
                var kb = (double)bytes / KB;
                return kb.TryFormat(destination, out charsWritten, "F2") && 
                       " KB".AsSpan().TryCopyTo(destination.Slice(charsWritten));
            }
            else
            {
                return bytes.TryFormat(destination, out charsWritten) && 
                       " bytes".AsSpan().TryCopyTo(destination.Slice(charsWritten));
            }
        }

        /// <summary>
        /// Copia string a buffer con encoding UTF8 sin allocations intermedias
        /// </summary>
        public static bool TryEncodeUtf8(ReadOnlySpan<char> text, Span<byte> destination, out int bytesWritten)
        {
            return Encoding.UTF8.TryGetBytes(text, destination, out bytesWritten);
        }

        /// <summary>
        /// Decodifica UTF8 a string desde Span<byte>
        /// </summary>
        public static string DecodeUtf8(ReadOnlySpan<byte> bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
