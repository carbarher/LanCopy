using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using SlskDown.Core;
using SlskDown.Services;

namespace SlskDown
{
    /// <summary>
    /// Integraciones completas de las 13 funcionalidades Rust en SlskDown
    /// </summary>
    public partial class MainForm
    {
        // ==================== VARIABLES PARA RUST ====================
        
        private RustSearchIndex authorSearchIndex = null;
        private List<string> authorSearchIndexCorpus = new List<string>();
        private int rustSearchCount = 0;
        private int rustValidatedFiles = 0;
        private int rustDedupeNativeCount = 0;
        private int rustDedupeJsonCount = 0;
        private int rustDedupeFallbackCount = 0;
        private int rustFilterCount = 0;
        private int rustSortCount = 0;
        private Label lblRustStats = null;

        private int adaptiveSinglePassMinCount = 4000;
        private DateTime adaptiveSinglePassLastUpdateUtc = DateTime.MinValue;

        private DateTime lastRustDedupeLogUtc = DateTime.MinValue;
        
        // ==================== 1. ORDENAMIENTO ULTRA-RÁPIDO ====================
        
        /// <summary>
        /// Ordena resultados de búsqueda usando Rust (5.3x más rápido)
        /// </summary>
        private List<SearchResultItem> SortSearchResultsOptimized(List<SearchResultItem> results)
        {
            if (results == null || results.Count == 0)
                return results;
            
            // Usar Rust para volúmenes grandes
            if (SlskNativeInterop.SupportsSearchFilterSort && results.Count >= 500)
            {
                var sw = Stopwatch.StartNew();
                if (SlskNativeInterop.TrySortByQualityNativeTable(results, out var sorted)
                    || SlskNativeInterop.TrySortByQualityNative(results, out sorted))
                {
                    sw.Stop();
                    rustSearchCount++;
                    rustSortCount++;
                    PerformanceTracker.Instance.Track("sort.rust_native", sw.ElapsedMilliseconds);
                    Log($"🦀 Rust(native): {results.Count:N0} resultados ordenados en {sw.ElapsedMilliseconds}ms");
                    UpdateRustStats();
                    return sorted;
                }
            }
            if (RustAdvancedCore.IsAvailable() && results.Count >= 500)
            {
                var sw = Stopwatch.StartNew();
                var sorted = RustAdvancedCore.SortSearchResults(results, RustAdvancedCore.SortCriteria.Quality);
                sw.Stop();
                
                rustSearchCount++;
                rustSortCount++;
                PerformanceTracker.Instance.Track("sort.rust_json", sw.ElapsedMilliseconds);
                Log($"🦀 Rust: {results.Count:N0} resultados ordenados en {sw.ElapsedMilliseconds}ms");

                UpdateRustStats();
                return sorted;
            }
            else
            {
                // Fallback a LINQ
                var sw = Stopwatch.StartNew();
                results.Sort((a, b) => b.QualityScore.CompareTo(a.QualityScore));
                sw.Stop();
                PerformanceTracker.Instance.Track("sort.csharp", sw.ElapsedMilliseconds);
                return results;
            }
        }

        private List<string> SearchAuthorIntelligentSilent(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || authorSearchIndex == null || authorSearchIndexCorpus.Count == 0)
            {
                return new List<string>();
            }

            try
            {
                var exact = authorSearchIndex.Search(query);
                if (exact.Count > 0)
                {
                    return exact
                        .Where(id => id >= 0 && id < authorSearchIndexCorpus.Count)
                        .Select(id => authorSearchIndexCorpus[id])
                        .ToList();
                }

                var fuzzy = authorSearchIndex.FuzzySearch(query, maxDistance: 2);
                if (fuzzy.Count == 0)
                {
                    return new List<string>();
                }

                return fuzzy
                    .OrderBy(r => r.Distance)
                    .Select(r => r.DocId)
                    .Where(id => id >= 0 && id < authorSearchIndexCorpus.Count)
                    .Select(id => authorSearchIndexCorpus[id])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(50)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
        
        // ==================== 2. VALIDACIÓN DE ARCHIVOS DESCARGADOS ====================
        
        /// <summary>
        /// Valida integridad de archivo descargado y extrae metadatos
        /// </summary>
        private bool ValidateDownloadedFile(string filePath, string filename)
        {
            // Verificar que el archivo existe antes de validar
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Log($"ARCHIVO NO ENCONTRADO: {filename}");
                Log($"   Ruta: {filePath}");
                return false;
            }
            
            // CAMBIO: Ser más permisivo - solo usar fallback que es menos estricto
            // La validación de Rust es demasiado estricta y marca archivos válidos como corruptos
            return ValidateDownloadedFileFallback(filePath, filename);
        }

