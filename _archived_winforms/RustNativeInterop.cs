using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SlskDown
{
    /// <summary>
    /// Interop con módulo nativo Rust para operaciones de alto rendimiento
    /// </summary>
    public static class RustNativeInterop
    {
        private const string DLL_NAME = "slskdown_native.dll";
        private static bool _isAvailable = false;

        static RustNativeInterop()
        {
            // Verificar si la DLL está disponible
            try
            {
                var bytes1 = Encoding.UTF8.GetBytes("a");
                var bytes2 = Encoding.UTF8.GetBytes("b");

                var handle1 = GCHandle.Alloc(bytes1, GCHandleType.Pinned);
                var handle2 = GCHandle.Alloc(bytes2, GCHandleType.Pinned);
                try
                {
                    var test = levenshtein_distance_native(handle1.AddrOfPinnedObject(), bytes1.Length, handle2.AddrOfPinnedObject(), bytes2.Length);
                    _isAvailable = test >= 0;
                }
                finally
                {
                    handle1.Free();
                    handle2.Free();
                }
            }
            catch
            {
                _isAvailable = false;
            }
        }

        public static bool IsAvailable => _isAvailable;

        #region P/Invoke Declarations

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "levenshtein_distance")]
        private static extern int levenshtein_distance_native(IntPtr s1, int s1_len, IntPtr s2, int s2_len);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "calculate_similarity")]
        private static extern double calculate_similarity_native(IntPtr s1, int s1_len, IntPtr s2, int s2_len);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "normalize_string")]
        private static extern int normalize_string_native(IntPtr input, int input_len, IntPtr output, int output_capacity);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "contains_pattern")]
        private static extern int contains_pattern_native(IntPtr text, int text_len, IntPtr pattern, int pattern_len);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FileCallback(IntPtr path);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "scan_directory_parallel")]
        private static extern int scan_directory_parallel_native(IntPtr path, FileCallback callback);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "find_similar_files")]
        private static extern int find_similar_files_native(
            IntPtr query, int query_len,
            IntPtr[] files, int files_count,
            double threshold,
            int[] results, int results_capacity);

        #endregion

        #region Safe Wrappers

        /// <summary>
        /// Calcular distancia de Levenshtein entre dos strings
        /// </summary>
        public static int LevenshteinDistance(string s1, string s2)
        {
            if (!_isAvailable)
                return LevenshteinDistanceFallback(s1, s2);

            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return Math.Max(s1?.Length ?? 0, s2?.Length ?? 0);

            var bytes1 = Encoding.UTF8.GetBytes(s1);
            var bytes2 = Encoding.UTF8.GetBytes(s2);

            var handle1 = GCHandle.Alloc(bytes1, GCHandleType.Pinned);
            var handle2 = GCHandle.Alloc(bytes2, GCHandleType.Pinned);

            try
            {
                var ptr1 = handle1.AddrOfPinnedObject();
                var ptr2 = handle2.AddrOfPinnedObject();

                return levenshtein_distance_native(ptr1, bytes1.Length, ptr2, bytes2.Length);
            }
            finally
            {
                handle1.Free();
                handle2.Free();
            }
        }

        /// <summary>
        /// Calcular similitud normalizada (0.0 a 1.0)
        /// </summary>
        public static double CalculateSimilarity(string s1, string s2)
        {
            if (!_isAvailable)
                return CalculateSimilarityFallback(s1, s2);

            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return 0.0;

            var bytes1 = Encoding.UTF8.GetBytes(s1);
            var bytes2 = Encoding.UTF8.GetBytes(s2);

            var handle1 = GCHandle.Alloc(bytes1, GCHandleType.Pinned);
            var handle2 = GCHandle.Alloc(bytes2, GCHandleType.Pinned);

            try
            {
                var ptr1 = handle1.AddrOfPinnedObject();
                var ptr2 = handle2.AddrOfPinnedObject();

                return calculate_similarity_native(ptr1, bytes1.Length, ptr2, bytes2.Length);
            }
            finally
            {
                handle1.Free();
                handle2.Free();
            }
        }

        /// <summary>
        /// Normalizar string (lowercase, sin caracteres especiales)
        /// </summary>
        public static string NormalizeString(string input)
        {
            if (!_isAvailable)
                return NormalizeStringFallback(input);

            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var inputBytes = Encoding.UTF8.GetBytes(input);
            var outputBytes = new byte[inputBytes.Length * 2]; // Buffer extra

            var handleInput = GCHandle.Alloc(inputBytes, GCHandleType.Pinned);
            var handleOutput = GCHandle.Alloc(outputBytes, GCHandleType.Pinned);

            try
            {
                var ptrInput = handleInput.AddrOfPinnedObject();
                var ptrOutput = handleOutput.AddrOfPinnedObject();

                var length = normalize_string_native(ptrInput, inputBytes.Length, ptrOutput, outputBytes.Length);

                if (length > 0)
                {
                    return Encoding.UTF8.GetString(outputBytes, 0, length);
                }

                return string.Empty;
            }
            finally
            {
                handleInput.Free();
                handleOutput.Free();
            }
        }

        /// <summary>
        /// Verificar si texto contiene patrón (case-insensitive)
        /// </summary>
        public static bool ContainsPattern(string text, string pattern)
        {
            if (!_isAvailable)
                return text?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false;

            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return false;

            var textBytes = Encoding.UTF8.GetBytes(text);
            var patternBytes = Encoding.UTF8.GetBytes(pattern);

            var handleText = GCHandle.Alloc(textBytes, GCHandleType.Pinned);
            var handlePattern = GCHandle.Alloc(patternBytes, GCHandleType.Pinned);

            try
            {
                var ptrText = handleText.AddrOfPinnedObject();
                var ptrPattern = handlePattern.AddrOfPinnedObject();

                return contains_pattern_native(ptrText, textBytes.Length, ptrPattern, patternBytes.Length) != 0;
            }
            finally
            {
                handleText.Free();
                handlePattern.Free();
            }
        }

        /// <summary>
        /// Escanear directorio en paralelo
        /// </summary>
        public static void ScanDirectoryParallel(string path, Action<string> onFileFound)
        {
            if (!_isAvailable)
            {
                ScanDirectoryFallback(path, onFileFound);
                return;
            }

            if (string.IsNullOrEmpty(path) || onFileFound == null)
                return;

            FileCallback callback = (pathPtr) =>
            {
                try
                {
                    var filePath = Marshal.PtrToStringAnsi(pathPtr);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        onFileFound(filePath);
                    }
                }
                catch { }
            };

            var pathBytes = Encoding.UTF8.GetBytes(path + "\0");
            var handle = GCHandle.Alloc(pathBytes, GCHandleType.Pinned);

            try
            {
                var ptr = handle.AddrOfPinnedObject();
                scan_directory_parallel_native(ptr, callback);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Encontrar archivos similares
        /// </summary>
        public static int[] FindSimilarFiles(string query, string[] files, double threshold = 0.7)
        {
            if (!_isAvailable)
                return FindSimilarFilesFallback(query, files, threshold);

            if (string.IsNullOrEmpty(query) || files == null || files.Length == 0)
                return Array.Empty<int>();

            var queryBytes = Encoding.UTF8.GetBytes(query);
            var filePointers = new IntPtr[files.Length];
            var handles = new GCHandle[files.Length];

            try
            {
                // Preparar punteros de archivos
                for (int i = 0; i < files.Length; i++)
                {
                    var fileBytes = Encoding.UTF8.GetBytes(files[i] + "\0");
                    handles[i] = GCHandle.Alloc(fileBytes, GCHandleType.Pinned);
                    filePointers[i] = handles[i].AddrOfPinnedObject();
                }

                var results = new int[files.Length];
                var queryHandle = GCHandle.Alloc(queryBytes, GCHandleType.Pinned);

                try
                {
                    var queryPtr = queryHandle.AddrOfPinnedObject();
                    var count = find_similar_files_native(
                        queryPtr, queryBytes.Length,
                        filePointers, files.Length,
                        threshold,
                        results, results.Length);

                    if (count > 0)
                    {
                        var result = new int[count];
                        Array.Copy(results, result, count);
                        return result;
                    }

                    return Array.Empty<int>();
                }
                finally
                {
                    queryHandle.Free();
                }
            }
            finally
            {
                foreach (var handle in handles)
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
            }
        }

        #endregion

        #region Fallback Implementations (C# puro)

        private static int LevenshteinDistanceFallback(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[s1.Length, s2.Length];
        }

        private static double CalculateSimilarityFallback(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return 0.0;

            int maxLen = Math.Max(s1.Length, s2.Length);
            if (maxLen == 0) return 1.0;

            int distance = LevenshteinDistanceFallback(s1, s2);
            return 1.0 - ((double)distance / maxLen);
        }

        private static string NormalizeStringFallback(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c) || c == ' ')
                    sb.Append(char.ToLowerInvariant(c));
                else
                    sb.Append(' ');
            }

            return sb.ToString();
        }

        private static void ScanDirectoryFallback(string path, Action<string> onFileFound)
        {
            try
            {
                foreach (var file in System.IO.Directory.EnumerateFiles(path, "*.*", System.IO.SearchOption.AllDirectories))
                {
                    onFileFound(file);
                }
            }
            catch { }
        }

        private static int[] FindSimilarFilesFallback(string query, string[] files, double threshold)
        {
            var results = new System.Collections.Generic.List<int>();

            for (int i = 0; i < files.Length; i++)
            {
                var similarity = CalculateSimilarityFallback(query, files[i]);
                if (similarity >= threshold)
                {
                    results.Add(i);
                }
            }

            return results.ToArray();
        }

        #endregion
    }
}
