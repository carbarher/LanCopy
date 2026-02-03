using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SlskDown
{
    /// <summary>
    /// Clase con optimizaciones y utilidades de rendimiento
    /// </summary>
    public static class Optimizations
    {
        // Regex compilados para mejor rendimiento
        private static readonly Regex SpanishRegex = new Regex(
            @"\b(espaÃ±ol|castellano|spanish|spain|espaÃ±a|es|spa|cast)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex ComicRegex = new Regex(
            @"\b(comic|manga|cbr|cbz|tomo|vol|chapter|cap[iÃ­]tulo)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Verifica si el contenido es en espaÃ±ol usando regex compilado
        /// </summary>
        public static bool IsSpanishContent(string text)
        {
            return SpanishRegex.IsMatch(text);
        }

        /// <summary>
        /// Verifica si es un comic usando regex compilado
        /// </summary>
        public static bool IsComic(string text)
        {
            return ComicRegex.IsMatch(text);
        }

        /// <summary>
        /// StringBuilder pool para reducir allocaciones
        /// </summary>
        private static readonly Stack<StringBuilder> StringBuilderPool = new Stack<StringBuilder>();
        private static readonly object PoolLock = new object();

        public static StringBuilder GetStringBuilder()
        {
            lock (PoolLock)
            {
                if (StringBuilderPool.Count > 0)
                {
                    var sb = StringBuilderPool.Pop();
                    sb.Clear();
                    return sb;
                }
            }
            return new StringBuilder(1024); // Capacidad inicial de 1KB
        }

        public static void ReturnStringBuilder(StringBuilder sb)
        {
            if (sb.Capacity > 8192) return; // No guardar builders muy grandes
            
            lock (PoolLock)
            {
                if (StringBuilderPool.Count < 10) // MÃ¡ximo 10 en pool
                {
                    StringBuilderPool.Push(sb);
                }
            }
        }

        /// <summary>
        /// Ãndice para bÃºsqueda rÃ¡pida de archivos descargados
        /// </summary>
        public class DownloadIndex
        {
            private readonly Dictionary<string, HashSet<long>> _index = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
            private readonly object _lock = new object();

            public void Add(string filename, long size)
            {
                lock (_lock)
                {
                    if (!_index.ContainsKey(filename))
                    {
                        _index[filename] = new HashSet<long>();
                    }
                    _index[filename].Add(size);
                }
            }

            public bool Contains(string filename, long size)
            {
                lock (_lock)
                {
                    return _index.TryGetValue(filename, out var sizes) && sizes.Contains(size);
                }
            }

            public void Clear()
            {
                lock (_lock)
                {
                    _index.Clear();
                }
            }

            public int Count
            {
                get
                {
                    lock (_lock)
                    {
                        return _index.Sum(kvp => kvp.Value.Count);
                    }
                }
            }
        }

        /// <summary>
        /// Buffer de escritura para reducir I/O
        /// </summary>
        public class WriteBuffer
        {
            private readonly List<string> _buffer = new List<string>();
            private readonly int _maxSize;
            private readonly TimeSpan _maxAge;
            private DateTime _lastWrite = DateTime.Now;
            private readonly object _lock = new object();

            public WriteBuffer(int maxSize = 10, int maxAgeSeconds = 30)
            {
                _maxSize = maxSize;
                _maxAge = TimeSpan.FromSeconds(maxAgeSeconds);
            }

            public void Add(string line)
            {
                lock (_lock)
                {
                    _buffer.Add(line);
                }
            }

            public bool ShouldFlush()
            {
                lock (_lock)
                {
                    return _buffer.Count >= _maxSize || 
                           (DateTime.Now - _lastWrite) >= _maxAge;
                }
            }

            public List<string> Flush()
            {
                lock (_lock)
                {
                    var lines = new List<string>(_buffer);
                    _buffer.Clear();
                    _lastWrite = DateTime.Now;
                    return lines;
                }
            }

            public int Count
            {
                get
                {
                    lock (_lock)
                    {
                        return _buffer.Count;
                    }
                }
            }
        }

        /// <summary>
        /// Parsea tamaÃ±o de texto a bytes (optimizado)
        /// </summary>
        public static long ParseSize(string sizeText)
        {
            try
            {
                var parts = sizeText.Split(' ');
                if (parts.Length == 2)
                {
                    if (double.TryParse(parts[0], out double value))
                    {
                        string unit = parts[1].ToUpperInvariant();
                        return unit switch
                        {
                            "GB" => (long)(value * 1024 * 1024 * 1024),
                            "MB" => (long)(value * 1024 * 1024),
                            "KB" => (long)(value * 1024),
                            "B" => (long)value,
                            _ => 0
                        };
                    }
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Formatea tamaÃ±o de bytes a texto (optimizado)
        /// </summary>
        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }
}

