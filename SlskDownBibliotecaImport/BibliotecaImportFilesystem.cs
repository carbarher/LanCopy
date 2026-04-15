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

        var stack = new Stack<string>();
        stack.Push(srcDir);
        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            try { foreach (var sub in Directory.EnumerateDirectories(dir)) { if (!skipDirNames.Contains(Path.GetFileName(sub))) stack.Push(sub); } }
            catch { }
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    var ext = Path.GetExtension(file);
                    if (allowedExts.Contains(ext)) { directFiles.Add(file); continue; }
                    if (extractArchives &&
                        (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".rar", StringComparison.OrdinalIgnoreCase)))
                        archiveFiles.Add(file);
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
                var ext = Path.GetExtension(file);
                if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var zip = ZipFile.OpenRead(file);
                        foreach (var entry in zip.Entries)
                        {
                            if (entry.Length == 0) continue;
                            var entryExt = Path.GetExtension(entry.Name);
                            if (!allowedExts.Contains(entryExt)) continue;
                            if (minBytes > 0 && entry.Length < minBytes) { Interlocked.Increment(ref belowMin); continue; }
                            var destName = NormalizeImportAuthorName(BuildArchiveDestName(file, entry.Name, entryExt));
                            archiveCandidates.Add(new ImportCandidate { ArchivePath = file, EntryName = entry.FullName, DestFileName = destName, SizeBytes = entry.Length });
                            Interlocked.Increment(ref archiveAdded);
                        }
                    }
                    catch { Interlocked.Increment(ref zipCorrupted); }
                }
                else
                {
                    try
                    {
                        var rarName = Path.GetFileName(file);
                        if (s_rxRarMultiVol.IsMatch(rarName))
                        { Interlocked.Increment(ref rarMultiVol); return; }
                        using (var stream = File.OpenRead(file))
                        using (var reader = ReaderFactory.OpenReader(stream))
                        {
                            while (reader.MoveToNextEntry())
                            {
                                var entry = reader.Entry;
                                if (entry.IsDirectory || entry.Key == null) continue;
                                if (entry.Size == 0) continue;
                                var entryExt = Path.GetExtension(entry.Key);
                                if (!allowedExts.Contains(entryExt)) continue;
                                if (minBytes > 0 && entry.Size < minBytes) { Interlocked.Increment(ref belowMin); continue; }
                                var destName = NormalizeImportAuthorName(BuildArchiveDestName(file, entry.Key, entryExt));
                                archiveCandidates.Add(new ImportCandidate { ArchivePath = file, EntryName = entry.Key, DestFileName = destName, SizeBytes = entry.Size });
                                Interlocked.Increment(ref archiveAdded);
                            }
                        }
                    }
                    catch (MultiVolumeExtractionException)
                    { Interlocked.Increment(ref rarMultiVol); }
                    catch { Interlocked.Increment(ref zipCorrupted); }
                }
            }
            catch { Interlocked.Increment(ref zipCorrupted); }
        });

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
        return s.Length == 0 ? "_" : s[..Math.Min(s.Length, 180)];
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
}
