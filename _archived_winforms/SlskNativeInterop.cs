using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace SlskDown
{
    /// <summary>
    /// Interop con slsk_native.dll (Rust) para operaciones de alto rendimiento
    /// </summary>
    public static class SlskNativeInterop
    {
        private const string DLL_NAME = "slsk_native.dll";
        private static bool _isAvailable = false;
        private static bool _supportsSearchFilterSort = true;
        private static bool _supportsSearchPipelinePointers = true;
        private static bool _supportsSearchPipelineTable = true;
        private static bool _supportsDedupeKeysTable = true;
        private static bool _rustEnabled = true;
        private static IntPtr _dllHandle = IntPtr.Zero;
        private static uint _nativeVersion = 0;
        private static string? _loadedDllPath = null;
        private static string? _dllExportsHint = null;
        private static int _loadLibraryWin32Error = 0;
        private static string? _loadLibraryException = null;

        private const int NATIVE_CB_FAILURE_THRESHOLD = 5;
        private const int NATIVE_CB_OPEN_SECONDS = 60;
        private static int _nativeConsecutiveFailures = 0;
        private static DateTime _nativeCircuitOpenUntilUtc = DateTime.MinValue;
        private static int _nativeHalfOpenInFlight = 0;

        private const ulong CAP_FILTER_SEARCH_RESULTS = 1UL << 0;
        private const ulong CAP_SORT_BY_QUALITY = 1UL << 1;
        private const ulong CAP_PROCESS_SEARCH_RESULTS = 1UL << 2;
        private const ulong CAP_PROCESS_SEARCH_RESULTS_TABLE = 1UL << 3;
        private const ulong CAP_FILTER_SEARCH_RESULTS_TABLE = 1UL << 4;
        private const ulong CAP_SORT_BY_QUALITY_TABLE = 1UL << 5;
        private const ulong CAP_DEDUPLICATE_KEYS_TABLE = 1UL << 6;

        static SlskNativeInterop()
        {
            // Verificar si la DLL está disponible
            try
            {
                _rustEnabled = !string.Equals(Environment.GetEnvironmentVariable("SLSK_DISABLE_RUST"), "1", StringComparison.Ordinal);
                if (!_rustEnabled)
                {
                    _isAvailable = false;
                    return;
                }
                var baseDir = AppContext.BaseDirectory;
                var dllPath = Path.Combine(baseDir, DLL_NAME);
                _loadedDllPath = dllPath;
                _dllExportsHint = TryBuildDllExportsHint(dllPath);
                _dllHandle = LoadLibrary(dllPath);
                if (_dllHandle == IntPtr.Zero)
                {
                    var publishPath = Path.Combine(baseDir, "publish", DLL_NAME);
                    _loadedDllPath = publishPath;
                    _dllExportsHint = TryBuildDllExportsHint(publishPath);
                    _dllHandle = LoadLibrary(publishPath);
                }
                if (_dllHandle == IntPtr.Zero)
                {
                    _loadLibraryWin32Error = Marshal.GetLastWin32Error();
                    _isAvailable = false;
                    return;
                }

                _isAvailable = true;
                InitializeCapabilities();
            }
            catch (Exception ex)
            {
                _loadLibraryException = $"{ex.GetType().Name}: {ex.Message}";
                _isAvailable = false;
            }
        }

        public static int LoadLibraryWin32Error => _loadLibraryWin32Error;
        public static bool IsRustEnabled => _rustEnabled;

        private static bool IsCircuitHardOpen()
        {
            return DateTime.UtcNow < _nativeCircuitOpenUntilUtc;
        }

        private static bool IsCircuitTripped()
        {
            return _nativeConsecutiveFailures >= NATIVE_CB_FAILURE_THRESHOLD;
        }

        private static bool TryEnterHalfOpenTrial()
        {
            if (!IsCircuitTripped())
            {
                return true;
            }

            return Interlocked.CompareExchange(ref _nativeHalfOpenInFlight, 1, 0) == 0;
        }

        private static void ExitHalfOpenTrial()
        {
            try
            {
                Interlocked.Exchange(ref _nativeHalfOpenInFlight, 0);
            }
            catch
            {
            }
        }

        private static void RecordNativeSuccess()
        {
            _nativeConsecutiveFailures = 0;
            _nativeCircuitOpenUntilUtc = DateTime.MinValue;
            Interlocked.Exchange(ref _nativeHalfOpenInFlight, 0);
        }

        private static void RecordNativeFailure()
        {
            try
            {
                var failures = _nativeConsecutiveFailures + 1;
                _nativeConsecutiveFailures = failures;
                Interlocked.Exchange(ref _nativeHalfOpenInFlight, 0);
                if (failures >= NATIVE_CB_FAILURE_THRESHOLD)
                {
                    _nativeCircuitOpenUntilUtc = DateTime.UtcNow.AddSeconds(NATIVE_CB_OPEN_SECONDS);
                }
            }
            catch
            {
            }
        }
        public static string GetAvailabilitySummary()
        {
            if (!_rustEnabled)
            {
                return "rust disabled";
            }

            if (IsCircuitHardOpen())
            {
                var remaining = _nativeCircuitOpenUntilUtc - DateTime.UtcNow;
                var secs = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
                return $"circuit_open {secs}s failures={_nativeConsecutiveFailures}";
            }

            if (IsCircuitTripped())
            {
                var inflight = Volatile.Read(ref _nativeHalfOpenInFlight);
                if (inflight != 0)
                {
                    return $"half_open trial failures={_nativeConsecutiveFailures}";
                }

                return $"half_open ready failures={_nativeConsecutiveFailures}";
            }

            if (_dllHandle == IntPtr.Zero)
            {
                if (_loadLibraryWin32Error != 0)
                {
                    var pathPart = !string.IsNullOrEmpty(_loadedDllPath) ? $" path={_loadedDllPath}" : string.Empty;
                    return $"LoadLibrary error={_loadLibraryWin32Error}{pathPart}";
                }
                if (!string.IsNullOrEmpty(_loadLibraryException))
                {
                    var pathPart = !string.IsNullOrEmpty(_loadedDllPath) ? $" path={_loadedDllPath}" : string.Empty;
                    return $"LoadLibrary {_loadLibraryException}{pathPart}";
                }
                return "LoadLibrary failed";
            }

            return "ok";
        }

        public static string GetSearchPipelineSupportSummary()
        {
            if (!_isAvailable || !_rustEnabled || IsCircuitHardOpen() || IsCircuitTripped())
            {
                return GetAvailabilitySummary();
            }

            if (SupportsSearchPipeline)
            {
                return $"supported v={_nativeVersion}";
            }

            var pathPart = !string.IsNullOrEmpty(_loadedDllPath) ? $" path={Path.GetFileName(_loadedDllPath)}" : string.Empty;
            var hintPart = !string.IsNullOrEmpty(_dllExportsHint) ? $" {_dllExportsHint}" : string.Empty;
            return $"unsupported v={_nativeVersion} ptr={(_supportsSearchPipelinePointers ? 1 : 0)} tbl={(_supportsSearchPipelineTable ? 1 : 0)}{pathPart}{hintPart}";
        }

        public static bool TryFilterSearchResultsNativeTable(
            List<SearchResultItem> results,
            long minSize,
            long maxSize,
            List<string> extensions,
            bool spanishOnly,
            int minQuality,
            out List<SearchResultItem> filtered)
        {
            filtered = results;
            if (!IsAvailable || !_supportsSearchFilterSort || results == null || results.Count == 0)
            {
                return false;
            }

            if (!HasExport("filter_search_results_table_native"))
            {
                return false;
            }

            var trialEntered = false;
            if (IsCircuitTripped())
            {
                if (!TryEnterHalfOpenTrial())
                {
                    return false;
                }

                trialEntered = true;
            }

            var list = results;
            var handles = new List<GCHandle>();
            GCHandle itemsHandle = default;
            GCHandle resultsHandle = default;
            GCHandle extPtrsHandle = default;
            GCHandle arenaHandle = default;
            SearchResultInfoOffsets[]? itemsRented = null;
            int[]? indicesRented = null;
            byte[]? arenaBytesRented = null;

            try
            {
                var itemCount = list.Count;
                itemsRented = ArrayPool<SearchResultInfoOffsets>.Shared.Rent(itemCount);
                indicesRented = ArrayPool<int>.Shared.Rent(itemCount);

                var totalBytes = 0;
                for (int i = 0; i < itemCount; i++)
                {
                    var r = list[i];
                    totalBytes += Encoding.UTF8.GetByteCount(r.Filename ?? string.Empty);
                    totalBytes += Encoding.UTF8.GetByteCount(r.Extension ?? string.Empty);
                }

                arenaBytesRented = ArrayPool<byte>.Shared.Rent(Math.Max(1, totalBytes));
                var arenaSpan = arenaBytesRented.AsSpan(0, Math.Max(1, totalBytes));
                var arenaOffset = 0;

                for (int i = 0; i < itemCount; i++)
                {
                    var r = list[i];
                    var filename = r.Filename ?? string.Empty;
                    var ext = r.Extension ?? string.Empty;

                    var filenameOffset = arenaOffset;
                    var filenameLen = Encoding.UTF8.GetByteCount(filename);
                    if (filenameLen > 0)
                    {
                        _ = Encoding.UTF8.GetBytes(filename, arenaSpan.Slice(arenaOffset, filenameLen));
                    }
                    arenaOffset += filenameLen;

                    var extOffset = arenaOffset;
                    var extLen = Encoding.UTF8.GetByteCount(ext);
                    if (extLen > 0)
                    {
                        _ = Encoding.UTF8.GetBytes(ext, arenaSpan.Slice(arenaOffset, extLen));
                    }
                    arenaOffset += extLen;

                    itemsRented[i] = new SearchResultInfoOffsets
                    {
                        filename_offset = (uint)filenameOffset,
                        filename_len = (uint)filenameLen,
                        extension_offset = (uint)extOffset,
                        extension_len = (uint)extLen,
                        size = r.Size,
                        quality = r.QualityScore,
                        provider_score = 0
                    };
                }

                arenaHandle = GCHandle.Alloc(arenaBytesRented, GCHandleType.Pinned);

                var extPtrs = Array.Empty<IntPtr>();
                if (extensions != null && extensions.Count > 0)
                {
                    extPtrs = new IntPtr[extensions.Count];
                    for (int i = 0; i < extensions.Count; i++)
                    {
                        var ext = extensions[i] ?? string.Empty;
                        var extHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(ext + "\0"), GCHandleType.Pinned);
                        handles.Add(extHandle);
                        extPtrs[i] = extHandle.AddrOfPinnedObject();
                    }
                }

                itemsHandle = GCHandle.Alloc(itemsRented, GCHandleType.Pinned);
                resultsHandle = GCHandle.Alloc(indicesRented, GCHandleType.Pinned);
                if (extPtrs.Length > 0)
                {
                    extPtrsHandle = GCHandle.Alloc(extPtrs, GCHandleType.Pinned);
                }

                var count = filter_search_results_table_native(
                    itemsHandle.AddrOfPinnedObject(),
                    itemCount,
                    arenaHandle.AddrOfPinnedObject(),
                    Math.Max(1, totalBytes),
                    minSize,
                    maxSize,
                    extPtrs.Length > 0 ? extPtrsHandle.AddrOfPinnedObject() : IntPtr.Zero,
                    extPtrs.Length,
                    spanishOnly,
                    minQuality,
                    resultsHandle.AddrOfPinnedObject());

                if (count <= 0)
                {
                    list.Clear();
                    filtered = list;
                    return true;
                }

                ApplyIndicesInPlace(list, indicesRented, count);
                filtered = list;
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch
            {
                filtered = results;
                return false;
            }
            finally
            {
                if (itemsHandle.IsAllocated)
                {
                    itemsHandle.Free();
                }
                if (resultsHandle.IsAllocated)
                {
                    resultsHandle.Free();
                }
                if (extPtrsHandle.IsAllocated)
                {
                    extPtrsHandle.Free();
                }
                if (arenaHandle.IsAllocated)
                {
                    arenaHandle.Free();
                }
                foreach (var h in handles)
                {
                    if (h.IsAllocated)
                    {
                        h.Free();
                    }
                }

                if (itemsRented != null)
                {
                    ArrayPool<SearchResultInfoOffsets>.Shared.Return(itemsRented);
                }
                if (indicesRented != null)
                {
                    ArrayPool<int>.Shared.Return(indicesRented, clearArray: false);
                }
                if (arenaBytesRented != null)
                {
                    ArrayPool<byte>.Shared.Return(arenaBytesRented);
                }
                if (trialEntered)
                {
                    ExitHalfOpenTrial();
                }
            }
        }

        public static bool TryProcessSearchResultsNativeInPlace(
            List<SearchResultItem> results,
            long minSize,
            long maxSize,
            List<string> extensions,
            bool spanishOnly,
            int minQuality,
            Func<string, int> getProviderScore)
        {
            if (!TryProcessSearchResultsNative(
                    results,
                    minSize,
                    maxSize,
                    extensions,
                    spanishOnly,
                    minQuality,
                    getProviderScore,
                    out var processed))
            {
                return false;
            }

            if (ReferenceEquals(processed, results))
            {
                return true;
            }

            results.Clear();
            results.AddRange(processed);
            return true;
        }

        public static bool IsAvailable => _isAvailable && _rustEnabled && !IsCircuitHardOpen();
        public static uint NativeVersion => _nativeVersion;
        public static bool RustEnabled => _rustEnabled;
        public static bool SupportsSearchFilterSort => IsAvailable && _supportsSearchFilterSort;
        public static bool SupportsSearchPipeline => IsAvailable && (_supportsSearchPipelinePointers || _supportsSearchPipelineTable);
        public static bool SupportsDedupeKeysTable => IsAvailable && _supportsDedupeKeysTable;

        public static void SetRustEnabled(bool enabled)
        {
            _rustEnabled = enabled;
        }

        #region P/Invoke Declarations

        // Detección de idioma
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int is_spanish_text_native(IntPtr text_ptr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint GetNativeVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ulong GetNativeCapabilitiesDelegate();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        // Deduplicación de archivos
        [StructLayout(LayoutKind.Sequential)]
        public struct FileInfo
        {
            public IntPtr filename_ptr;
            public IntPtr username_ptr;
            public long size;
            public int score;
        }

        // Filtrado/ordenamiento de resultados de búsqueda (sin JSON)
        [StructLayout(LayoutKind.Sequential)]
        public struct SearchResultInfo
        {
            public IntPtr filename_ptr;
            public IntPtr extension_ptr;
            public long size;
            public int quality;
            public int provider_score;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SearchResultInfoOffsets
        {
            public uint filename_offset;
            public uint filename_len;
            public uint extension_offset;
            public uint extension_len;
            public long size;
            public int quality;
            public int provider_score;
        }

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int deduplicate_files_native(
            IntPtr files_ptr,
            int count,
            IntPtr results_ptr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int deduplicate_keys_table_native(
            IntPtr items_ptr,
            int count,
            IntPtr string_table_ptr,
            int string_table_len,
            IntPtr results_ptr);

        // Filtrado de autores
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int filter_authors_native(
            IntPtr authors_ptr,
            int count,
            IntPtr search_ptr,
            IntPtr results_ptr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int filter_search_results_native(
            IntPtr items_ptr,
            int count,
            long min_size,
            long max_size,
            IntPtr extensions_ptr,
            int ext_count,
            [MarshalAs(UnmanagedType.I1)] bool spanish_only,
            int min_quality,
            IntPtr results_ptr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int filter_search_results_table_native(
            IntPtr items_ptr,
            int count,
            IntPtr string_table_ptr,
            int string_table_len,
            long min_size,
            long max_size,
            IntPtr extensions_ptr,
            int ext_count,
            [MarshalAs(UnmanagedType.I1)] bool spanish_only,
            int min_quality,
            IntPtr results_ptr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sort_by_quality_native(
            IntPtr items_ptr,
            int count,
            IntPtr results_ptr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sort_by_quality_table_native(
            IntPtr items_ptr,
            int count,
            IntPtr results_ptr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int process_search_results_native(
            IntPtr items_ptr,
            int count,
            long min_size,
            long max_size,
            IntPtr extensions_ptr,
            int ext_count,
            [MarshalAs(UnmanagedType.I1)] bool spanish_only,
            int min_quality,
            IntPtr results_ptr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int process_search_results_table_native(
            IntPtr items_ptr,
            int count,
            IntPtr string_table_ptr,
            int string_table_len,
            long min_size,
            long max_size,
            IntPtr extensions_ptr,
            int ext_count,
            [MarshalAs(UnmanagedType.I1)] bool spanish_only,
            int min_quality,
            IntPtr results_ptr);

        #endregion

        #region Safe Wrappers

        /// <summary>
        /// Detectar si un texto está en español (50x más rápido que C#)
        /// </summary>
        public static bool IsSpanishText(string text)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(text))
                return false;

            var trialEntered = false;
            if (IsCircuitTripped())
            {
                if (!TryEnterHalfOpenTrial())
                {
                    return false;
                }

                trialEntered = true;
            }

            IntPtr textPtr = IntPtr.Zero;
            try
            {
                textPtr = Marshal.StringToHGlobalAnsi(text);
                var result = is_spanish_text_native(textPtr) != 0;
                RecordNativeSuccess();
                return result;
            }
            catch
            {
                RecordNativeFailure();
                return false;
            }
            finally
            {
                if (textPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(textPtr);

                if (trialEntered)
                {
                    ExitHalfOpenTrial();
                }
            }
        }

        /// <summary>
        /// Eliminar archivos duplicados por nombre, manteniendo el de mejor proveedor (20x más rápido)
        /// </summary>
        public static List<T> DeduplicateFiles<T>(List<T> files, Func<T, string> getFileName, Func<T, string> getUsername, Func<T, long> getSize, Func<string, int> getProviderScore)
        {
            if (!IsAvailable || files == null || files.Count == 0)
            {
                // Fallback a C# LINQ
                return files
                    .GroupBy(f => getFileName(f), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(f => getProviderScore(getUsername(f))).First())
                    .ToList();
            }

            var trialEntered = false;
            if (IsCircuitTripped())
            {
                if (!TryEnterHalfOpenTrial())
                {
                    return files
                        .GroupBy(f => getFileName(f), StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.OrderByDescending(f => getProviderScore(getUsername(f))).First())
                        .ToList();
                }

                trialEntered = true;
            }

            try
            {
                // Preparar estructuras para Rust
                var fileInfos = new FileInfo[files.Count];
                var handles = new List<GCHandle>();

                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var filename = getFileName(file);
                    var username = getUsername(file);

                    var filenameHandle = GCHandle.Alloc(System.Text.Encoding.UTF8.GetBytes(filename + "\0"), GCHandleType.Pinned);
                    var usernameHandle = GCHandle.Alloc(System.Text.Encoding.UTF8.GetBytes(username + "\0"), GCHandleType.Pinned);
                    handles.Add(filenameHandle);
                    handles.Add(usernameHandle);

                    fileInfos[i] = new FileInfo
                    {
                        filename_ptr = filenameHandle.AddrOfPinnedObject(),
                        username_ptr = usernameHandle.AddrOfPinnedObject(),
                        size = getSize(file),
                        score = getProviderScore(username)
                    };
                }

                // Llamar a Rust
                var results = new int[files.Count];
                var fileInfosHandle = GCHandle.Alloc(fileInfos, GCHandleType.Pinned);
                var resultsHandle = GCHandle.Alloc(results, GCHandleType.Pinned);

                try
                {
                    var count = deduplicate_files_native(
                        fileInfosHandle.AddrOfPinnedObject(),
                        files.Count,
                        resultsHandle.AddrOfPinnedObject());

                    // Construir lista de resultados
                    var uniqueFiles = new List<T>(count);
                    for (int i = 0; i < count; i++)
                    {
                        uniqueFiles.Add(files[results[i]]);
                    }

                    RecordNativeSuccess();
                    return uniqueFiles;
                }
                finally
                {
                    fileInfosHandle.Free();
                    resultsHandle.Free();
                    foreach (var handle in handles)
                    {
                        if (handle.IsAllocated)
                            handle.Free();
                    }
                }
            }
            catch
            {
                RecordNativeFailure();
                // Fallback a C# LINQ si falla
                return files
                    .GroupBy(f => getFileName(f), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(f => getProviderScore(getUsername(f))).First())
                    .ToList();
            }
            finally
            {
                if (trialEntered)
                {
                    ExitHalfOpenTrial();
                }
            }
        }

        public static bool TryDeduplicateKeysNativeTable(
            List<SearchResultItem> items,
            Func<SearchResultItem, string> getKey,
            Func<string, int> getProviderScore,
            out List<SearchResultItem> unique)
        {
            unique = items;
            if (!IsAvailable || items == null || items.Count == 0)
            {
                return false;
            }

            if (!HasExport("deduplicate_keys_table_native"))
            {
                return false;
            }

            var list = items;
            GCHandle itemsHandle = default;
            GCHandle resultsHandle = default;
            GCHandle arenaHandle = default;
            SearchResultInfoOffsets[]? itemsRented = null;
            int[]? indicesRented = null;
            byte[]? arenaBytesRented = null;

            try
            {
                var count = list.Count;
                itemsRented = ArrayPool<SearchResultInfoOffsets>.Shared.Rent(count);
                indicesRented = ArrayPool<int>.Shared.Rent(count);

                var totalBytes = 0;
                for (int i = 0; i < count; i++)
                {
                    var key = getKey(list[i]) ?? string.Empty;
                    totalBytes += Encoding.UTF8.GetByteCount(key);
                }

                arenaBytesRented = ArrayPool<byte>.Shared.Rent(Math.Max(1, totalBytes));
                var arenaSpan = arenaBytesRented.AsSpan(0, Math.Max(1, totalBytes));
                var arenaOffset = 0;

                for (int i = 0; i < count; i++)
                {
                    var r = list[i];
                    var key = getKey(r) ?? string.Empty;
                    var keyOffset = arenaOffset;
                    var keyLen = Encoding.UTF8.GetByteCount(key);
                    if (keyLen > 0)
                    {
                        _ = Encoding.UTF8.GetBytes(key, arenaSpan.Slice(arenaOffset, keyLen));
                    }
                    arenaOffset += keyLen;

                    var username = r.Username ?? string.Empty;
                    var score = getProviderScore != null ? getProviderScore(username) : 0;

                    itemsRented[i] = new SearchResultInfoOffsets
                    {
                        filename_offset = (uint)keyOffset,
                        filename_len = (uint)keyLen,
                        extension_offset = 0,
                        extension_len = 0,
                        size = r.Size,
                        quality = 0,
                        provider_score = score
                    };
                }

                arenaHandle = GCHandle.Alloc(arenaBytesRented, GCHandleType.Pinned);
                itemsHandle = GCHandle.Alloc(itemsRented, GCHandleType.Pinned);
                resultsHandle = GCHandle.Alloc(indicesRented, GCHandleType.Pinned);

                var outCount = deduplicate_keys_table_native(
                    itemsHandle.AddrOfPinnedObject(),
                    count,
                    arenaHandle.AddrOfPinnedObject(),
                    Math.Max(1, totalBytes),
                    resultsHandle.AddrOfPinnedObject());

                if (outCount <= 0)
                {
                    list.Clear();
                    unique = list;
                    return true;
                }

                ApplyIndicesInPlace(list, indicesRented, outCount);
                unique = list;
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch
            {
                unique = items;
                return false;
            }
            finally
            {
                if (itemsHandle.IsAllocated)
                {
                    itemsHandle.Free();
                }
                if (resultsHandle.IsAllocated)
                {
                    resultsHandle.Free();
                }
                if (arenaHandle.IsAllocated)
                {
                    arenaHandle.Free();
                }

                if (itemsRented != null)
                {
                    ArrayPool<SearchResultInfoOffsets>.Shared.Return(itemsRented);
                }
                if (indicesRented != null)
                {
                    ArrayPool<int>.Shared.Return(indicesRented, clearArray: false);
                }
                if (arenaBytesRented != null)
                {
                    ArrayPool<byte>.Shared.Return(arenaBytesRented);
                }
            }
        }

        /// <summary>
        /// Filtrar autores por búsqueda (10x más rápido)
        /// </summary>
        public static int[] FilterAuthors(string[] authors, string search)
        {
            if (!IsAvailable || authors == null || authors.Length == 0 || string.IsNullOrWhiteSpace(search))
            {
                // Fallback a C# LINQ
                return authors
                    .Select((author, index) => new { author, index })
                    .Where(x => x.author.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.index)
                    .ToArray();
            }

            try
            {
                // Preparar punteros de autores
                var authorPtrs = new IntPtr[authors.Length];
                var handles = new List<GCHandle>();

                for (int i = 0; i < authors.Length; i++)
                {
                    var handle = GCHandle.Alloc(System.Text.Encoding.UTF8.GetBytes(authors[i] + "\0"), GCHandleType.Pinned);
                    handles.Add(handle);
                    authorPtrs[i] = handle.AddrOfPinnedObject();
                }

                var results = new int[authors.Length];
                var authorPtrsHandle = GCHandle.Alloc(authorPtrs, GCHandleType.Pinned);
                var resultsHandle = GCHandle.Alloc(results, GCHandleType.Pinned);
                var searchHandle = GCHandle.Alloc(System.Text.Encoding.UTF8.GetBytes(search + "\0"), GCHandleType.Pinned);

                try
                {
                    var count = filter_authors_native(
                        authorPtrsHandle.AddrOfPinnedObject(),
                        authors.Length,
                        searchHandle.AddrOfPinnedObject(),
                        resultsHandle.AddrOfPinnedObject());

                    var filteredIndices = new int[count];
                    Array.Copy(results, filteredIndices, count);
                    return filteredIndices;
                }
                finally
                {
                    authorPtrsHandle.Free();
                    resultsHandle.Free();
                    searchHandle.Free();
                    foreach (var handle in handles)
                    {
                        if (handle.IsAllocated)
                            handle.Free();
                    }
                }
            }
            catch
            {
                // Fallback a C# LINQ
                return authors
                    .Select((author, index) => new { author, index })
                    .Where(x => x.author.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.index)
                    .ToArray();
            }
        }

        public static bool TryFilterSearchResultsNative(
            List<SearchResultItem> results,
            long minSize,
            long maxSize,
            List<string> extensions,
            bool spanishOnly,
            int minQuality,
            out List<SearchResultItem> filtered)
        {
            filtered = results;
            if (!IsAvailable || !_supportsSearchFilterSort || results == null || results.Count == 0)
            {
                return false;
            }

            var trialEntered = false;
            if (IsCircuitTripped())
            {
                if (!TryEnterHalfOpenTrial())
                {
                    return false;
                }

                trialEntered = true;
            }

            var list = results;
            var handles = new List<GCHandle>();
            GCHandle itemsHandle = default;
            GCHandle resultsHandle = default;
            GCHandle extPtrsHandle = default;
            SearchResultInfo[]? itemsRented = null;
            int[]? indicesRented = null;

            try
            {
                var itemCount = list.Count;
                itemsRented = ArrayPool<SearchResultInfo>.Shared.Rent(itemCount);
                indicesRented = ArrayPool<int>.Shared.Rent(itemCount);

                for (int i = 0; i < itemCount; i++)
                {
                    var r = list[i];
                    var filenameBytes = Encoding.UTF8.GetBytes((r.Filename ?? string.Empty) + "\0");
                    var extBytes = Encoding.UTF8.GetBytes((r.Extension ?? string.Empty) + "\0");

                    var filenameHandle = GCHandle.Alloc(filenameBytes, GCHandleType.Pinned);
                    var extHandle = GCHandle.Alloc(extBytes, GCHandleType.Pinned);
                    handles.Add(filenameHandle);
                    handles.Add(extHandle);

                    itemsRented[i] = new SearchResultInfo
                    {
                        filename_ptr = filenameHandle.AddrOfPinnedObject(),
                        extension_ptr = extHandle.AddrOfPinnedObject(),
                        size = r.Size,
                        quality = r.QualityScore,
                        provider_score = 0
                    };
                }

                var extPtrs = Array.Empty<IntPtr>();
                if (extensions != null && extensions.Count > 0)
                {
                    extPtrs = new IntPtr[extensions.Count];
                    for (int i = 0; i < extensions.Count; i++)
                    {
                        var ext = extensions[i] ?? string.Empty;
                        var extHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(ext + "\0"), GCHandleType.Pinned);
                        handles.Add(extHandle);
                        extPtrs[i] = extHandle.AddrOfPinnedObject();
                    }
                }

                itemsHandle = GCHandle.Alloc(itemsRented, GCHandleType.Pinned);
                resultsHandle = GCHandle.Alloc(indicesRented, GCHandleType.Pinned);
                if (extPtrs.Length > 0)
                {
                    extPtrsHandle = GCHandle.Alloc(extPtrs, GCHandleType.Pinned);
                }

                var count = filter_search_results_native(
                    itemsHandle.AddrOfPinnedObject(),
                    itemCount,
                    minSize,
                    maxSize,
                    extPtrs.Length > 0 ? extPtrsHandle.AddrOfPinnedObject() : IntPtr.Zero,
                    extPtrs.Length,
                    spanishOnly,
                    minQuality,
                    resultsHandle.AddrOfPinnedObject());

                if (count <= 0)
                {
                    list.Clear();
                    filtered = list;
                    RecordNativeSuccess();
                    return true;
                }

                ApplyIndicesInPlace(list, indicesRented, count);
                filtered = list;
                RecordNativeSuccess();
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                _supportsSearchFilterSort = false;
                filtered = results;
                return false;
            }
            catch
            {
                RecordNativeFailure();
                filtered = results;
                return false;
            }
            finally
            {
                if (trialEntered)
                {
                    ExitHalfOpenTrial();
                }

                if (itemsHandle.IsAllocated)
                {
                    itemsHandle.Free();
                }
                if (resultsHandle.IsAllocated)
                {
                    resultsHandle.Free();
                }
                if (extPtrsHandle.IsAllocated)
                {
                    extPtrsHandle.Free();
                }
                foreach (var h in handles)
                {
                    if (h.IsAllocated)
                    {
                        h.Free();
                    }
                }

                if (itemsRented != null)
                {
                    ArrayPool<SearchResultInfo>.Shared.Return(itemsRented, clearArray: false);
                }
                if (indicesRented != null)
                {
                    ArrayPool<int>.Shared.Return(indicesRented, clearArray: false);
                }
            }
        }

        public static bool TrySortByQualityNativeTable(List<SearchResultItem> results, out List<SearchResultItem> sorted)
        {
            sorted = results;
            if (!IsAvailable || !_supportsSearchFilterSort || results == null || results.Count == 0)
            {
                return false;
            }

            if (!HasExport("sort_by_quality_table_native"))
            {
                return false;
            }

            var trialEntered = false;
            if (IsCircuitTripped())
            {
                if (!TryEnterHalfOpenTrial())
                {
                    return false;
                }

                trialEntered = true;
            }

            var list = results;
            GCHandle itemsHandle = default;
            GCHandle resultsHandle = default;
            SearchResultInfoOffsets[]? itemsRented = null;
            int[]? indicesRented = null;

            try
            {
                var itemCount = list.Count;
                itemsRented = ArrayPool<SearchResultInfoOffsets>.Shared.Rent(itemCount);
                indicesRented = ArrayPool<int>.Shared.Rent(itemCount);

                for (int i = 0; i < itemCount; i++)
                {
                    var r = list[i];
                    itemsRented[i] = new SearchResultInfoOffsets
                    {
                        filename_offset = 0,
                        filename_len = 0,
                        extension_offset = 0,
                        extension_len = 0,
                        size = r.Size,
                        quality = r.QualityScore,
                        provider_score = 0
                    };
                }

                itemsHandle = GCHandle.Alloc(itemsRented, GCHandleType.Pinned);
                resultsHandle = GCHandle.Alloc(indicesRented, GCHandleType.Pinned);

                var count = sort_by_quality_table_native(
                    itemsHandle.AddrOfPinnedObject(),
                    itemCount,
                    resultsHandle.AddrOfPinnedObject());

                if (count <= 0)
                {
                    list.Clear();
                    sorted = list;
                    RecordNativeSuccess();
                    return true;
                }

                ApplyIndicesInPlace(list, indicesRented, count);
                sorted = list;
                RecordNativeSuccess();
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                _supportsSearchFilterSort = false;
                sorted = results;
                return false;
            }
            catch
            {
                RecordNativeFailure();
                sorted = results;
                return false;
            }
            finally
            {
                if (itemsHandle.IsAllocated)
                {
                    itemsHandle.Free();
                }
                if (resultsHandle.IsAllocated)
                {
                    resultsHandle.Free();
                }

                if (itemsRented != null)
                {
                    ArrayPool<SearchResultInfoOffsets>.Shared.Return(itemsRented);
                }
                if (indicesRented != null)
                {
                    ArrayPool<int>.Shared.Return(indicesRented, clearArray: false);
                }
                if (trialEntered)
                {
                    ExitHalfOpenTrial();
                }
            }
        }

        public static bool TryProcessSearchResultsNativeTable(
            List<SearchResultItem> results,
            long minSize,
            long maxSize,
            List<string> extensions,
            bool spanishOnly,
            int minQuality,
            Func<string, int> getProviderScore,
            out List<SearchResultItem> processed)
        {
            processed = results;
            if (!IsAvailable || !_supportsSearchPipelineTable || results == null || results.Count == 0)
            {
                return false;
            }

            if (!HasExport("process_search_results_table_native"))
            {
                return false;
            }

            var trialEntered = false;
            if (IsCircuitTripped())
            {
                if (!TryEnterHalfOpenTrial())
                {
                    return false;
                }

                trialEntered = true;
            }

            var list = results;
            var handles = new List<GCHandle>();
            GCHandle itemsHandle = default;
            GCHandle resultsHandle = default;
            GCHandle extPtrsHandle = default;
            GCHandle arenaHandle = default;
            SearchResultInfoOffsets[]? itemsRented = null;
            int[]? indicesRented = null;
            byte[]? arenaBytesRented = null;

            try
            {
                var itemCount = list.Count;
                itemsRented = ArrayPool<SearchResultInfoOffsets>.Shared.Rent(itemCount);
                indicesRented = ArrayPool<int>.Shared.Rent(itemCount);

                var totalBytes = 0;
                for (int i = 0; i < itemCount; i++)
                {
                    var r = list[i];
                    var filename = r.Filename ?? string.Empty;
                    var ext = r.Extension ?? string.Empty;

                    totalBytes += Encoding.UTF8.GetByteCount(filename);
                    totalBytes += Encoding.UTF8.GetByteCount(ext);
                    if (totalBytes < 0)
                    {
                        throw new InvalidOperationException();
                    }
                }

                arenaBytesRented = ArrayPool<byte>.Shared.Rent(Math.Max(1, totalBytes));
                var arenaSpan = arenaBytesRented.AsSpan(0, Math.Max(1, totalBytes));
                var arenaOffset = 0;

                for (int i = 0; i < itemCount; i++)
                {
                    var r = list[i];
                    var filename = r.Filename ?? string.Empty;
                    var ext = r.Extension ?? string.Empty;

                    var filenameOffset = arenaOffset;
                    var filenameLen = Encoding.UTF8.GetByteCount(filename);
                    if (filenameLen > 0)
                    {
                        _ = Encoding.UTF8.GetBytes(filename, arenaSpan.Slice(arenaOffset, filenameLen));
                    }
                    arenaOffset += filenameLen;

                    var extOffset = arenaOffset;
                    var extLen = Encoding.UTF8.GetByteCount(ext);
                    if (extLen > 0)
                    {
                        _ = Encoding.UTF8.GetBytes(ext, arenaSpan.Slice(arenaOffset, extLen));
                    }
                    arenaOffset += extLen;

                    var username = r.Username ?? string.Empty;
                    var providerScore = getProviderScore != null ? getProviderScore(username) : 0;

                    itemsRented[i] = new SearchResultInfoOffsets
                    {
                        filename_offset = (uint)filenameOffset,
                        filename_len = (uint)filenameLen,
                        extension_offset = (uint)extOffset,
                        extension_len = (uint)extLen,
                        size = r.Size,
                        quality = r.QualityScore,
                        provider_score = providerScore
                    };
                }

                arenaHandle = GCHandle.Alloc(arenaBytesRented, GCHandleType.Pinned);

                var extPtrs = Array.Empty<IntPtr>();
                if (extensions != null && extensions.Count > 0)
                {
                    extPtrs = new IntPtr[extensions.Count];
                    for (int i = 0; i < extensions.Count; i++)
                    {
                        var ext = extensions[i] ?? string.Empty;
                        var extHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(ext + "\0"), GCHandleType.Pinned);
                        handles.Add(extHandle);
                        extPtrs[i] = extHandle.AddrOfPinnedObject();
                    }
                }

                itemsHandle = GCHandle.Alloc(itemsRented, GCHandleType.Pinned);
                resultsHandle = GCHandle.Alloc(indicesRented, GCHandleType.Pinned);
                if (extPtrs.Length > 0)
                {
                    extPtrsHandle = GCHandle.Alloc(extPtrs, GCHandleType.Pinned);
                }

                var count = process_search_results_table_native(
                    itemsHandle.AddrOfPinnedObject(),
                    itemCount,
                    arenaHandle.AddrOfPinnedObject(),
                    Math.Max(1, totalBytes),
                    minSize,
                    maxSize,
                    extPtrs.Length > 0 ? extPtrsHandle.AddrOfPinnedObject() : IntPtr.Zero,
                    extPtrs.Length,
                    spanishOnly,
                    minQuality,
                    resultsHandle.AddrOfPinnedObject());

                if (count <= 0)
                {
                    list.Clear();
                    processed = list;
                    RecordNativeSuccess();
                    return true;
                }

                ApplyIndicesInPlace(list, indicesRented, count);
                processed = list;
                RecordNativeSuccess();
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                _supportsSearchPipelineTable = false;
                processed = results;
                return false;
            }
            catch
            {
                RecordNativeFailure();
                processed = results;
                return false;
            }
            finally
            {
                if (itemsHandle.IsAllocated)
                {
                    itemsHandle.Free();
                }
                if (resultsHandle.IsAllocated)
                {
                    resultsHandle.Free();
                }
                if (extPtrsHandle.IsAllocated)
                {
                    extPtrsHandle.Free();
                }
                if (arenaHandle.IsAllocated)
                {
                    arenaHandle.Free();
                }
                foreach (var h in handles)
                {
                    if (h.IsAllocated)
                    {
                        h.Free();
                    }
                }

                if (itemsRented != null)
                {
                    ArrayPool<SearchResultInfoOffsets>.Shared.Return(itemsRented);
                }
                if (indicesRented != null)
                {
                    ArrayPool<int>.Shared.Return(indicesRented);
                }
                if (arenaBytesRented != null)
                {
                    ArrayPool<byte>.Shared.Return(arenaBytesRented);
                }
                if (trialEntered)
                {
                    ExitHalfOpenTrial();
                }
            }
        }

        public static bool TrySortByQualityNative(List<SearchResultItem> results, out List<SearchResultItem> sorted)
        {
            sorted = results;
            if (!IsAvailable || !_supportsSearchFilterSort || results == null || results.Count == 0)
            {
                return false;
            }

            var trialEntered = false;
            if (IsCircuitTripped())
            {
                if (!TryEnterHalfOpenTrial())
                {
                    return false;
                }

                trialEntered = true;
            }

            var list = results;
            GCHandle itemsHandle = default;
            GCHandle resultsHandle = default;
            SearchResultInfo[]? itemsRented = null;
            int[]? indicesRented = null;

            try
            {
                var itemCount = list.Count;
                itemsRented = ArrayPool<SearchResultInfo>.Shared.Rent(itemCount);
                indicesRented = ArrayPool<int>.Shared.Rent(itemCount);

                for (int i = 0; i < itemCount; i++)
                {
                    var r = list[i];
                    itemsRented[i] = new SearchResultInfo
                    {
                        filename_ptr = IntPtr.Zero,
                        extension_ptr = IntPtr.Zero,
                        size = r.Size,
                        quality = r.QualityScore,
                        provider_score = 0
                    };
                }

                itemsHandle = GCHandle.Alloc(itemsRented, GCHandleType.Pinned);
                resultsHandle = GCHandle.Alloc(indicesRented, GCHandleType.Pinned);

                var count = sort_by_quality_native(
                    itemsHandle.AddrOfPinnedObject(),
                    itemCount,
                    resultsHandle.AddrOfPinnedObject());

                if (count <= 0)
                {
                    list.Clear();
                    sorted = list;
                    RecordNativeSuccess();
                    return true;
                }

                ApplyIndicesInPlace(list, indicesRented, count);
                sorted = list;
                RecordNativeSuccess();
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                _supportsSearchFilterSort = false;
                sorted = results;
                return false;
            }
            catch
            {
                RecordNativeFailure();
                sorted = results;
                return false;
            }
            finally
            {
                if (trialEntered)
                {
                    ExitHalfOpenTrial();
                }

                if (itemsHandle.IsAllocated)
                {
                    itemsHandle.Free();
                }
                if (resultsHandle.IsAllocated)
                {
                    resultsHandle.Free();
                }

                if (itemsRented != null)
                {
                    ArrayPool<SearchResultInfo>.Shared.Return(itemsRented, clearArray: false);
                }
                if (indicesRented != null)
                {
                    ArrayPool<int>.Shared.Return(indicesRented, clearArray: false);
                }
            }
        }

        public static bool TryProcessSearchResultsNative(
            List<SearchResultItem> results,
            long minSize,
            long maxSize,
            List<string> extensions,
            bool spanishOnly,
            int minQuality,
            Func<string, int> getProviderScore,
            out List<SearchResultItem> processed)
        {
            processed = results;
            if (!IsAvailable || !_supportsSearchPipelinePointers || results == null || results.Count == 0)
            {
                return false;
            }

            var trialEntered = false;
            if (IsCircuitTripped())
            {
                if (!TryEnterHalfOpenTrial())
                {
                    return false;
                }

                trialEntered = true;
            }

            var list = results;
            var handles = new List<GCHandle>();
            GCHandle itemsHandle = default;
            GCHandle resultsHandle = default;
            GCHandle extPtrsHandle = default;
            SearchResultInfo[]? itemsRented = null;
            int[]? indicesRented = null;

            try
            {
                var itemCount = list.Count;
                itemsRented = ArrayPool<SearchResultInfo>.Shared.Rent(itemCount);
                indicesRented = ArrayPool<int>.Shared.Rent(itemCount);

                for (int i = 0; i < itemCount; i++)
                {
                    var r = list[i];
                    var filenameBytes = Encoding.UTF8.GetBytes((r.Filename ?? string.Empty) + "\0");
                    var extBytes = Encoding.UTF8.GetBytes((r.Extension ?? string.Empty) + "\0");

                    var filenameHandle = GCHandle.Alloc(filenameBytes, GCHandleType.Pinned);
                    var extHandle = GCHandle.Alloc(extBytes, GCHandleType.Pinned);
                    handles.Add(filenameHandle);
                    handles.Add(extHandle);

                    var username = r.Username ?? string.Empty;
                    var providerScore = getProviderScore != null ? getProviderScore(username) : 0;

                    itemsRented[i] = new SearchResultInfo
                    {
                        filename_ptr = filenameHandle.AddrOfPinnedObject(),
                        extension_ptr = extHandle.AddrOfPinnedObject(),
                        size = r.Size,
                        quality = r.QualityScore,
                        provider_score = providerScore
                    };
                }

                var extPtrs = Array.Empty<IntPtr>();
                if (extensions != null && extensions.Count > 0)
                {
                    extPtrs = new IntPtr[extensions.Count];
                    for (int i = 0; i < extensions.Count; i++)
                    {
                        var ext = extensions[i] ?? string.Empty;
                        var extHandle = GCHandle.Alloc(Encoding.UTF8.GetBytes(ext + "\0"), GCHandleType.Pinned);
                        handles.Add(extHandle);
                        extPtrs[i] = extHandle.AddrOfPinnedObject();
                    }
                }

                itemsHandle = GCHandle.Alloc(itemsRented, GCHandleType.Pinned);
                resultsHandle = GCHandle.Alloc(indicesRented, GCHandleType.Pinned);
                if (extPtrs.Length > 0)
                {
                    extPtrsHandle = GCHandle.Alloc(extPtrs, GCHandleType.Pinned);
                }

                var count = process_search_results_native(
                    itemsHandle.AddrOfPinnedObject(),
                    itemCount,
                    minSize,
                    maxSize,
                    extPtrs.Length > 0 ? extPtrsHandle.AddrOfPinnedObject() : IntPtr.Zero,
                    extPtrs.Length,
                    spanishOnly,
                    minQuality,
                    resultsHandle.AddrOfPinnedObject());

                if (count <= 0)
                {
                    list.Clear();
                    processed = list;
                    RecordNativeSuccess();
                    return true;
                }

                ApplyIndicesInPlace(list, indicesRented, count);
                processed = list;
                RecordNativeSuccess();
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                _supportsSearchPipelinePointers = false;
                processed = results;
                return false;
            }
            catch
            {
                RecordNativeFailure();
                processed = results;
                return false;
            }
            finally
            {
                if (itemsHandle.IsAllocated)
                {
                    itemsHandle.Free();
                }
                if (resultsHandle.IsAllocated)
                {
                    resultsHandle.Free();
                }
                if (extPtrsHandle.IsAllocated)
                {
                    extPtrsHandle.Free();
                }
                foreach (var h in handles)
                {
                    if (h.IsAllocated)
                    {
                        h.Free();
                    }
                }

                if (itemsRented != null)
                {
                    ArrayPool<SearchResultInfo>.Shared.Return(itemsRented, clearArray: false);
                }
                if (indicesRented != null)
                {
                    ArrayPool<int>.Shared.Return(indicesRented, clearArray: false);
                }
            }
        }

        private static void ApplyIndicesInPlace(List<SearchResultItem> list, int[] indices, int count)
        {
            if (count <= 0)
            {
                list.Clear();
                return;
            }

            if (count == list.Count)
            {
                var identity = true;
                for (int i = 0; i < count; i++)
                {
                    if (indices[i] != i)
                    {
                        identity = false;
                        break;
                    }
                }

                if (identity)
                {
                    return;
                }
            }

            var tmp = ArrayPool<SearchResultItem>.Shared.Rent(count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var idx = indices[i];
                    tmp[i] = (idx >= 0 && idx < list.Count) ? list[idx] : null;
                }

                list.Clear();
                if (list.Capacity < count)
                {
                    list.Capacity = count;
                }

                for (int i = 0; i < count; i++)
                {
                    var item = tmp[i];
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
            }
            finally
            {
                ArrayPool<SearchResultItem>.Shared.Return(tmp, clearArray: true);
            }
        }

        public static bool TryProcessSearchResultsNativeTableInPlace(
            List<SearchResultItem> results,
            long minSize,
            long maxSize,
            List<string> extensions,
            bool spanishOnly,
            int minQuality,
            Func<string, int> getProviderScore)
        {
            if (!TryProcessSearchResultsNativeTable(
                    results,
                    minSize,
                    maxSize,
                    extensions,
                    spanishOnly,
                    minQuality,
                    getProviderScore,
                    out var processed))
            {
                return false;
            }

            if (ReferenceEquals(processed, results))
            {
                return true;
            }

            results.Clear();
            results.AddRange(processed);
            return true;
        }

        #endregion

        private static void InitializeCapabilities()
        {
            var hasFilter = HasExport("filter_search_results_native") || HasExport("filter_search_results_table_native");
            var hasSort = HasExport("sort_by_quality_native") || HasExport("sort_by_quality_table_native");
            _supportsSearchFilterSort = hasFilter && hasSort;
            _supportsSearchPipelinePointers = HasExport("process_search_results_native");
            _supportsSearchPipelineTable = HasExport("process_search_results_table_native");
            _supportsDedupeKeysTable = HasExport("deduplicate_keys_table_native");

            var versionPtr = GetProcAddress(_dllHandle, "get_native_version");
            if (versionPtr != IntPtr.Zero)
            {
                var getVersion = Marshal.GetDelegateForFunctionPointer<GetNativeVersionDelegate>(versionPtr);
                _nativeVersion = getVersion();
            }

            var capsPtr = GetProcAddress(_dllHandle, "get_native_capabilities");
            if (capsPtr != IntPtr.Zero)
            {
                var getCaps = Marshal.GetDelegateForFunctionPointer<GetNativeCapabilitiesDelegate>(capsPtr);
                var caps = getCaps();

                var capHasFilter = (caps & CAP_FILTER_SEARCH_RESULTS) != 0 || (caps & CAP_FILTER_SEARCH_RESULTS_TABLE) != 0;
                var capHasSort = (caps & CAP_SORT_BY_QUALITY) != 0 || (caps & CAP_SORT_BY_QUALITY_TABLE) != 0;
                _supportsSearchFilterSort = capHasFilter && capHasSort;
                _supportsSearchPipelinePointers = (caps & CAP_PROCESS_SEARCH_RESULTS) != 0;
                _supportsSearchPipelineTable = (caps & CAP_PROCESS_SEARCH_RESULTS_TABLE) != 0;
                _supportsDedupeKeysTable = (caps & CAP_DEDUPLICATE_KEYS_TABLE) != 0;
            }
        }

        private static bool HasExport(string name)
        {
            if (_dllHandle == IntPtr.Zero)
            {
                return false;
            }

            return GetProcAddress(_dllHandle, name) != IntPtr.Zero;
        }

        private static string? TryBuildDllExportsHint(string dllPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                {
                    return null;
                }

                var data = File.ReadAllBytes(dllPath);
                var names = TryReadPeExportNames(data, maxNames: 4096);
                if (names == null)
                {
                    return null;
                }

                var v = names.Contains("get_native_version") ? 1 : 0;
                var c = names.Contains("get_native_capabilities") ? 1 : 0;
                var p = names.Contains("process_search_results_native") ? 1 : 0;
                var t = names.Contains("process_search_results_table_native") ? 1 : 0;
                var bit = IntPtr.Size * 8;
                return $"exp(ver={v} cap={c} ptr={p} tbl={t} bit={bit} n={names.Count})";
            }
            catch
            {
                return null;
            }
        }

        private static HashSet<string>? TryReadPeExportNames(byte[] data, int maxNames)
        {
            if (data == null || data.Length < 0x100)
            {
                return null;
            }

            if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2)) != 0x5A4D)
            {
                return null;
            }

            var lfanew = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x3C, 4));
            if (lfanew <= 0 || lfanew + 4 > data.Length)
            {
                return null;
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(lfanew, 4)) != 0x00004550)
            {
                return null;
            }

            var fileHeaderOffset = lfanew + 4;
            if (fileHeaderOffset + 20 > data.Length)
            {
                return null;
            }

            var sizeOfOptionalHeader = (int)BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(fileHeaderOffset + 16, 2));
            var optionalHeaderOffset = fileHeaderOffset + 20;
            var sectionHeadersOffset = optionalHeaderOffset + sizeOfOptionalHeader;
            if (optionalHeaderOffset + sizeOfOptionalHeader > data.Length)
            {
                return null;
            }

            var numberOfSections = (int)BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(fileHeaderOffset + 2, 2));
            if (numberOfSections <= 0)
            {
                return null;
            }

            var magic = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(optionalHeaderOffset, 2));
            var dataDirOffset = magic switch
            {
                0x10B => optionalHeaderOffset + 96,
                0x20B => optionalHeaderOffset + 112,
                _ => -1
            };
            if (dataDirOffset < 0 || dataDirOffset + 8 > data.Length)
            {
                return null;
            }

            var exportTableRva = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(dataDirOffset, 4));
            var exportTableSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(dataDirOffset + 4, 4));
            if (exportTableRva == 0 || exportTableSize == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var exportDirOffset = RvaToFileOffset(data, sectionHeadersOffset, numberOfSections, exportTableRva);
            if (exportDirOffset <= 0 || exportDirOffset + 40 > data.Length)
            {
                return null;
            }

            var numberOfNames = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(exportDirOffset + 24, 4));
            var addressOfNamesRva = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(exportDirOffset + 32, 4));
            if (numberOfNames == 0 || addressOfNamesRva == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var namesTableOffset = RvaToFileOffset(data, sectionHeadersOffset, numberOfSections, addressOfNamesRva);
            if (namesTableOffset <= 0)
            {
                return null;
            }

            var count = (int)Math.Min((uint)Math.Max(0, maxNames), numberOfNames);
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                var entryOffset = namesTableOffset + (i * 4);
                if (entryOffset + 4 > data.Length)
                {
                    break;
                }

                var nameRva = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(entryOffset, 4));
                var nameOffset = RvaToFileOffset(data, sectionHeadersOffset, numberOfSections, nameRva);
                if (nameOffset <= 0 || nameOffset >= data.Length)
                {
                    continue;
                }

                var s = ReadNullTerminatedAscii(data, nameOffset);
                if (!string.IsNullOrEmpty(s))
                {
                    set.Add(s);
                }
            }

            return set;
        }

        private static int RvaToFileOffset(byte[] data, int sectionHeadersOffset, int numberOfSections, uint rva)
        {
            const int sectionHeaderSize = 40;
            for (int i = 0; i < numberOfSections; i++)
            {
                var off = sectionHeadersOffset + (i * sectionHeaderSize);
                if (off + sectionHeaderSize > data.Length)
                {
                    return -1;
                }

                var virtualSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 8, 4));
                var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 12, 4));
                var sizeOfRawData = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 16, 4));
                var pointerToRawData = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 20, 4));

                var size = Math.Max(virtualSize, sizeOfRawData);
                if (rva >= virtualAddress && rva < virtualAddress + size)
                {
                    var delta = rva - virtualAddress;
                    var fileOffset = (long)pointerToRawData + delta;
                    if (fileOffset < 0 || fileOffset > int.MaxValue)
                    {
                        return -1;
                    }
                    return (int)fileOffset;
                }
            }

            return -1;
        }

        private static string? ReadNullTerminatedAscii(byte[] data, int offset)
        {
            if (offset < 0 || offset >= data.Length)
            {
                return null;
            }

            var end = offset;
            while (end < data.Length && data[end] != 0)
            {
                end++;
            }

            if (end == offset)
            {
                return string.Empty;
            }

            return Encoding.ASCII.GetString(data, offset, end - offset);
        }
    }
}
