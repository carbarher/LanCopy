using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace SlskDown.Core
{
    public static class StringOptimizations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> ExtractAuthorSpan(ReadOnlySpan<char> filename)
        {
            ReadOnlySpan<char> patterns = stackalloc char[] { '-', '_' };
            
            foreach (var separator in patterns)
            {
                int index = filename.IndexOf(separator);
                if (index > 0)
                {
                    var candidate = filename.Slice(0, index).Trim();
                    if (candidate.Length > 2 && candidate.Length < 50)
                    {
                        return candidate;
                    }
                }
            }

            int spaceCount = 0;
            int lastSpace = -1;
            
            for (int i = 0; i < filename.Length && spaceCount < 3; i++)
            {
                if (filename[i] == ' ')
                {
                    spaceCount++;
                    lastSpace = i;
                }
            }

            if (lastSpace > 0)
            {
                return filename.Slice(0, lastSpace).Trim();
            }

            return ReadOnlySpan<char>.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> GetFileNameWithoutExtensionSpan(ReadOnlySpan<char> path)
        {
            int lastSlash = path.LastIndexOfAny('\\', '/');
            var fileName = lastSlash >= 0 ? path.Slice(lastSlash + 1) : path;
            
            int lastDot = fileName.LastIndexOf('.');
            return lastDot > 0 ? fileName.Slice(0, lastDot) : fileName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> GetExtensionSpan(ReadOnlySpan<char> path)
        {
            int lastDot = path.LastIndexOf('.');
            return lastDot >= 0 && lastDot < path.Length - 1 
                ? path.Slice(lastDot + 1) 
                : ReadOnlySpan<char>.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsSpanish(ReadOnlySpan<char> text)
        {
            ReadOnlySpan<char> spanishChars = stackalloc char[] 
            { 
                'á', 'é', 'í', 'ó', 'ú', 'ñ', 'ü',
                'Á', 'É', 'Í', 'Ó', 'Ú', 'Ñ', 'Ü'
            };

            foreach (var c in text)
            {
                if (spanishChars.Contains(c))
                    return true;
            }

            return false;
        }

        public static string ExtractAuthor(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            var span = filename.AsSpan();
            var authorSpan = ExtractAuthorSpan(span);
            
            return authorSpan.IsEmpty ? null : authorSpan.ToString();
        }

        public static bool TryFormatFileSize(long bytes, Span<char> destination, out int charsWritten)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
            {
                double gb = bytes / (double)GB;
                return gb.TryFormat(destination, out charsWritten, "F2") 
                    && " GB".AsSpan().TryCopyTo(destination.Slice(charsWritten));
            }
            else if (bytes >= MB)
            {
                double mb = bytes / (double)MB;
                return mb.TryFormat(destination, out charsWritten, "F2") 
                    && " MB".AsSpan().TryCopyTo(destination.Slice(charsWritten));
            }
            else if (bytes >= KB)
            {
                double kb = bytes / (double)KB;
                return kb.TryFormat(destination, out charsWritten, "F2") 
                    && " KB".AsSpan().TryCopyTo(destination.Slice(charsWritten));
            }
            else
            {
                return bytes.TryFormat(destination, out charsWritten) 
                    && " B".AsSpan().TryCopyTo(destination.Slice(charsWritten));
            }
        }

        public static string FormatFileSize(long bytes)
        {
            Span<char> buffer = stackalloc char[32];
            if (TryFormatFileSize(bytes, buffer, out int written))
            {
                return new string(buffer.Slice(0, written));
            }
            return $"{bytes} B";
        }
    }
}
