using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SlskDownBibliotecaImport;

public static class BibliotecaImportOptions
{
    public static HashSet<string> BuildAllowedImportExtensions(
        bool wantEpub, bool wantMobi, bool wantPdf, bool wantFb2, bool wantAzw3, bool wantDjvu, bool wantTxt)
    {
        var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (wantEpub) { allowedExts.Add(".epub"); }
        if (wantMobi) { allowedExts.Add(".mobi"); allowedExts.Add(".prc"); }
        if (wantPdf) { allowedExts.Add(".pdf"); }
        if (wantFb2) { allowedExts.Add(".fb2"); }
        if (wantAzw3) { allowedExts.Add(".azw3"); allowedExts.Add(".azw"); }
        if (wantDjvu) { allowedExts.Add(".djvu"); allowedExts.Add(".djv"); }
        if (wantTxt) { allowedExts.Add(".txt"); allowedExts.Add(".text"); }
        if (allowedExts.Count > 0)
        {
            allowedExts.Add(".doc"); allowedExts.Add(".docx"); allowedExts.Add(".rtf");
            allowedExts.Add(".odt"); allowedExts.Add(".lit"); allowedExts.Add(".lrf");
            allowedExts.Add(".html"); allowedExts.Add(".htm"); allowedExts.Add(".xhtml");
        }
        return allowedExts;
    }
}

public static class BibliotecaImportFilesystem
{
    private static readonly Encoding s_latin1 = Encoding.Latin1;

    private static readonly Regex s_rxRarMultiVol =
        new(@"\.part[2-9]\d*\.rar$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex s_rxImportAuthorCommaFmt =
        new(@"^([^,]+),\s*(.+?)\s*(-\s*.+)$", RegexOptions.Compiled);

    public static bool IsWithinHourWindow(int hour, int startHour, int endHour)
    {
        startHour = Math.Clamp(startHour, 0, 23);
        endHour = Math.Clamp(endHour, 0, 23);
        if (startHour == endHour) return true;
        return startHour < endHour
            ? hour >= startHour && hour < endHour
            : hour >= startHour || hour < endHour;
    }

    public static int ImportFormatPriority(string destFileName)
    {
        var ext = Path.GetExtension(destFileName).ToLowerInvariant();
        return ext switch
        {
            ".epub" => 0,
            ".fb2" => 1,
            ".azw3" or ".mobi" or ".prc" => 2,
            ".pdf" => 3,
            ".docx" or ".doc" or ".rtf" or ".odt" => 4,
            ".txt" or ".text" => 5,
            _ => 6
        };
    }

    public static string GetSourceTopFolder(string srcDir, string sourcePath)
    {
        try
        {
            var rel = Path.GetRelativePath(srcDir, sourcePath);
            if (string.IsNullOrWhiteSpace(rel)) return "(root)";
            var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Length > 1 ? parts[0] : "(root)";
        }
        catch { return "(unknown)"; }
    }

    /// <summary>Clave estable por origen (no solo nombre destino) para mapas de hash en memoria.</summary>
    public static string CandidateContentDedupKey(ImportCandidate c)
    {
        if (c.IsArchived)
            return "A\x1F" + c.ArchivePath + "\x1F" + c.EntryName;
        return "F\x1F" + c.FilePath;
    }

    /// <summary>Hash rápido no criptográfico del contenido (XxHash64), hex mayúsculas.</summary>
    public static string ComputeStreamContentHashHex(Stream stream)
    {
        var h = new XxHash64();
        Span<byte> buf = stackalloc byte[65536];
        int n;
        while ((n = stream.Read(buf)) > 0)
            h.Append(buf[..n]);
        return Convert.ToHexString(h.GetHashAndReset());
    }

    public static string? TryComputeCandidateHash(ImportCandidate c)
    {
        try
        {
            if (c.IsArchived)
            {
                var archExt = Path.GetExtension(c.ArchivePath);
                if (archExt.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var zip = ZipFile.OpenRead(c.ArchivePath);
                    var e = zip.Entries.FirstOrDefault(x => string.Equals(x.FullName, c.EntryName, StringComparison.OrdinalIgnoreCase));
                    using var s = e?.Open();
                    return s != null ? ComputeStreamContentHashHex(s) : null;
                }
                using (var stream = File.OpenRead(c.ArchivePath))
                using (var reader = ReaderFactory.OpenReader(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory && string.Equals(reader.Entry.Key, c.EntryName, StringComparison.OrdinalIgnoreCase))
                        {
                            using var rs = reader.OpenEntryStream();
                            return ComputeStreamContentHashHex(rs);
                        }
                    }
                }
                return null;
            }
            using var fs = File.OpenRead(c.FilePath);
            return ComputeStreamContentHashHex(fs);
        }
        catch { return null; }
    }