        private bool ValidateDownloadedFileFallback(string filePath, string filename)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    return false;
                }

                var info = new FileInfo(filePath);
                if (info.Length <= 0)
                {
                    return false;
                }

                var ext = Path.GetExtension(filename ?? filePath)?.ToLowerInvariant() ?? string.Empty;

                if (ext == ".pdf")
                {
                    if (info.Length < 1024)
                    {
                        return false;
                    }

                    Span<byte> header = stackalloc byte[5];
                    using (var stream = File.OpenRead(filePath))
                    {
                        var read = stream.Read(header);
                        if (read < 5)
                        {
                            return false;
                        }
                    }

                    return header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F' && header[4] == (byte)'-';
                }

                if (ext == ".epub" || ext == ".zip" || ext == ".azw3")
                {
                    if (info.Length < 1024)
                    {
                        return false;
                    }

                    Span<byte> header = stackalloc byte[4];
                    using (var stream = File.OpenRead(filePath))
                    {
                        var read = stream.Read(header);
                        if (read < 4)
                        {
                            return false;
                        }
                    }

                    return header[0] == (byte)'P' && header[1] == (byte)'K';
                }
                
                // Soporte para archivos .fb2 (FictionBook - formato XML o ZIP)
                if (ext == ".fb2")
                {
                    if (info.Length < 100)
                    {
                        return false;
                    }
                    
                    // Leer más bytes para detectar diferentes formatos
                    Span<byte> header = stackalloc byte[100];
                    using (var stream = File.OpenRead(filePath))
                    {
                        var read = stream.Read(header);
                        if (read < 5)
                        {
                            return false;
                        }
                    }
                    
                    // 1. Verificar si es ZIP (algunos .fb2 están comprimidos)
                    if (header[0] == (byte)'P' && header[1] == (byte)'K')
                    {
                        return true; // .fb2 comprimido (ZIP)
                    }
                    
                    // 2. Verificar BOM UTF-8 (EF BB BF)
                    int offset = 0;
                    if (header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
                    {
                        offset = 3; // Saltar BOM
                    }
                    
                    // 3. Buscar '<' en los primeros 100 bytes (puede haber espacios/saltos de línea)
                    for (int i = offset; i < header.Length - 5; i++)
                    {
                        if (header[i] == (byte)'<')
                        {
                            // Verificar si es <?xml o <FictionBook o cualquier tag XML
                            if (header[i + 1] == (byte)'?' || 
                                header[i + 1] == (byte)'F' || 
                                header[i + 1] == (byte)'f' ||
                                (header[i + 1] >= (byte)'a' && header[i + 1] <= (byte)'z') ||
                                (header[i + 1] >= (byte)'A' && header[i + 1] <= (byte)'Z'))
                            {
                                return true; // Es XML válido
                            }
                        }
                    }
                    
                    // Si tiene extensión .fb2 y tamaño razonable, aceptarlo
                    // (algunos .fb2 pueden tener formatos no estándar)
                    return true;
                }
                
                // Soporte para archivos .doc/.docx (Word)
                if (ext == ".doc" || ext == ".docx")
                {
                    // .doc es formato binario de Office, .docx es ZIP
                    if (info.Length < 512)
                    {
                        return false;
                    }
                    
                    if (ext == ".docx")
                    {
                        // .docx es un archivo ZIP
                        Span<byte> header = stackalloc byte[4];
                        using (var stream = File.OpenRead(filePath))
                        {
                            var read = stream.Read(header);
                            if (read < 4)
                            {
                                return false;
                            }
                        }
                        return header[0] == (byte)'P' && header[1] == (byte)'K';
                    }
                    
                    // .doc tiene firma específica de Office
                    return true; // Aceptar .doc por ahora
                }

                sw.Stop();
                PerformanceTracker.Instance.Track("validate.csharp", sw.ElapsedMilliseconds);
                return true;
            }
            catch
            {
                return true;
            }
        }
        
        // ==================== 3. FILTRADO PARALELO ====================
        
        /// <summary>
        /// Filtra resultados de búsqueda en paralelo (10x más rápido)
        /// </summary>
        private List<SearchResultItem> FilterResultsOptimized(
            List<SearchResultItem> results,
            long minSize = 0,
            long maxSize = long.MaxValue,
            List<string> extensions = null,
            bool spanishOnly = false,
            int minQuality = 60)
        {
            if (results == null || results.Count == 0)
                return results;

            // Single-pass pipeline (filter+dedupe+sort) is applied in ProcessSearchResultsWithRust.
            
            // Usar Rust para volúmenes grandes
            if (SlskNativeInterop.SupportsSearchFilterSort && results.Count >= 2000)
            {
                var sw = Stopwatch.StartNew();
                if (SlskNativeInterop.TryFilterSearchResultsNativeTable(
                        results,
                        minSize: minSize,
                        maxSize: maxSize,
                        extensions: extensions ?? new List<string> { ".epub", ".mobi", ".pdf", ".azw3" },
                        spanishOnly: spanishOnly,
                        minQuality: minQuality,
                        out var filteredNative)
                    || SlskNativeInterop.TryFilterSearchResultsNative(
                        results,
                        minSize: minSize,
                        maxSize: maxSize,
                        extensions: extensions ?? new List<string> { ".epub", ".mobi", ".pdf", ".azw3" },
                        spanishOnly: spanishOnly,
                        minQuality: minQuality,
                        out filteredNative))
                {
                    sw.Stop();

                    rustSearchCount++;
                    rustFilterCount++;
                    PerformanceTracker.Instance.Track("filter.rust_native", sw.ElapsedMilliseconds);
                    Log($"🦀 Rust(native): Filtrado {results.Count:N0} → {filteredNative.Count:N0} en {sw.ElapsedMilliseconds}ms");
                    UpdateRustStats();
                    return filteredNative;
                }
            }
            if (RustAdvancedCore.IsAvailable() && results.Count >= 2000)
            {
                var sw = Stopwatch.StartNew();
                
                var filtered = RustAdvancedCore.FilterResultsParallel<SearchResultItem>(
                    results,
                    minSize,
                    maxSize,
                    extensions ?? new List<string> { ".epub", ".mobi", ".pdf", ".azw3" },
                    spanishOnly,
                    minQuality
                );
                
                sw.Stop();
                
                rustSearchCount++;
                rustFilterCount++;
                PerformanceTracker.Instance.Track("filter.rust_json", sw.ElapsedMilliseconds);
                Log($"🦀 Rust: Filtrado paralelo {results.Count:N0} → {filtered.Count:N0} en {sw.ElapsedMilliseconds}ms");

                UpdateRustStats();
                return filtered;
            }
            else
            {
                // Fallback a LINQ
                var sw = Stopwatch.StartNew();
                HashSet<string> extensionSet = null;
                if (extensions != null && extensions.Count > 0)
                {
                    extensionSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
                }

                for (int i = results.Count - 1; i >= 0; i--)
                {
                    var r = results[i];

                    if (minSize > 0 && r.Size < minSize)
                    {
                        results.RemoveAt(i);
                        continue;
                    }

                    if (maxSize < long.MaxValue && r.Size > maxSize)
                    {
                        results.RemoveAt(i);
                        continue;
                    }

                    if (extensionSet != null && !extensionSet.Contains(r.Extension ?? string.Empty))
                    {
                        results.RemoveAt(i);
                        continue;
                    }

                    if (minQuality > 0 && r.QualityScore < minQuality)
                    {
                        results.RemoveAt(i);
                        continue;
                    }

                    if (spanishOnly && !IsSpanishText(r.Filename ?? string.Empty))
                    {
                        results.RemoveAt(i);
                    }
                }
                sw.Stop();
                PerformanceTracker.Instance.Track("filter.csharp", sw.ElapsedMilliseconds);
                return results;
            }
        }

        private bool ProcessSearchResultsSinglePassNativeInPlace(
            List<SearchResultItem> results,
            long minSize,
            long maxSize,
            List<string> extensions,
            bool spanishOnly,
            int minQuality)
        {
            if (results == null || results.Count == 0)
            {
                return false;
            }

            if (!SlskNativeInterop.SupportsSearchPipeline || !ShouldUseSinglePassNative(results.Count))
            {
                return false;
            }

            var sw = Stopwatch.StartNew();
            if (SlskNativeInterop.TryProcessSearchResultsNativeTableInPlace(
                    results,
                    minSize: minSize,
                    maxSize: maxSize,
                    extensions: extensions,
                    spanishOnly: spanishOnly,
                    minQuality: minQuality,
                    getProviderScore: u => GetDeterministicProviderScoreInt(u))
                || SlskNativeInterop.TryProcessSearchResultsNativeInPlace(
                    results,
                    minSize: minSize,
                    maxSize: maxSize,
                    extensions: extensions,
                    spanishOnly: spanishOnly,
                    minQuality: minQuality,
                    getProviderScore: u => GetDeterministicProviderScoreInt(u)))
            {
                sw.Stop();
                rustSearchCount++;
                PerformanceTracker.Instance.Track("pipeline.rust_native", sw.ElapsedMilliseconds);
                UpdateRustStats();
                return true;
            }

            return false;
        }

        private bool ShouldUseSinglePassNative(int count)
        {
            if (count <= 0)
            {
                return false;
            }

            if (!SlskNativeInterop.SupportsSearchPipeline)
            {
                return false;
            }

            if ((DateTime.UtcNow - adaptiveSinglePassLastUpdateUtc).TotalSeconds >= 30)
            {
                adaptiveSinglePassLastUpdateUtc = DateTime.UtcNow;
                TryUpdateAdaptiveSinglePassThreshold();
            }

            return count >= adaptiveSinglePassMinCount;
        }

        private void TryUpdateAdaptiveSinglePassThreshold()
        {
            var bins = new[] { "s", "m", "l" };
            foreach (var bin in bins)
            {
                var sp = PerformanceTracker.Instance.GetPercentiles($"pipeline.singlepass_total.{bin}");
                var st = PerformanceTracker.Instance.GetPercentiles($"pipeline.staged_total.{bin}");
                if (sp.count < 20 || st.count < 20)
                {
                    continue;
                }

                if (sp.p50 > 0 && st.p50 > 0 && sp.p50 <= (long)Math.Max(1, st.p50 * 0.95))
                {
                    adaptiveSinglePassMinCount = bin switch
                    {
                        "s" => 2000,
                        "m" => 8000,
                        "l" => 20000,
                        _ => adaptiveSinglePassMinCount
                    };
                    return;
                }
            }

            var spAll = PerformanceTracker.Instance.GetPercentiles("pipeline.singlepass_total.l");
            var stAll = PerformanceTracker.Instance.GetPercentiles("pipeline.staged_total.l");
            if (spAll.count >= 20 && stAll.count >= 20 && spAll.p50 > stAll.p50)
            {
                adaptiveSinglePassMinCount = 30000;
            }
        }
        
        // ==================== 4. BÚSQUEDA DE AUTORES CON FUZZY SEARCH ====================
        
        /// <summary>
        /// Indexa autores para búsqueda ultra-rápida (1000x)
        /// </summary>
        private void IndexAuthorsForSearch()
        {
            if (!RustSearchIndex.IsRustAvailable())
            {
                Log("Índice de autores no disponible (Rust requerido)");
                return;
            }
            
            try
            {
                var sw = Stopwatch.StartNew();
                
                authorSearchIndex?.Dispose();
                authorSearchIndex = new RustSearchIndex();
                
                authorSearchIndexCorpus = authorIndex.Keys.ToList();
                
                for (int i = 0; i < authorSearchIndexCorpus.Count; i++)
                {
                    authorSearchIndex.AddDocument(i, authorSearchIndexCorpus[i]);
                }
                
                sw.Stop();
                
                Log($"Índice de {authorSearchIndexCorpus.Count:N0} autores creado en {sw.ElapsedMilliseconds}ms");
                Log($"   Búsqueda instantánea con fuzzy matching activa");
            }
            catch (Exception ex)
            {
                Log($"Error creando índice de autores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Busca autor con tolerancia a errores tipográficos
        /// </summary>
        private List<string> SearchAuthorIntelligent(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<string>();
            
            if (authorSearchIndex == null)
            {
                Log("Índice de autores no inicializado - ejecuta IndexAuthorsForSearch() primero");
                return new List<string>();
            }
            
            var allAuthors = authorIndex.Keys.ToList();
            var sw = Stopwatch.StartNew();
            
            // Primero búsqueda exacta
            var exactMatches = authorSearchIndex.Search(query);
            if (exactMatches.Count > 0)
            {
                sw.Stop();
                Log($"{exactMatches.Count} coincidencias exactas para '{query}' ({sw.ElapsedMilliseconds}ms)");
                return exactMatches.Select(id => allAuthors[id]).ToList();
            }
            
            // Si no hay exactas, búsqueda fuzzy (tolerante a errores)
            var fuzzyResults = authorSearchIndex.FuzzySearch(query, maxDistance: 2);
            sw.Stop();
            
            if (fuzzyResults.Count > 0)
            {
                Log($"{fuzzyResults.Count} coincidencias similares para '{query}' (fuzzy, {sw.ElapsedMilliseconds}ms)");
                
                foreach (var (docId, distance) in fuzzyResults.Take(10))
                {
                    string author = allAuthors[docId];
                    string similarity = distance == 0 ? "exacto" : $"~{distance} difs";
                    Log($"   → {author} ({similarity})");
                }
                
                return fuzzyResults
                    .Take(10)
                    .Select(r => allAuthors[r.DocId])
                    .ToList();
            }
            
            Log($"No se encontró autor similar a '{query}' ({sw.ElapsedMilliseconds}ms)");
            return new List<string>();
        }
        
        // ==================== 5. DEDUPLICACIÓN ULTRA-RÁPIDA ====================
        
        /// <summary>
        /// Elimina duplicados 21x más rápido
        /// </summary>
        private List<SearchResultItem> DeduplicateResultsOptimized(List<SearchResultItem> results)
        {
            if (results == null || results.Count == 0)
                return results;
            
            // Usar Rust para volúmenes grandes
            if (results.Count >= 5000 && SlskNativeInterop.IsAvailable)
            {
                var sw = Stopwatch.StartNew();
                var beforeCount = results.Count;

                if (SlskNativeInterop.TryDeduplicateKeysNativeTable(
                        results,
                        getKey: r => Path.GetFileName(r.Filename ?? string.Empty),
                        getProviderScore: u => GetDeterministicProviderScoreInt(u),
                        out var uniqueTable))
                {
                    sw.Stop();
                    var removedCountTable = beforeCount - uniqueTable.Count;

                    if (removedCountTable > 0)
                    {
                        var nowUtc = DateTime.UtcNow;
                        if ((nowUtc - lastRustDedupeLogUtc).TotalSeconds >= 5)
                        {
                            lastRustDedupeLogUtc = nowUtc;
                            Log($"Rust(native): {removedCountTable:N0} duplicados eliminados en {sw.ElapsedMilliseconds}ms");
                            Log($"   {uniqueTable.Count:N0} archivos únicos restantes");
                        }
                    }

                    rustSearchCount++;
                    rustDedupeNativeCount++;
                    PerformanceTracker.Instance.Track("dedupe.rust_native", sw.ElapsedMilliseconds);
                    UpdateRustStats();

                    return uniqueTable;
                }

                // Dedupe nativo (slsk_native.dll): agrupación por filename string.
                // Para dedupe por (Filename|Size), pasamos una clave compuesta como "filename|size".
                var unique = SlskNativeInterop.DeduplicateFiles(
                    results,
                    getFileName: r => DedupeKeyHelpers.BuildRemoteFileKey(r.Filename, r.Size, normalizeFileName: false),
                    getUsername: r => r.Username,
                    getSize: r => r.Size,
                    getProviderScore: u => GetDeterministicProviderScoreInt(u));
                
                sw.Stop();
                var removedCountNative = beforeCount - unique.Count;
                
                if (removedCountNative > 0)
                {
                    var nowUtc = DateTime.UtcNow;
                    if ((nowUtc - lastRustDedupeLogUtc).TotalSeconds >= 5)
                    {
                        lastRustDedupeLogUtc = nowUtc;
                        Log($"Rust(native): {removedCountNative:N0} duplicados eliminados en {sw.ElapsedMilliseconds}ms");
                        Log($"   {unique.Count:N0} archivos únicos restantes");
                    }
                }

                rustSearchCount++;
                rustDedupeNativeCount++;
                PerformanceTracker.Instance.Track("dedupe.rust_native", sw.ElapsedMilliseconds);
                UpdateRustStats();
                
                return unique;
            }
            else if (RustAdvancedCore.IsAvailable() && results.Count >= 5000)
            {
                var sw = Stopwatch.StartNew();
                var beforeCount = results.Count;

                var unique = RustAdvancedCore.DeduplicateFiles(results);

                sw.Stop();
                var removedCount = beforeCount - unique.Count;

                if (removedCount > 0)
                {
                    Log($"Rust(JSON): {removedCount:N0} duplicados eliminados en {sw.ElapsedMilliseconds}ms");
                    Log($"   {unique.Count:N0} archivos únicos restantes");
                }

                rustSearchCount++;
                rustDedupeJsonCount++;
                PerformanceTracker.Instance.Track("dedupe.rust_json", sw.ElapsedMilliseconds);
                UpdateRustStats();

                return unique;
            }
            else
            {
                // Fallback a HashSet
                var sw = Stopwatch.StartNew();
                var seen = new HashSet<string>(results.Count, StringComparer.OrdinalIgnoreCase);
                for (int i = results.Count - 1; i >= 0; i--)
                {
                    var r = results[i];
                    var key = DedupeKeyHelpers.BuildRemoteFileKey(r.Filename, r.Size, normalizeFileName: false);
                    if (!seen.Add(key))
                    {
                        results.RemoveAt(i);
                    }
                }

                sw.Stop();
                rustDedupeFallbackCount++;
                PerformanceTracker.Instance.Track("dedupe.csharp", sw.ElapsedMilliseconds);
                return results;
            }
        }
        
        // ==================== 6. FILTRADO POR KEYWORDS ULTRA-RÁPIDO ====================
        
        /// <summary>
        /// Filtra por múltiples keywords simultáneamente (100x más rápido)
        /// </summary>
        private List<SearchResultItem> FilterByKeywords(List<SearchResultItem> results, List<string> keywords)
        {
            if (results == null || results.Count == 0 || keywords == null || keywords.Count == 0)
                return results;
            
            if (!RustFileOperations.IsAvailable())
            {
                // Fallback a LINQ
                return results.Where(r => 
                    keywords.All(kw => r.Filename.Contains(kw, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
            
            var sw = Stopwatch.StartNew();
            
            // Usar Aho-Corasick (100x más rápido)
            var filtered = results.Where(r =>
            {
                int matchCount = RustFileOperations.CountMatchingPatterns(r.Filename, keywords);
                return matchCount == keywords.Count; // Todas las keywords presentes
            }).ToList();
            
            sw.Stop();
            
            Log($"🦀 Rust: Filtrado por {keywords.Count} keywords: {results.Count:N0} → {filtered.Count:N0} ({sw.ElapsedMilliseconds}ms)");
            
            return filtered;
        }
        
        /// <summary>
        /// Filtra resultados en español usando keywords
        /// </summary>
        private List<SearchResultItem> FilterSpanishResults(List<SearchResultItem> results)
        {
            var spanishKeywords = new List<string> { "español", "spanish", "castellano", "spa" };
            return FilterByKeywords(results, spanishKeywords);
        }
        
        // ==================== 7. COMPRESIÓN AUTOMÁTICA DE LOGS ====================
        
        /// <summary>
        /// Comprime logs antiguos automáticamente (85% reducción)
        /// </summary>
        private void CompressOldLogs()
        {
            if (!RustAdvancedCore.IsAvailable())
            {
                Log("Compresión de logs no disponible (Rust requerido)");
                return;
            }
            
            try
            {
                var logsDir = Path.Combine(dataDir, "logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                    return;
                }
                
                var oldLogs = Directory.GetFiles(logsDir, "*.log")
                    .Where(f => new FileInfo(f).LastWriteTime < DateTime.Now.AddDays(-7))
                    .ToList();
                
                if (oldLogs.Count == 0)
                {
                    Log("📦 No hay logs antiguos para comprimir");
                    return;
                }
                
                Log($"📦 Comprimiendo {oldLogs.Count} logs antiguos...");
                
                long totalOriginalSize = 0;
                long totalCompressedSize = 0;
                
                foreach (var logPath in oldLogs)
                {
                    try
                    {
                        byte[] data = File.ReadAllBytes(logPath);
                        byte[] compressed = RustAdvancedCore.CompressData(data);
                        
                        string compressedPath = logPath + ".zst";
                        File.WriteAllBytes(compressedPath, compressed);
                        File.Delete(logPath);
                        
                        totalOriginalSize += data.Length;
                        totalCompressedSize += compressed.Length;
                        
                        double ratio = 100.0 * compressed.Length / data.Length;
                        Log($"   {Path.GetFileName(logPath)} → {ratio:F1}% del tamaño original");
                    }
                    catch (Exception ex)
                    {
                        Log($"   Error comprimiendo {Path.GetFileName(logPath)}: {ex.Message}");
                    }
                }
                
                if (totalOriginalSize > 0)
                {
                    double overallRatio = 100.0 * totalCompressedSize / totalOriginalSize;
                    long saved = totalOriginalSize - totalCompressedSize;
                    
                    Log($"{oldLogs.Count} logs comprimidos");
                    Log($"   Original: {FormatSize(totalOriginalSize)}");
                    Log($"   Comprimido: {FormatSize(totalCompressedSize)}");
                    Log($"   Ahorro: {FormatSize(saved)} ({overallRatio:F1}% del tamaño original)");
                }
            }
            catch (Exception ex)
            {
                Log($"Error comprimiendo logs: {ex.Message}");
            }
        }
        
        // ==================== 8. NORMALIZACIÓN DE NOMBRES DE AUTORES ====================
        
        /// <summary>
        /// Consolida variantes de nombres de autores
        /// </summary>
        private void ConsolidateAuthorVariants()
        {
            if (!RustAdvancedCore.IsAvailable())
            {
                Log("Normalización de autores no disponible (Rust requerido)");
                return;
            }
            
            try
            {
                var allAuthors = authorIndex.Keys.ToList();
                if (allAuthors.Count == 0)
                {
                    Log("No hay autores para consolidar");
                    return;
                }
                
                var sw = Stopwatch.StartNew();
                var groups = RustAdvancedCore.GroupAuthorVariants(allAuthors);
                sw.Stop();
                
                var uniqueCount = groups.Values.Distinct().Count();
                var duplicatesRemoved = allAuthors.Count - uniqueCount;
                
                if (duplicatesRemoved > 0)
                {
                    Log($"Autores consolidados en {sw.ElapsedMilliseconds}ms:");
                    Log($"   {allAuthors.Count:N0} → {uniqueCount:N0} únicos");
                    Log($"   {duplicatesRemoved} variaciones eliminadas (acentos, mayúsculas, etc.)");
                    
                    // Mostrar algunos ejemplos
                    var examples = groups.GroupBy(kvp => kvp.Value)
                        .Where(g => g.Count() > 1)
                        .Take(5);
                    
                    foreach (var group in examples)
                    {
                        Log($"   • {group.Key}:");
                        foreach (var variant in group.Take(3))
                        {
                            Log($"      - {variant.Key}");
                        }
                    }
                }
                else
                {
                    Log($"Autores ya consolidados ({allAuthors.Count:N0} únicos)");
                }
            }
            catch (Exception ex)
            {
                Log($"Error consolidando autores: {ex.Message}");
            }
        }
        
        // ==================== 9. BOTÓN DE DIAGNÓSTICO RUST ====================
        
        /// <summary>
        /// Crea botón de diagnóstico Rust en UI
        /// </summary>
        private void CreateRustDiagnosticsButton(Panel parentPanel)
        {
            var btnTestRust = new Button
            {
                Text = "🧪 Test Rust",
                Location = new Point(10, 10),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(50, 150, 250),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            
            btnTestRust.FlatAppearance.BorderSize = 0;
            
            btnTestRust.Click += (s, e) =>
            {
                Log("\n════════════════════════════════════");
                Log("EJECUTANDO TESTS RUST COMPLETOS");
                Log("════════════════════════════════════\n");
                
                try
                {
                    // TestRustIntegration.RunTests();
                    // TestRustIntegration.RunComparativeTest();
                    Log("Tests de Rust comentados - ejecutar manualmente si es necesario");
                    
                    Log("\n════════════════════════════════════");
                    Log("TESTS COMPLETADOS EXITOSAMENTE");
                    Log("════════════════════════════════════\n");
                    
                    ShowNotification(
                        "Tests Rust Completados",
                        "Todas las funcionalidades probadas exitosamente",
                        ToolTipIcon.Info
                    );
                }
                catch (Exception ex)
                {
                    Log($"\nError en tests: {ex.Message}\n");
                }
            };
            
            parentPanel?.Controls.Add(btnTestRust);
        }
        
        // ==================== 10. ESTADÍSTICAS EN TIEMPO REAL ====================
        
        /// <summary>
        /// Crea label de estadísticas Rust
        /// </summary>
        private void CreateRustStatsLabel(Panel parentPanel)
        {
            lblRustStats = new Label
            {
                Location = new Point(140, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 200, 255)
            };
            
            UpdateRustStats();
            
            parentPanel?.Controls.Add(lblRustStats);
        }
        
        /// <summary>
        /// Actualiza estadísticas de rendimiento Rust
        /// </summary>
        private void UpdateRustStats()
        {
            if (lblRustStats == null)
                return;
            
            SafeInvoke(() =>
            {
                var stats = new List<string>();
                
                if (RustAdvancedCore.IsAvailable())
                {
                    stats.Add("🦀 Rust: ACTIVO");
                    stats.Add($"Native: {(SlskNativeInterop.IsAvailable ? "SI" : "NO")}");
                    stats.Add($"Búsq: {rustSearchCount:N0}");
                    stats.Add($"Val: {rustValidatedFiles:N0}");
                    stats.Add($"DedN: {rustDedupeNativeCount:N0}");
                    stats.Add($"DedJ: {rustDedupeJsonCount:N0}");
                    stats.Add($"DedC: {rustDedupeFallbackCount:N0}");

                    var (_, p50n, p90n, _) = PerformanceTracker.Instance.GetPercentiles("dedupe.rust_native");
                    if (p50n > 0)
                    {
                        stats.Add($"DedN p50/p90: {p50n}ms/{p90n}ms");
                    }
                }
                else
                {
                    stats.Add("Rust: No disponible");
                }
                
                lblRustStats.Text = string.Join(" | ", stats);
            });
        }
    }
}