    public static void ExtractFromArchive(ImportCandidate candidate, string destFile)
    {
        var archExt = Path.GetExtension(candidate.ArchivePath);
        if (archExt.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(candidate.ArchivePath);
            var entry = zip.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName, candidate.EntryName, StringComparison.OrdinalIgnoreCase));
            if (entry == null) throw new InvalidOperationException($"Entrada no encontrada: {candidate.EntryName}");
            using var src = entry.Open();
            using var dst = File.Create(destFile);
            src.CopyTo(dst);
        }
        else
        {
            using var stream = File.OpenRead(candidate.ArchivePath);
            using var reader = ReaderFactory.OpenReader(stream);
            bool found = false;
            while (reader.MoveToNextEntry())
            {
                if (!reader.Entry.IsDirectory && string.Equals(reader.Entry.Key, candidate.EntryName, StringComparison.OrdinalIgnoreCase))
                {
                    using var src = reader.OpenEntryStream();
                    using var dst = File.Create(destFile);
                    src.CopyTo(dst);
                    found = true;
                    break;
                }
            }
            if (!found) throw new InvalidOperationException($"Entrada no encontrada: {candidate.EntryName}");
        }
    }

    public static HashSet<string> LoadImportCheckpoint(string path)
    {
        try
        {
            if (!File.Exists(path)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            var list = JsonSerializer.Deserialize<List<string>>(fs);
            return list != null
                ? new HashSet<string>(list, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
    }

    public static void SaveImportCheckpoint(string path, HashSet<string> done)
    {
        try
        {
            var tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
                JsonSerializer.Serialize(fs, done);
            File.Move(tmp, path, overwrite: true);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        }
        catch { }
    }

    public static void AppendImportCheckpointDelta(string deltaPath, string fileName)
    {
        try
        {
            File.AppendAllText(deltaPath, fileName + Environment.NewLine, Encoding.UTF8);
            if (!File.GetAttributes(deltaPath).HasFlag(FileAttributes.Hidden))
                File.SetAttributes(deltaPath, File.GetAttributes(deltaPath) | FileAttributes.Hidden);
        }
        catch { }
    }

    /// <summary>
    /// Fusiona líneas del delta en el JSON principal y vacía el delta. Llamar bajo el mismo bloqueo que <see cref="AppendImportCheckpointDelta"/>.
    /// </summary>
    public static void MergeImportCheckpointDeltaIntoMain(string jsonPath, string deltaPath)
    {
        try
        {
            if (!File.Exists(deltaPath)) return;
            if (new FileInfo(deltaPath).Length == 0) return;
            var done = LoadImportCheckpoint(jsonPath);
            foreach (var line in File.ReadLines(deltaPath))
            {
                var s = line.Trim();
                if (!string.IsNullOrEmpty(s)) done.Add(s);
            }
            SaveImportCheckpoint(jsonPath, done);
            using (var fs = new FileStream(deltaPath, FileMode.Create, FileAccess.Write, FileShare.None))
            { }
            File.SetAttributes(deltaPath, File.GetAttributes(deltaPath) | FileAttributes.Hidden);
        }
        catch { }
    }

    public static HashSet<string> LoadImportCheckpointWithDelta(string jsonPath, string deltaPath)
    {
        var done = LoadImportCheckpoint(jsonPath);
        try
        {
            if (File.Exists(deltaPath))
            {
                foreach (var line in File.ReadLines(deltaPath))
                {
                    var s = line.Trim();
                    if (!string.IsNullOrEmpty(s)) done.Add(s);
                }
            }
        }
        catch { }
        return done;
    }

    public static void DeleteImportCheckpoint(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public static string BuildQuickImportSignature(string destFileName, long sizeBytes)
    {
        var stem = Path.GetFileNameWithoutExtension(destFileName).Trim().ToLowerInvariant();
        return $"{sizeBytes}:{stem}";
    }

    /// <summary>Mismo <see cref="ImportCandidate.DestFileName"/> (sin distinguir mayúsculas): conserva el primero en el orden actual.</summary>
    public static int DeduplicateCandidatesByDestFileName(List<ImportCandidate> candidates)
    {
        if (candidates.Count <= 1) return 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int w = 0;
        int dropped = 0;
        for (int r = 0; r < candidates.Count; r++)
        {
            var c = candidates[r];
            if (!seen.Add(c.DestFileName))
            {
                dropped++;
                continue;
            }
            if (w != r) candidates[w] = c;
            w++;
        }
        if (w < candidates.Count)
            candidates.RemoveRange(w, candidates.Count - w);
        return dropped;
    }

    /// <summary>
    /// Ordena por prioridad de formato y quita candidatos con la misma firma rápida (tamaño + título).
    /// Entradas con <c>SizeBytes == 0</c> no se fusionan por firma.
    /// </summary>
    public static int DeduplicateCandidatesByQuickSignature(List<ImportCandidate> candidates)
    {
        if (candidates.Count <= 1) return 0;
        candidates.Sort(static (a, b) =>
        {
            int pa = ImportFormatPriority(a.DestFileName);
            int pb = ImportFormatPriority(b.DestFileName);
            int cmp = pa.CompareTo(pb);
            if (cmp != 0) return cmp;
            return string.Compare(a.DestFileName, b.DestFileName, StringComparison.OrdinalIgnoreCase);
        });
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int w = 0;
        int dropped = 0;
        for (int r = 0; r < candidates.Count; r++)
        {
            var c = candidates[r];
            if (c.SizeBytes <= 0)
            {
                if (w != r) candidates[w] = c;
                w++;
                continue;
            }
            var sig = BuildQuickImportSignature(c.DestFileName, c.SizeBytes);
            if (!seen.Add(sig))
            {
                dropped++;
                continue;
            }
            if (w != r) candidates[w] = c;
            w++;
        }
        if (w < candidates.Count)
            candidates.RemoveRange(w, candidates.Count - w);
        return dropped;
    }

    private sealed class ImportScanCacheData
    {
        public string SrcDir { get; set; } = "";
        public long SrcFingerprint { get; set; }
        public bool WantArchive { get; set; }
        public long MinBytes { get; set; }
        public string[] AllowedExts { get; set; } = [];
        public int RarMultiVolume { get; set; }
        public int ZipCorrupted { get; set; }
        public int BelowMinSize { get; set; }
        public List<ImportCandidateDto> Candidates { get; set; } = [];
    }

    private sealed class ImportCandidateDto
    {
        public string FilePath { get; set; } = "";
        public string ArchivePath { get; set; } = "";
        public string EntryName { get; set; } = "";
        public string DestFileName { get; set; } = "";
        public long SizeBytes { get; set; }
        public bool IsArchived { get; set; }
    }

    private static long ComputeSrcFingerprint(string srcDir)
    {
        long max = 0;
        long count = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(srcDir))
            {
                count++;
                try { var t = new FileInfo(f).LastWriteTimeUtc.Ticks; if (t > max) max = t; } catch { }
            }
            foreach (var sub in Directory.EnumerateDirectories(srcDir))
            {
                count++;
                try { var dt = new DirectoryInfo(sub).LastWriteTimeUtc.Ticks; if (dt > max) max = dt; } catch { }
                try
                {
                    foreach (var f in Directory.EnumerateFiles(sub))
                    {
                        count++;
                        try { var t = new FileInfo(f).LastWriteTimeUtc.Ticks; if (t > max) max = t; } catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return max ^ unchecked(count * -7046029254386353131L);
    }

    public static ImportScanResult? LoadImportScanCache(
        string cachePath, string srcDir,
        HashSet<string> allowedExts,
        bool wantArchive, long minBytes, bool warmCache)
    {
        try
        {
            if (!warmCache) return null;
            if (!File.Exists(cachePath)) return null;
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) > TimeSpan.FromHours(12)) return null;
            using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            var data = JsonSerializer.Deserialize<ImportScanCacheData>(fs);
            if (data == null) return null;
            if (!string.Equals(data.SrcDir, srcDir, StringComparison.OrdinalIgnoreCase)) return null;
            if (data.WantArchive != wantArchive || data.MinBytes != minBytes) return null;
            var cachedExts = new HashSet<string>(data.AllowedExts, StringComparer.OrdinalIgnoreCase);
            if (!cachedExts.SetEquals(allowedExts)) return null;
            var fp = ComputeSrcFingerprint(srcDir);
            if (fp != data.SrcFingerprint) return null;
            var candidates = data.Candidates.Select(d => new ImportCandidate
            {
                FilePath = d.FilePath,
                ArchivePath = d.ArchivePath,
                EntryName = d.EntryName,
                DestFileName = d.DestFileName,
                SizeBytes = d.SizeBytes,
            }).ToList();
            return new ImportScanResult
            {
                Candidates = candidates,
                RarMultiVolume = data.RarMultiVolume,
                ZipCorrupted = data.ZipCorrupted,
                BelowMinSize = data.BelowMinSize,
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch { return null; }
    }

    public static void SaveImportScanCache(
        string cachePath, string srcDir,
        HashSet<string> allowedExts,
        bool wantArchive, long minBytes, ImportScanResult scan)
    {
        try
        {
            var data = new ImportScanCacheData
            {
                SrcDir = srcDir,
                SrcFingerprint = ComputeSrcFingerprint(srcDir),
                WantArchive = wantArchive,
                MinBytes = minBytes,
                AllowedExts = allowedExts.ToArray(),
                RarMultiVolume = scan.RarMultiVolume,
                ZipCorrupted = scan.ZipCorrupted,
                BelowMinSize = scan.BelowMinSize,
                Candidates = scan.Candidates.Select(c => new ImportCandidateDto
                {
                    FilePath = c.FilePath,
                    ArchivePath = c.ArchivePath,
                    EntryName = c.EntryName,
                    DestFileName = c.DestFileName,
                    SizeBytes = c.SizeBytes,
                    IsArchived = c.IsArchived,
                }).ToList(),
            };
            var tmp = cachePath + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
                JsonSerializer.Serialize(fs, data);
            File.Move(tmp, cachePath, overwrite: true);
            File.SetAttributes(cachePath, File.GetAttributes(cachePath) | FileAttributes.Hidden);
        }
        catch { }
    }

    public static string ValidateAndCorrectExtension(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return filePath;
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var filename = Path.GetFileName(filePath);
            
            bool hasDoubleExt = false;
            var baseNoExt = Path.GetFileNameWithoutExtension(filename);
            var prevExt = Path.GetExtension(baseNoExt).ToLowerInvariant();
            if (prevExt == ".epub" || prevExt == ".mobi" || prevExt == ".pdf" || prevExt == ".azw3" || prevExt == ".fb2")
            {
                hasDoubleExt = true;
            }

            if (hasDoubleExt || ext == ".zip" || ext == ".rar")
            {
                Span<byte> header = stackalloc byte[8];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8, false))
                {
                    int read = fs.Read(header);
                    if (read < 4) return filePath;
                }

                if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                {
                    try
                    {
                        using var fs = File.OpenRead(filePath);
                        using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
                        var mt = zip.GetEntry("mimetype");
                        if (mt != null)
                        {
                            using var s = mt.Open();
                            using var sr = new StreamReader(s, Encoding.ASCII);
                            var content = sr.ReadToEnd().Trim();
                            if (content == "application/epub+zip")
                            {
                                return Path.ChangeExtension(filePath, ".epub");
                            }
                        }
                    }
                    catch {}
                    return Path.ChangeExtension(filePath, ".zip");
                }

                if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                    return Path.ChangeExtension(filePath, ".pdf");

                try
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (fs.Length >= 68)
                    {
                        fs.Seek(60, SeekOrigin.Begin);
                        byte[] mobi = new byte[8];
                        if (fs.Read(mobi, 0, 8) == 8 &&
                            mobi[0] == 'B' && mobi[1] == 'O' && mobi[2] == 'O' && mobi[3] == 'K' &&
                            mobi[4] == 'M' && mobi[5] == 'O' && mobi[6] == 'B' && mobi[7] == 'I')
                            return Path.ChangeExtension(filePath, ".mobi");
                    }
                }
                catch {}
            }
        }
        catch {}
        return filePath;
    }

    public static string TryRenameFileWithCorrectedExtension(string filePath)
    {
        var correctedPath = ValidateAndCorrectExtension(filePath);
        if (!string.Equals(filePath, correctedPath, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (File.Exists(correctedPath))
                {
                    File.Delete(filePath);
                    return correctedPath;
                }
                File.Move(filePath, correctedPath);
                return correctedPath;
            }
            catch { }
        }
        return filePath;
    }

    private static void EnumerateArchiveRecursively(
        string rootArchivePath,
        string currentEntryPath,
        Stream archiveStream,
        int depth,
        ConcurrentBag<ImportCandidate> results,
        HashSet<string> allowedExts,
        long minBytes,
        ref int belowMin,
        ref int zipCorrupted,
        ref int rarMultiVol)
    {
        if (depth > 3) return;

        try
        {
            using var ms = new MemoryStream();
            archiveStream.CopyTo(ms);
            ms.Position = 0;

            if (ms.Length < 4) return;
            byte[] header = new byte[4];
            ms.Read(header, 0, 4);
            ms.Position = 0;

            bool isZip = header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
            bool isRar = header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 && header[3] == 0x21;

            if (isZip)
            {
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
                foreach (var entry in zip.Entries)
                {
                    if (entry.Length == 0) continue;
                    var entryExt = Path.GetExtension(entry.Name).ToLowerInvariant();
                    var combinedEntryPath = string.IsNullOrEmpty(currentEntryPath)
                        ? entry.FullName
                        : currentEntryPath + "::" + entry.FullName;

                    if (entryExt == ".zip" || entryExt == ".rar")
                    {
                        using var entryStream = entry.Open();
                        EnumerateArchiveRecursively(rootArchivePath, combinedEntryPath, entryStream, depth + 1, results, allowedExts, minBytes, ref belowMin, ref zipCorrupted, ref rarMultiVol);
                        continue;
                    }

                    if (allowedExts.Contains(entryExt))
                    {
                        if (minBytes > 0 && entry.Length < minBytes)
                        {
                            Interlocked.Increment(ref belowMin);
                            continue;
                        }
                        var destName = NormalizeImportAuthorName(BuildArchiveDestName(rootArchivePath, entry.Name, entryExt));
                        results.Add(new ImportCandidate
                        {
                            ArchivePath = rootArchivePath,
                            EntryName = combinedEntryPath,
                            DestFileName = destName,
                            SizeBytes = entry.Length
                        });
                    }
                }
            }
            else if (isRar)
            {
                using var reader = ReaderFactory.OpenReader(ms);
                while (reader.MoveToNextEntry())
                {
                    var entry = reader.Entry;
                    if (entry.IsDirectory || entry.Key == null || entry.Size == 0) continue;

                    var entryExt = Path.GetExtension(entry.Key).ToLowerInvariant();
                    var combinedEntryPath = string.IsNullOrEmpty(currentEntryPath)
                        ? entry.Key
                        : currentEntryPath + "::" + entry.Key;

                    if (entryExt == ".zip" || entryExt == ".rar")
                    {
                        using var entryStream = reader.OpenEntryStream();
                        EnumerateArchiveRecursively(rootArchivePath, combinedEntryPath, entryStream, depth + 1, results, allowedExts, minBytes, ref belowMin, ref zipCorrupted, ref rarMultiVol);
                        continue;
                    }

                    if (allowedExts.Contains(entryExt))
                    {
                        if (minBytes > 0 && entry.Size < minBytes)
                        {
                            Interlocked.Increment(ref belowMin);
                            continue;
                        }
                        var destName = NormalizeImportAuthorName(BuildArchiveDestName(rootArchivePath, entry.Key, entryExt));
                        results.Add(new ImportCandidate
                        {
                            ArchivePath = rootArchivePath,
                            EntryName = combinedEntryPath,
                            DestFileName = destName,
                            SizeBytes = entry.Size
                        });
                    }
                }
            }
        }
        catch
        {
            Interlocked.Increment(ref zipCorrupted);
        }
    }

    public static void ExtractFromArchiveRecursive(ImportCandidate candidate, string destFile)
    {
        if (!candidate.EntryName.Contains("::"))
        {
            ExtractFromArchive(candidate, destFile);
            return;
        }

        var parts = candidate.EntryName.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
        var currentArchive = candidate.ArchivePath;

        using var rootFs = File.OpenRead(currentArchive);
        var currentStream = (Stream)rootFs;
        MemoryStream? msTemp = null;

        try
        {
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var nextMs = new MemoryStream();
                
                byte[] header = new byte[4];
                if (currentStream.Read(header, 0, 4) < 4)
                    throw new InvalidOperationException("Fallo al leer cabecera de archivo anidado.");
                
                using var seekableStream = new MemoryStream();
                seekableStream.Write(header, 0, 4);
                currentStream.CopyTo(seekableStream);
                seekableStream.Position = 0;

                bool isZip = header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;

                if (isZip)
                {
                    using var zip = new ZipArchive(seekableStream, ZipArchiveMode.Read);
                    var entry = zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, part, StringComparison.OrdinalIgnoreCase));
                    if (entry == null) throw new InvalidOperationException($"No se encontró la entrada anidada: {part}");
                    using var entryStream = entry.Open();
                    entryStream.CopyTo(nextMs);
                }
                else
                {
                    using var reader = ReaderFactory.OpenReader(seekableStream);
                    bool found = false;
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory && string.Equals(reader.Entry.Key, part, StringComparison.OrdinalIgnoreCase))
                        {
                            using var entryStream = reader.OpenEntryStream();
                            entryStream.CopyTo(nextMs);
                            found = true;
                            break;
                        }
                    }
                    if (!found) throw new InvalidOperationException($"No se encontró la entrada anidada: {part}");
                }

                nextMs.Position = 0;
                if (msTemp != null) msTemp.Dispose();
                msTemp = nextMs;
                currentStream = msTemp;
            }

            using var dst = File.Create(destFile);
            currentStream.Position = 0;
            currentStream.CopyTo(dst);
        }
        finally
        {
            msTemp?.Dispose();
        }
    }

    public static string? GetEpubTextSample(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                                                        e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                                                        e.FullName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                using var s = entry.Open();
                using var sr = new StreamReader(s, Encoding.UTF8);
                var html = sr.ReadToEnd();
                return Regex.Replace(html, "<.*?>", string.Empty);
            }
        }
        catch {}
        return null;
    }

    public static string GetBinaryWordsSample(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sb = new StringBuilder();
            byte[] buf = new byte[65536];
            int read = fs.Read(buf, 0, buf.Length);
            int wordLen = 0;
            var wordBuf = new char[50];
            for (int i = 0; i < read; i++)
            {
                byte b = buf[i];
                if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') || b == 0xC3 || b == 0xC2)
                {
                    if (wordLen < 45)
                    {
                        wordBuf[wordLen++] = (char)b;
                    }
                }
                else
                {
                    if (wordLen >= 2)
                    {
                        sb.Append(wordBuf, 0, wordLen).Append(' ');
                    }
                    wordLen = 0;
                }
            }
            return sb.ToString();
        }
        catch { return string.Empty; }
    }

    public static string? ExtractTextSample(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".txt" || ext == ".text")
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                char[] buffer = new char[4000];
                int read = sr.Read(buffer, 0, 4000);
                return new string(buffer, 0, read);
            }
            catch { return null; }
        }
        if (ext == ".epub")
        {
            var txt = GetEpubTextSample(filePath);
            if (!string.IsNullOrWhiteSpace(txt)) return txt;
        }
        if (ext == ".pdf")
        {
            try
            {
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(filePath);
                if (pdf.NumberOfPages > 0)
                {
                    var page = pdf.GetPage(1);
                    return page.Text;
                }
            }
            catch {}
        }
        return GetBinaryWordsSample(filePath);
    }

    public static bool IsSpanishText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        var words = text.Split(new[] { ' ', '.', ',', ';', ':', '!', '?', '\r', '\n', '-', '_', '"', '\'', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 5) return true;

        int esCount = 0;
        int enCount = 0;
        int frCount = 0;

        var esStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "de", "la", "el", "en", "y", "que", "un", "una", "los", "las", "con", "para", "por", "se", "del", "lo", "es", "su", "al", "como", "mas", "o", "sus", "pero" };
        var enStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "the", "of", "and", "to", "a", "in", "is", "that", "it", "he", "was", "for", "on", "are", "as", "with", "his", "they", "i", "at", "be", "this", "had", "have" };
        var frStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "de", "la", "le", "et", "les", "des", "en", "un", "une", "que", "est", "dans", "pour", "qui", "sur", "avec", "ce", "se", "il", "au", "par", "mais" };

        int limit = Math.Min(words.Length, 500);
        for (int i = 0; i < limit; i++)
        {
            var w = words[i];
            if (esStop.Contains(w)) esCount++;
            if (enStop.Contains(w)) enCount++;
            if (frStop.Contains(w)) frCount++;
        }

        if (esCount == 0 && enCount == 0 && frCount == 0) return true;
        return esCount >= enCount && esCount >= frCount;
    }

    public static bool TryEnrichEpubMetadata(string epubPath, string author, string title, string? seriesName = null, string? seriesIndex = null)
    {
        try
        {
            using (var fs = new FileStream(epubPath, FileMode.Open, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Update))
            {
                var opfEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));
                if (opfEntry != null)
                {
                    string opfContent;
                    using (var s = opfEntry.Open())
                    using (var sr = new StreamReader(s, Encoding.UTF8))
                    {
                        opfContent = sr.ReadToEnd();
                    }

                    var doc = new System.Xml.XmlDocument();
                    doc.LoadXml(opfContent);

                    var nsmgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
                    nsmgr.AddNamespace("opf", doc.DocumentElement?.NamespaceURI ?? string.Empty);
                    nsmgr.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");

                    var creatorNode = doc.SelectSingleNode("//dc:creator", nsmgr);
                    if (creatorNode != null)
                    {
                        creatorNode.InnerText = author;
                    }
                    else
                    {
                        var metadataNode = doc.SelectSingleNode("//opf:metadata", nsmgr) ?? doc.SelectSingleNode("//metadata", nsmgr);
                        if (metadataNode != null)
                        {
                            var creator = doc.CreateElement("dc", "creator", "http://purl.org/dc/elements/1.1/");
                            creator.InnerText = author;
                            metadataNode.AppendChild(creator);
                        }
                    }

                    var titleNode = doc.SelectSingleNode("//dc:title", nsmgr);
                    if (titleNode != null)
                    {
                        titleNode.InnerText = title;
                    }
                    else
                    {
                        var metadataNode = doc.SelectSingleNode("//opf:metadata", nsmgr) ?? doc.SelectSingleNode("//metadata", nsmgr);
                        if (metadataNode != null)
                        {
                            var tNode = doc.CreateElement("dc", "title", "http://purl.org/dc/elements/1.1/");
                            tNode.InnerText = title;
                            metadataNode.AppendChild(tNode);
                        }
                    }

                    // Series metadata
                    if (!string.IsNullOrWhiteSpace(seriesName))
                    {
                        var existingSeriesNodes = doc.SelectNodes("//opf:meta[@name='calibre:series']", nsmgr) ?? doc.SelectNodes("//meta[@name='calibre:series']", nsmgr);
                        if (existingSeriesNodes != null)
                        {
                            foreach (System.Xml.XmlNode n in existingSeriesNodes)
                                n.ParentNode?.RemoveChild(n);
                        }

                        var existingSeriesIdxNodes = doc.SelectNodes("//opf:meta[@name='calibre:series_index']", nsmgr) ?? doc.SelectNodes("//meta[@name='calibre:series_index']", nsmgr);
                        if (existingSeriesIdxNodes != null)
                        {
                            foreach (System.Xml.XmlNode n in existingSeriesIdxNodes)
                                n.ParentNode?.RemoveChild(n);
                        }

                        var metadataNode = doc.SelectSingleNode("//opf:metadata", nsmgr) ?? doc.SelectSingleNode("//metadata", nsmgr);
                        if (metadataNode != null)
                        {
                            var seriesMeta = doc.CreateElement("meta");
                            seriesMeta.SetAttribute("name", "calibre:series");
                            seriesMeta.SetAttribute("content", seriesName);
                            metadataNode.AppendChild(seriesMeta);

                            if (!string.IsNullOrWhiteSpace(seriesIndex))
                            {
                                var indexMeta = doc.CreateElement("meta");
                                indexMeta.SetAttribute("name", "calibre:series_index");
                                indexMeta.SetAttribute("content", seriesIndex);
                                metadataNode.AppendChild(indexMeta);
                            }
                        }
                    }

                    opfEntry.Delete();
                    var newOpf = zip.CreateEntry(opfEntry.FullName, CompressionLevel.Optimal);
                    using (var s = newOpf.Open())
                    using (var sw = new StreamWriter(s, new UTF8Encoding(false)))
                    {
                        doc.Save(sw);
                    }
                    return true;
                }
            }
        }
        catch {}
        return false;
    }

    public static bool IsEpubValidAndNotCorrupt(string epubPath)
    {
        try
        {
            using var fs = new FileStream(epubPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < 100) return false;
            
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var entries = zip.Entries;
            if (entries.Count == 0) return false;

            bool hasMimetype = zip.GetEntry("mimetype") != null;
            bool hasOpf = zip.Entries.Any(e => e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));
            
            return hasMimetype || hasOpf;
        }
        catch { return false; }
    }

    public static void ExtractSeriesAndTitle(string title, out string cleanTitle, out string? seriesName, out string? seriesIndex)
    {
        cleanTitle = title;
        seriesName = null;
        seriesIndex = null;

        var rxStart = new Regex(@"^(.*?)\s+(\d+)\s*-\s*(.*)$", RegexOptions.Compiled);
        var mStart = rxStart.Match(title);
        if (mStart.Success)
        {
            var sName = mStart.Groups[1].Value.Trim();
            var sIndex = mStart.Groups[2].Value.Trim();
            var tName = mStart.Groups[3].Value.Trim();
            if (sName.Length >= 3 && !sName.Equals("vol", StringComparison.OrdinalIgnoreCase) && !sName.Equals("part", StringComparison.OrdinalIgnoreCase))
            {
                seriesName = sName;
                seriesIndex = sIndex;
                cleanTitle = tName;
                return;
            }
        }

        var rxBrackets = new Regex(@"[\(\[](.*?)\s*[-#]?\s*(\d+)\s*[\)\]]", RegexOptions.Compiled);
        var mBrackets = rxBrackets.Match(title);
        if (mBrackets.Success)
        {
            var sName = mBrackets.Groups[1].Value.Trim();
            var sIndex = mBrackets.Groups[2].Value.Trim();
            if (sName.Length >= 3 && !sName.Equals("vol", StringComparison.OrdinalIgnoreCase) && !sName.Equals("part", StringComparison.OrdinalIgnoreCase))
            {
                seriesName = sName;
                seriesIndex = sIndex;
                cleanTitle = title.Replace(mBrackets.Value, string.Empty).Trim();
                return;
            }
        }
    }

    public static bool TryConvertEpubToTxtDirect(string epubPath, string txtPath)
    {
        try
        {
            using var fs = File.OpenRead(epubPath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            
            var entries = zip.Entries
                .Where(e => e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                            e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                            e.FullName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName)
                .ToList();

            if (entries.Count == 0) return false;

            using var outFs = new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new StreamWriter(outFs, Encoding.UTF8);

            foreach (var entry in entries)
            {
                using var s = entry.Open();
                using var sr = new StreamReader(s, Encoding.UTF8);
                var html = sr.ReadToEnd();
                
                var text = Regex.Replace(html, "<.*?>", string.Empty);
                text = System.Net.WebUtility.HtmlDecode(text);
                
                sw.WriteLine(text);
                sw.WriteLine();
            }
            return true;
        }
        catch { return false; }
    }

    public static bool TryConvertPdfToTxtDirect(string pdfPath, string txtPath)
    {
        try
        {
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
            using var outFs = new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new StreamWriter(outFs, Encoding.UTF8);

            for (int i = 1; i <= pdf.NumberOfPages; i++)
            {
                var page = pdf.GetPage(i);
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sw.WriteLine(text);
                }
            }
            return true;
        }
        catch { return false; }
    }

    public static ImportScanResult CollectImportCandidates(
        string srcDir,
        HashSet<string> allowedExts,
        bool extractArchives,
        long minBytes,
        CancellationToken cancellationToken = default)
    {
        var skipDirNames = FrozenSet.ToFrozenSet(
            new[] { "_NoEspañol", "_NoPublicos", "_Duplicados", "incomplete", "txt", ".git", "__MACOSX" },
            StringComparer.OrdinalIgnoreCase);

        var directFiles = new List<string>(1024);
        var archiveFiles = new List<string>(512);

        var stack = new Stack<(string Path, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        stack.Push((srcDir, 0));
        visited.Add(srcDir);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (dir, depth) = stack.Pop();
            
            if (depth > 16) continue;

            try 
            { 
                foreach (var sub in Directory.EnumerateDirectories(dir)) 
                { 
                    var subFull = Path.GetFullPath(sub);
                    if (!skipDirNames.Contains(Path.GetFileName(subFull)) && visited.Add(subFull)) 
                        stack.Push((subFull, depth + 1)); 
                } 
            }
            catch { }
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    var actualFile = TryRenameFileWithCorrectedExtension(file);
                    var ext = Path.GetExtension(actualFile);
                    if (allowedExts.Contains(ext)) { directFiles.Add(actualFile); continue; }
                    if (extractArchives &&
                        (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".rar", StringComparison.OrdinalIgnoreCase)))
                        archiveFiles.Add(actualFile);
                }
            }
            catch { }
        }

        var directCandidates = new ConcurrentBag<ImportCandidate>();
        int directAdded = 0;
        int belowMin = 0;
        var directParallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };
        Parallel.ForEach(directFiles, directParallel,
            file =>
            {
                long sz;
                if (minBytes > 0)
                {
                    try { sz = new FileInfo(file).Length; if (sz < minBytes) { Interlocked.Increment(ref belowMin); return; } }
                    catch { return; }
                }
                else
                {
                    try { sz = new FileInfo(file).Length; } catch { sz = 0; }
                }
                var rawName = FixBrokenEncoding(Path.GetFileName(file));
                var name = NormalizeImportAuthorName(rawName);
                directCandidates.Add(new ImportCandidate { FilePath = file, DestFileName = name, SizeBytes = sz });
                Interlocked.Increment(ref directAdded);
            });

        var archiveCandidates = new ConcurrentBag<ImportCandidate>();
        int archiveAdded = 0;
        int rarMultiVol = 0, zipCorrupted = 0;
        var archiveParallel = new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken };
        Parallel.ForEach(archiveFiles, archiveParallel, file =>
        {
            try
            {
                using var fs = File.OpenRead(file);
                EnumerateArchiveRecursively(file, "", fs, 1, archiveCandidates, allowedExts, minBytes, ref belowMin, ref zipCorrupted, ref rarMultiVol);
            }
            catch
            {
                Interlocked.Increment(ref zipCorrupted);
            }
        });
        archiveAdded = archiveCandidates.Count;

        var all = new List<ImportCandidate>(directAdded + archiveAdded);
        all.AddRange(directCandidates);
        all.AddRange(archiveCandidates);
        all.Sort(static (a, b) =>
        {
            var ac = string.IsNullOrEmpty(a.ArchivePath) ? 0 : 1;
            var bc = string.IsNullOrEmpty(b.ArchivePath) ? 0 : 1;
            var c1 = ac.CompareTo(bc);
            if (c1 != 0) return c1;
            if (ac == 1)
            {
                var c2 = string.Compare(a.ArchivePath, b.ArchivePath, StringComparison.OrdinalIgnoreCase);
                if (c2 != 0) return c2;
            }
            return string.Compare(a.DestFileName, b.DestFileName, StringComparison.OrdinalIgnoreCase);
        });

        return new ImportScanResult
        {
            Candidates = all,
            RarMultiVolume = rarMultiVol,
            ZipCorrupted = zipCorrupted,
            BelowMinSize = belowMin
        };
    }

    private static string FixBrokenEncoding(string name)
    {
        if (!name.Contains('Ã') && !name.Contains('Â')) return name;
        try
        {
            var latin1 = Encoding.GetEncoding("iso-8859-1");
            var utf8 = Encoding.UTF8;
            var bytes = latin1.GetBytes(name);
            var fixed_ = utf8.GetString(bytes);
            int brokenBefore = CountBrokenChars(name);
            int brokenAfter = CountBrokenChars(fixed_);
            return brokenAfter < brokenBefore ? SanitizeFileName(fixed_) : SanitizeFileName(name);
        }
        catch { return SanitizeFileName(name); }
    }

    private static int CountBrokenChars(string s)
    {
        int n = 0;
        foreach (var c in s) if (c > 0x7E && c < 0xC0) n++;
        return n;
    }

    private static string NormalizeImportAuthorName(string fileName)
    {
        var m = s_rxImportAuthorCommaFmt.Match(fileName);
        if (!m.Success) return fileName;
        var surnames = m.Groups[1].Value.Trim();
        var firstName = m.Groups[2].Value.Trim();
        var rest = m.Groups[3].Value;
        return $"{firstName} {surnames}{rest}";
    }

    private static string BuildArchiveDestName(string archivePath, string entryName, string entryExt)
    {
        var archBaseName = Path.GetFileNameWithoutExtension(archivePath);
        var archBaseExt = Path.GetExtension(archBaseName);
        if (archBaseExt.Equals(entryExt, StringComparison.OrdinalIgnoreCase))
            return SanitizeFileName(archBaseName);

        var entryBaseName = Path.GetFileName(entryName);
        if (!string.IsNullOrWhiteSpace(entryBaseName) &&
            entryBaseName.Length > 4 &&
            !entryBaseName.Equals("book" + entryExt, StringComparison.OrdinalIgnoreCase))
            return SanitizeFileName(entryBaseName);

        return SanitizeFileName(archBaseName + entryExt);
    }

    private static readonly char[] s_invalidFileNameChars = Path.GetInvalidFileNameChars();
    private static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Array.IndexOf(s_invalidFileNameChars, c) >= 0 ? '_' : c);
        var s = sb.ToString().Trim('.', ' ');
        if (s.Length == 0) return "_";
        var res = s[..Math.Min(s.Length, 180)];
        return res.Normalize(NormalizationForm.FormC);
    }

    public static string ResolveNameConflict(string dir, string fileName)
    {
        var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        int n = 2;
        string candidate;
        do { candidate = Path.Combine(dir, $"{nameNoExt} ({n}){ext}"); n++; }
        while (File.Exists(candidate) && n < 1000);
        return candidate;
    }

    // ─── SimHash y BK-Tree para Deduplicación Difusa (Offline/Cero dependencias) ───

    private static ulong Fnv1a64(string s)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in s)
        {
            hash ^= (ushort)c;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    public static ulong ComputeSimHash(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var weights = new int[64];
        var tokens = text.Split(new[] { ' ', '-', '_', '.', ',', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (token.Length < 2) continue;
            ulong tokenHash = Fnv1a64(token.ToLowerInvariant());

            for (int i = 0; i < 64; i++)
            {
                if (((tokenHash >> i) & 1) == 1)
                    weights[i]++;
                else
                    weights[i]--;
            }
        }

        ulong simhash = 0;
        for (int i = 0; i < 64; i++)
        {
            if (weights[i] > 0)
                simhash |= (1UL << i);
        }

        return simhash;
    }

    public static int HammingDistance(ulong x, ulong y)
    {
        ulong val = x ^ y;
        int dist = 0;
        while (val > 0)
        {
            val &= val - 1;
            dist++;
        }
        return dist;
    }

    public sealed class BKNode
    {
        public ulong Hash { get; }
        public Dictionary<int, BKNode> Children { get; } = new();

        public BKNode(ulong hash)
        {
            Hash = hash;
        }
    }

    public sealed class BKTree
    {
        private BKNode? _root;

        public void Add(ulong hash)
        {
            if (_root == null)
            {
                _root = new BKNode(hash);
                return;
            }

            var curr = _root;
            while (true)
            {
                int dist = HammingDistance(curr.Hash, hash);
                if (dist == 0) return;

                if (curr.Children.TryGetValue(dist, out var next))
                {
                    curr = next;
                }
                else
                {
                    curr.Children[dist] = new BKNode(hash);
                    break;
                }
            }
        }

        public bool FindNearDuplicate(ulong hash, int maxDistance)
        {
            if (_root == null) return false;
            return FindNearDuplicate(_root, hash, maxDistance);
        }

        private bool FindNearDuplicate(BKNode node, ulong hash, int maxDistance)
        {
            int dist = HammingDistance(node.Hash, hash);
            if (dist <= maxDistance) return true;

            int minDist = dist - maxDistance;
            int maxDist = dist + maxDistance;

            foreach (var kv in node.Children)
            {
                if (kv.Key >= minDist && kv.Key <= maxDist)
                {
                    if (FindNearDuplicate(kv.Value, hash, maxDistance))
                        return true;
                }
            }
            return false;
        }
    }
}
