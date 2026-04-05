using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDownBibliotecaImport;

public static class BibliotecaImportEngine
{
    private static int CountUnderscoreOrDash(string s)
    {
        int n = 0;
        foreach (var ch in s)
            if (ch == '_' || ch == '-') n++;
        return n;
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:F1} MB";
        return $"{mb / 1024.0:F2} GB";
    }

    public static async Task RunAsync(
        string importSrc,
        string importDest,
        HashSet<string> allowedExts,
        string importCheckpointPath,
        string importCheckpointDeltaPath,
        BibliotecaImportRuntimeState o,
        IBibliotecaImportHost host,
        IBibliotecaImportUi ui,
        HashSet<string>? retryOnlyDestFileNames,
        CancellationToken ct)
    {
        const bool autoTuneImport = true;
        host.LogClear();
        if (o.PipelineVerify || o.PipelineTxt)
        {
            host.PipelineQueue(
                "⏳ Importación…\n" +
                (o.PipelineVerify ? "○ Verificación completa (pendiente)\n" : "") +
                (o.PipelineTxt ? "○ TXTs faltantes (pendiente)" : ""));
        }
        else
            host.PipelineQueue("⏳ Importación… (sin post-tareas)");

        host.Log($"📂 Importando desde: {importSrc}");
        host.Log($"   → Destino: {importDest}");
        host.Log($"   → Formatos: {string.Join(" ", allowedExts)} {(o.WantArchive ? "+ ZIP/RAR" : "")}");
        host.Log($"   → Mínimo: {BibliotecaImportEngine.FormatBytes(o.MinBytes)} · Skip dups: {o.SkipDup} · Gen TXT: {o.GenTxt} · Hash dedup: {o.HashDedup}{(o.HashDedupForce ? " (forzado)" : "")}");
        if (o.DryRun) host.Log("   🔍 MODO SIMULACIÓN — no se modificará nada");
        host.Log("");

        int copied = 0, skipped = 0, errors = 0, txtOk = 0, txtFail = 0, extracted = 0;
        int skipExistingFile = 0, skipQuickSigDest = 0, skipContentHash = 0, copyIoRetries = 0;
        int resumeFromCheckpoint = 0, droppedQuickSigPrepass = 0;
        long copiedBytes = 0;
        var copiedPaths = new System.Collections.Concurrent.ConcurrentBag<string>();
        var failedCandidates = new System.Collections.Concurrent.ConcurrentBag<ImportCandidate>();
        var sourceHeatmap = new System.Collections.Concurrent.ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var errorSamples = new System.Collections.Concurrent.ConcurrentQueue<string>();
        int suspiciousCount = 0;
        long lastCheckpointTicks = DateTime.UtcNow.Ticks;
        var nameConflictLock = new object();
        var swTotal = System.Diagnostics.Stopwatch.StartNew();
        var swScan = new System.Diagnostics.Stopwatch();
        var swDestHash = new System.Diagnostics.Stopwatch();
        var swSrcHash = new System.Diagnostics.Stopwatch();

        // j3: cargar checkpoint previo si existe
        var importScanCachePath = Path.Combine(importDest, ".import_scan_cache.json");
        var importDone = BibliotecaImportFilesystem.LoadImportCheckpointWithDelta(importCheckpointPath, importCheckpointDeltaPath);
        if (!o.DryRun && importDone.Count > 0)
            host.Log($"   ♻️ Reanudando: {importDone.Count} archivos ya copiados en sesión anterior");

        try
        {
            // Recopilar candidatos — usar caché si el árbol fuente no cambió
            host.Status("🔍 Escaneando archivos...");
            ImportScanResult scan;
            swScan.Start();
            var cachedScan = !o.DryRun
                ? await Task.Run(() => BibliotecaImportFilesystem.LoadImportScanCache(importScanCachePath, importSrc, allowedExts, o.WantArchive, o.MinBytes, o.WarmCache), ct)
                : null;
            if (cachedScan != null)
            {
                scan = cachedScan;
                host.Log($"   ⚡ Caché válido — {scan.Candidates.Count} candidatos (escaneo omitido)");
            }
            else
            {
                scan = await Task.Run(() => BibliotecaImportFilesystem.CollectImportCandidates(importSrc, allowedExts, o.WantArchive, o.MinBytes, ct), ct);
                if (!o.DryRun)
                    await Task.Run(() => BibliotecaImportFilesystem.SaveImportScanCache(importScanCachePath, importSrc, allowedExts, o.WantArchive, o.MinBytes, scan), ct);
            }
            swScan.Stop();
            var candidates = scan.Candidates;

            // Reintento solo fallidos (pedido por botón dedicado)
            if (retryOnlyDestFileNames != null && retryOnlyDestFileNames.Count > 0)
            {
                candidates = candidates.Where(c => retryOnlyDestFileNames.Contains(c.DestFileName)).ToList();
                host.Log($"🔁 Modo reintento: {candidates.Count:N0} candidatos fallidos");
            }

            // Solo nuevos por fecha
            if (o.OnlyNew && o.OnlyNewDays > 0)
            {
                var minDateUtc = DateTime.UtcNow.AddDays(-o.OnlyNewDays);
                var filtered = new System.Collections.Generic.List<ImportCandidate>(candidates.Count);
                int byDateSkipped = 0;
                foreach (var c in candidates)
                {
                    var srcPath = c.IsArchived ? c.ArchivePath : c.FilePath;
                    DateTime ts;
                    try { ts = File.GetLastWriteTimeUtc(srcPath); } catch { ts = DateTime.MinValue; }
                    if (ts >= minDateUtc) filtered.Add(c);
                    else byDateSkipped++;
                }
                candidates = filtered;
                if (byDateSkipped > 0) host.Log($"🕒 Filtro fecha: {byDateSkipped:N0} omitidos (> {o.OnlyNewDays} días)");
            }

            // Prioridad por formato (valor primero)
            if (o.Prioritize)
                candidates.Sort((a, b) => BibliotecaImportFilesystem.ImportFormatPriority(a.DestFileName).CompareTo(BibliotecaImportFilesystem.ImportFormatPriority(b.DestFileName)));

            int droppedSameDest = BibliotecaImportFilesystem.DeduplicateCandidatesByDestFileName(candidates);
            if (droppedSameDest > 0)
                host.Log($"   🔀 {droppedSameDest:N0} omitidos (mismo nombre de archivo en destino)");

            int archivedN = 0;
            foreach (var c in candidates) if (c.IsArchived) archivedN++;
            host.Log($"📋 {candidates.Count} candidatos ({archivedN} en ZIP/RAR)");
            if (scan.RarMultiVolume > 0)  host.Log($"   ⚠️ {scan.RarMultiVolume} RAR multi-volumen ignorados (requieren todos los volúmenes)");
            if (scan.ZipCorrupted > 0)    host.Log($"   ⚠️ {scan.ZipCorrupted} ZIP/RAR corruptos o protegidos ignorados");
            if (scan.BelowMinSize > 0)    host.Log($"   ⚠️ {scan.BelowMinSize} archivos descartados por tamaño mínimo");

            if (candidates.Count == 0)
            {
                host.Status("Sin archivos que importar");
                host.PipelineQueue("ℹ️ Sin archivos que importar", persistSnapshot: true);
                return;
            }

            // Perfil automático: adapta flags según tamaño real del lote
            if (autoTuneImport)
            {
                var n = candidates.Count;
                var hasArchives = candidates.Exists(c => c.IsArchived);
                // Base: respeta preferencia del usuario como "mínimo", pero auto-eleva para lotes grandes.
                o.FastMode   = o.FastMode || n >= 8000;
                o.QuickSig   = o.QuickSig || n >= 2500;
                o.MinimalLog = o.MinimalLog || n >= 3000;
                o.WarmCache  = true;
                // hashDedup fuerte solo en lotes pequeños/medianos y cuando no hay fast mode (salvo forzado)
                if (!o.HashDedupForce)
                    o.HashDedup = o.HashDedup && !o.FastMode && n <= 20000;
                // TXT en línea solo si lote manejable; en lotes grandes pasa a fase diferida
                if (o.GenTxt && n >= 2500) o.GenTxt = false;

                host.Log($"🤖 AutoTune import: n={n:N0} · archives={(hasArchives ? "sí" : "no")} · fast={o.FastMode} · quickSig={o.QuickSig} · hash={o.HashDedup} · txtInline={o.GenTxt} · logMin={o.MinimalLog}");
            }

            // j3: filtrar ya copiados según checkpoint
            if (!o.DryRun && importDone.Count > 0)
            {
                int resumeSkipped = 0;
                var filtered = new System.Collections.Generic.List<ImportCandidate>(candidates.Count);
                foreach (var c in candidates)
                {
                    if (importDone.Contains(c.DestFileName)) { resumeSkipped++; resumeFromCheckpoint++; skipped++; }
                    else filtered.Add(c);
                }
                if (resumeSkipped > 0)
                {
                    host.Log($"   ♻️ {resumeSkipped} ya copiados (checkpoint) — se saltan");
                    candidates = filtered;
                }
            }

            if (o.QuickSig && candidates.Count > 1)
            {
                droppedQuickSigPrepass = BibliotecaImportFilesystem.DeduplicateCandidatesByQuickSignature(candidates);
                if (droppedQuickSigPrepass > 0)
                    host.Log($"   🔀 {droppedQuickSigPrepass:N0} omitidos (misma firma rápida: tamaño + título; se prioriza mejor formato)");
            }

            if (candidates.Count == 0)
            {
                host.Status("Sin archivos que importar");
                host.PipelineQueue("ℹ️ Sin archivos que importar", persistSnapshot: true);
                return;
            }

            // Dry-run: mostrar resumen sin copiar
            if (o.DryRun)
            {
                var byExt = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in candidates)
                {
                    var ext2 = Path.GetExtension(c.DestFileName);
                    byExt[ext2] = byExt.TryGetValue(ext2, out var n) ? n + 1 : 1;
                }
                var kvps = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string,int>>(byExt);
                kvps.Sort(static (a,b) => b.Value.CompareTo(a.Value));
                host.Log("📊 Desglose por formato:");
                foreach (var kv in kvps)
                    host.Log($"   {kv.Key.TrimStart('.')} → {kv.Value}");
                host.Log($"\n🔍 Simulación completa — activa 'Importar' sin simulación para copiar los {candidates.Count} archivos.");
                host.Status($"🔍 Simulación: {candidates.Count} candidatos · sin cambios");
                var simTotal = candidates.Count;
                host.InvokeOnUiThread(() =>
                {
                    ui.SetProgressMax(Math.Max(1, simTotal));
                    ui.SetProgressValue(simTotal);
                });
                if (!ct.IsCancellationRequested)
                    host.PipelineQueue("✅ Simulación terminada", persistSnapshot: true);
                return;
            }

            // Hash dedup: construir set de hashes de archivos ya en destino
            var existingHashes = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var existingQuickSigs = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            if (o.QuickSig)
            {
                host.Status("🧾 Cargando firmas rápidas...");
                await Task.Run(() =>
                {
                    foreach (var f in Directory.EnumerateFiles(importDest, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var ext = Path.GetExtension(f);
                            if (!allowedExts.Contains(ext)) continue;
                            var fi = new FileInfo(f);
                            var sig = BibliotecaImportFilesystem.BuildQuickImportSignature(Path.GetFileName(f), fi.Length);
                            existingQuickSigs.TryAdd(sig, 0);
                        }
                        catch { }
                    }
                }, ct);
                host.Log($"🧾 {existingQuickSigs.Count:N0} firmas rápidas en destino");
            }
            if (o.HashDedup)
            {
                host.Status("🔐 Calculando hashes en destino...");
                swDestHash.Start();
                await Task.Run(() =>
                {
                    System.Threading.Tasks.Parallel.ForEach(
                        Directory.EnumerateFiles(importDest, "*.*", SearchOption.TopDirectoryOnly),
                        new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
                        f =>
                        {
                            if (!allowedExts.Contains(Path.GetExtension(f))) return;
                            try
                            {
                                using var fs = File.OpenRead(f);
                                var hash = BibliotecaImportFilesystem.ComputeStreamContentHashHex(fs);
                                existingHashes.TryAdd(hash, 0);
                            }
                            catch { }
                        });
                }, ct);
                swDestHash.Stop();
                host.Log($"🔐 {existingHashes.Count} hashes (XxHash64) en destino cargados");
            }

            // Precalcular hashes de los candidatos origen en paralelo (solo si hashDedup activo).
            // En lotes gigantes, este pre-cálculo duplica I/O de forma brutal (hash + copia).
            // Opt: en importaciones grandes calculamos hash "on-demand" por candidato.
            var srcHashMap = new System.Collections.Concurrent.ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            bool precomputeSourceHashes = o.HashDedup && !existingHashes.IsEmpty && candidates.Count <= 4000;
            if (precomputeSourceHashes)
            {
                host.Status("🔐 Calculando hashes origen (XxHash64)…");
                swSrcHash.Start();
                await Task.Run(() =>
                {
                    System.Threading.Tasks.Parallel.ForEach(candidates,
                        new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
                        c =>
                        {
                            try
                            {
                                var key = BibliotecaImportFilesystem.CandidateContentDedupKey(c);
                                srcHashMap[key] = BibliotecaImportFilesystem.TryComputeCandidateHash(c);
                            }
                            catch
                            {
                                srcHashMap[BibliotecaImportFilesystem.CandidateContentDedupKey(c)] = null;
                            }
                        });
                }, ct);
                swSrcHash.Stop();
                host.Log($"🔐 {srcHashMap.Count} hashes origen calculados");
            }
            else if (o.HashDedup && !existingHashes.IsEmpty)
            {
                host.Log($"🔐 Hash origen on-demand (candidatos: {candidates.Count:N0}) para acelerar arranque");
            }

            host.InvokeOnUiThread(() =>
            {
                ui.SetProgressMax(Math.Max(1, candidates.Count));
                ui.SetProgressValue(0);
            });

            // k9: cronómetro para ETA
            var importSw = System.Diagnostics.Stopwatch.StartNew();
            int idx = 0;
            int checkpointCounter = 0;
            long lastUiPushTicks = 0;

            // Paralelismo adaptativo: limita presión de I/O en importes masivos.
            int workerCount = candidates.Count >= 60_000 ? 4 : candidates.Count >= 20_000 ? 6 : 8;
            if (o.QuickSig && !o.HashDedup) workerCount = Math.Min(workerCount + 1, 10);
            if (o.FastMode) workerCount = Math.Min(workerCount + 1, 12);
            if (o.NightAuto)
            {
                var hour = DateTime.Now.Hour;
                bool inNightWindow = BibliotecaImportFilesystem.IsWithinHourWindow(hour, o.NightStart, o.NightEnd);
                workerCount = inNightWindow ? Math.Min(workerCount + 2, 14) : Math.Max(workerCount - 2, 3);
            }
            workerCount = Math.Clamp(workerCount, 3, Math.Max(3, Environment.ProcessorCount));
            host.Log($"⚙️ Workers de importación: {workerCount}");
            // Menos snapshots JSON con sets enormes (clonar importDone cuesta O(n)).
            int checkpointStride = candidates.Count >= 60_000 ? 1200 : candidates.Count >= 20_000 ? 600 : 200;
            if (candidates.Count >= 50_000)
                host.Log($"   📦 Lote grande ({candidates.Count:N0} candidatos): checkpoint ~cada {checkpointStride} archivos (menos I/O)");

            host.Log($"[{DateTime.Now:HH:mm:ss}] ▶ Copia paralela · total={candidates.Count:N0} candidatos · workers={workerCount} (log de avance sin nombres de archivo)");

            var parallelImport = new ParallelOptions
            {
                MaxDegreeOfParallelism = workerCount,
                CancellationToken = ct
            };

            var checkpointIoLock = new object();
            var archiveExtractLocks = new System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

            var swCopyPhase = System.Diagnostics.Stopwatch.StartNew();
            await Parallel.ForEachAsync(candidates, parallelImport, async (cap, parallelCt) =>
            {
                    var destFileName = cap.DestFileName;
                    var destFile     = Path.Combine(importDest, destFileName);
                    var srcPathForHeat = cap.IsArchived ? cap.ArchivePath : cap.FilePath;
                    var srcTop = BibliotecaImportFilesystem.GetSourceTopFolder(importSrc, srcPathForHeat);
                    sourceHeatmap.AddOrUpdate(srcTop, 1, static (_, prev) => prev + 1);

                    if (o.AutoPause)
                    {
                        var activeDl = host.ActiveDownloads;
                        if (activeDl >= 3)
                            await Task.Delay(220, parallelCt).ConfigureAwait(false);
                    }

                    // Skip duplicado por nombre
                    if (o.SkipDup && File.Exists(destFile))
                    {
                        System.Threading.Interlocked.Increment(ref skipExistingFile);
                        System.Threading.Interlocked.Increment(ref skipped);
                        goto UpdateUi;
                    }

                    // Skip duplicado por firma rápida (size + nombre normalizado sin extensión)
                    if (o.QuickSig && cap.SizeBytes > 0)
                    {
                        var qsig = BibliotecaImportFilesystem.BuildQuickImportSignature(destFileName, cap.SizeBytes);
                        if (existingQuickSigs.ContainsKey(qsig))
                        {
                            System.Threading.Interlocked.Increment(ref skipQuickSigDest);
                            System.Threading.Interlocked.Increment(ref skipped);
                            goto UpdateUi;
                        }
                    }

                    // Skip duplicado por hash (precalculado o on-demand)
                    if (o.HashDedup && !existingHashes.IsEmpty)
                    {
                        var srcKey = BibliotecaImportFilesystem.CandidateContentDedupKey(cap);
                        string? srcHash = null;
                        if (srcHashMap.TryGetValue(srcKey, out var precalc))
                            srcHash = precalc;
                        else
                        {
                            srcHash = BibliotecaImportFilesystem.TryComputeCandidateHash(cap);
                            srcHashMap.TryAdd(srcKey, srcHash);
                        }
                        if (srcHash != null && existingHashes.ContainsKey(srcHash))
                        {
                            System.Threading.Interlocked.Increment(ref skipContentHash);
                            System.Threading.Interlocked.Increment(ref skipped);
                            goto UpdateUi;
                        }
                    }

                    // Resolver conflicto de nombre (lock dedicado, más corto)
                    lock (nameConflictLock)
                    {
                        if (File.Exists(destFile))
                            destFile = BibliotecaImportFilesystem.ResolveNameConflict(importDest, destFileName);
                    }

                    try
                    {
                        if (cap.IsArchived)
                        {
                            var archSem = archiveExtractLocks.GetOrAdd(cap.ArchivePath, _ => new System.Threading.SemaphoreSlim(1, 1));
                            await archSem.WaitAsync(parallelCt).ConfigureAwait(false);
                            try
                            {
                                BibliotecaImportFilesystem.ExtractFromArchive(cap, destFile);
                                System.Threading.Interlocked.Increment(ref extracted);
                            }
                            finally { archSem.Release(); }
                        }
                        else
                        {
                            const int maxCopyAttempts = 3;
                            bool copyOk = false;
                            for (int attempt = 0; attempt < maxCopyAttempts && !copyOk; attempt++)
                            {
                                try
                                {
                                    File.Copy(cap.FilePath, destFile, overwrite: false);
                                    copyOk = true;
                                }
                                catch (IOException) when (attempt < maxCopyAttempts - 1)
                                {
                                    System.Threading.Interlocked.Increment(ref copyIoRetries);
                                    lock (nameConflictLock)
                                    {
                                        destFile = BibliotecaImportFilesystem.ResolveNameConflict(importDest, Path.GetFileName(destFile));
                                    }
                                }
                            }
                            if (!copyOk)
                                throw new IOException("No se pudo copiar tras reintentos de I/O.");
                        }
                        var copyNum = System.Threading.Interlocked.Increment(ref copied);
                        System.Threading.Interlocked.Add(ref copiedBytes, cap.SizeBytes);
                        copiedPaths.Add(destFile);
                        if (o.QualityScan)
                        {
                            bool suspicious =
                                cap.SizeBytes > 0 && cap.SizeBytes < 32 * 1024 ||
                                destFileName.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
                                CountUnderscoreOrDash(destFileName) > 16;
                            if (suspicious)
                            {
                                var sN = System.Threading.Interlocked.Increment(ref suspiciousCount);
                                if (!o.MinimalLog && (sN <= 10 || sN % 200 == 0))
                                    host.Log($"  ⚠️ Calidad: candidato sospechoso (#{sN})");
                            }
                        }
                        if (!o.MinimalLog && (copyNum <= 30 || copyNum % 150 == 0))
                            host.Log($"  ✅ Copia OK #{copyNum:N0}{(cap.IsArchived ? " (desde ZIP/RAR)" : "")}");

                        // checkpoint: snapshot infrecuente para minimizar I/O
                        lock (importDone)
                        {
                            importDone.Add(destFileName);
                        }
                        if (o.QuickSig && cap.SizeBytes > 0)
                            existingQuickSigs.TryAdd(BibliotecaImportFilesystem.BuildQuickImportSignature(destFileName, cap.SizeBytes), 0);
                        var shouldCheckpointByCount = (System.Threading.Interlocked.Increment(ref checkpointCounter) % checkpointStride) == 0;
                        var nowCpTicks = DateTime.UtcNow.Ticks;
                        var shouldCheckpointByTime = (nowCpTicks - System.Threading.Volatile.Read(ref lastCheckpointTicks)) > TimeSpan.FromSeconds(15).Ticks;
                        lock (checkpointIoLock)
                        {
                            BibliotecaImportFilesystem.AppendImportCheckpointDelta(importCheckpointDeltaPath, destFileName);
                            if (shouldCheckpointByCount || shouldCheckpointByTime)
                            {
                                BibliotecaImportFilesystem.MergeImportCheckpointDeltaIntoMain(importCheckpointPath, importCheckpointDeltaPath);
                                System.Threading.Volatile.Write(ref lastCheckpointTicks, nowCpTicks);
                            }
                        }

                        // Generar TXT si está habilitado (solo si pasa tamaño ≥200 KB, español y calidad)
                        if (o.GenTxt && host.CalibreAvailable)
                        {
                            var ext = Path.GetExtension(destFile);
                            if (!ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
                                !ext.Equals(".text", StringComparison.OrdinalIgnoreCase) &&
                                host.ShouldEnqueueTxtForImport(destFileName, destFile, cap.SizeBytes, o.QualityScan))
                            {
                                try
                                {
                                    var r = await host.EnqueueCalibreTxtAsync(destFile, importDest, parallelCt).ConfigureAwait(false);
                                    if (r.Success && !r.Skipped) System.Threading.Interlocked.Increment(ref txtOk);
                                    else if (!r.Skipped) System.Threading.Interlocked.Increment(ref txtFail);
                                }
                                catch { System.Threading.Interlocked.Increment(ref txtFail); }
                            }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        System.Threading.Interlocked.Increment(ref errors);
                        failedCandidates.Add(cap);
                        if (errorSamples.Count < 20)
                            errorSamples.Enqueue($"{destFileName}: {ex.Message}");
                        host.Log($"  ❌ Fallo #{System.Threading.Volatile.Read(ref errors):N0}: {ex.Message}");
                    }

                    UpdateUi:
                    var curIdx = System.Threading.Interlocked.Increment(ref idx);
                    var snapCopied = System.Threading.Volatile.Read(ref copied);
                    var snapSkipped = System.Threading.Volatile.Read(ref skipped);
                    var snapErrors = System.Threading.Volatile.Read(ref errors);
                    var snapExtracted = System.Threading.Volatile.Read(ref extracted);
                    var snapTxtOk = System.Threading.Volatile.Read(ref txtOk);
                    var snapTxtFail = System.Threading.Volatile.Read(ref txtFail);
                    bool pushUi = curIdx == 1 || curIdx >= candidates.Count || (curIdx % 12 == 0);
                    var nowUiTicks = DateTime.UtcNow.Ticks;
                    if (pushUi && nowUiTicks - System.Threading.Volatile.Read(ref lastUiPushTicks) < TimeSpan.FromMilliseconds(250).Ticks)
                        pushUi = false;
                    if (pushUi)
                    {
                        string etaStr = "";
                            string speedStr = "";
                        if (curIdx >= 10 && importSw.Elapsed.TotalSeconds > 1)
                        {
                            var secPerFile = importSw.Elapsed.TotalSeconds / curIdx;
                            var remaining  = (candidates.Count - curIdx) * secPerFile;
                            etaStr = remaining >= 60
                                ? $" · ETA {(int)(remaining / 60)}m{(int)(remaining % 60):D2}s"
                                : $" · ETA {(int)remaining}s";
                                var bps = System.Threading.Volatile.Read(ref copiedBytes) / importSw.Elapsed.TotalSeconds;
                                speedStr = bps > 0 ? $" · {(bps / (1024 * 1024)):F1} MB/s" : "";
                        }
                        var pct = candidates.Count > 0 ? 100.0 * curIdx / candidates.Count : 0d;
                        var logTime = DateTime.Now.ToString("HH:mm:ss");
                        var txtPart = (o.GenTxt && host.CalibreAvailable)
                            ? $" · TXT ok {snapTxtOk:N0} / fallo {snapTxtFail:N0}"
                            : "";
                        host.Log(
                            $"[{logTime}] Avance {curIdx:N0}/{candidates.Count:N0} ({pct:F1}%) · " +
                            $"copiados {snapCopied:N0} · omitidos {snapSkipped:N0} · err {snapErrors:N0} · ZIP/RAR {snapExtracted:N0}{txtPart}{etaStr}{speedStr} · workers={workerCount}");
                        host.PostToUi(() =>
                        {
                            ui.SetProgressValue(curIdx);
                            ui.SetStatus($"[{curIdx}/{candidates.Count}] {snapCopied} copiados · {snapSkipped} dup · {snapErrors} err{etaStr}{speedStr}");
                            ui.SetPerf($"workers={workerCount} · done={curIdx}/{candidates.Count} · copied={snapCopied} · skipped={snapSkipped} · err={snapErrors}{speedStr}");
                        });
                            System.Threading.Volatile.Write(ref lastUiPushTicks, nowUiTicks);
                    }
            }).ConfigureAwait(false);

            foreach (var sem in archiveExtractLocks.Values)
            {
                try { sem.Dispose(); } catch { }
            }
            archiveExtractLocks.Clear();

            // Modo rápido: fase 2 diferida de TXT (solo si usuario la pidió originalmente)
            if (o.FastMode && o.GenTxtRequested && host.CalibreAvailable && !copiedPaths.IsEmpty)
            {
                host.Status("🔊 Fase 2: generando TXTs diferidos...");
                var txtTargets = copiedPaths.Where(p =>
                {
                    var ext = Path.GetExtension(p);
                    return !ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
                           !ext.Equals(".text", StringComparison.OrdinalIgnoreCase);
                }).ToList();
                var f2Total = Math.Max(1, txtTargets.Count);
                host.InvokeOnUiThread(() =>
                {
                    ui.SetProgressMax(f2Total);
                    ui.SetProgressValue(0);
                    ui.SetStatus("🔊 Fase 2: generando TXTs diferidos…");
                    ui.SetPerf($"0/{txtTargets.Count} · ok {txtOk} · fallo {txtFail}");
                });
                var f2Step = 0;
                foreach (var p in txtTargets)
                {
                    try
                    {
                        long sz;
                        try { sz = new FileInfo(p).Length; } catch { continue; }
                        if (!host.ShouldEnqueueTxtForImport(Path.GetFileName(p), p, sz, o.QualityScan))
                            continue;
                        var r = await host.EnqueueCalibreTxtAsync(p, importDest).ConfigureAwait(false);
                        if (r.Success && !r.Skipped) txtOk++;
                        else if (!r.Skipped) txtFail++;
                    }
                    catch { txtFail++; }
                    finally
                    {
                        f2Step++;
                        var snapStep = f2Step;
                        var snapOk = txtOk;
                        var snapFail = txtFail;
                        host.InvokeOnUiThread(() =>
                        {
                            ui.SetProgressValue(Math.Min(snapStep, f2Total));
                            ui.SetStatus($"🔊 Fase 2 TXT: {snapStep}/{txtTargets.Count}");
                            ui.SetPerf($"ok {snapOk} · fallo {snapFail}");
                        });
                    }
                }
                host.Log($"✅ Conversión TXT (fase 2 diferida) terminada · total sesión: {txtOk} OK · {txtFail} fallos");
                o.GenTxt = true; // para el resumen final
            }

            // Manifest de rollback de esta ejecución
            var copiedList = copiedPaths.ToList();
            var manifestPath = Path.Combine(importDest, $"import_manifest_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            try
            {
                File.WriteAllText(manifestPath, System.Text.Json.JsonSerializer.Serialize(copiedList));
                host.SetLastImportManifestPath(manifestPath);
            }
            catch { }

            // Reporte JSON + CSV exportable
            var report = new ImportRunReport
            {
                StartedAt = DateTime.Now - swTotal.Elapsed,
                FinishedAt = DateTime.Now,
                SourceDir = importSrc,
                DestDir = importDest,
                Candidates = candidates.Count,
                Imported = copied,
                Skipped = skipped,
                SkippedExistingFileName = skipExistingFile,
                SkippedQuickSigInDest = skipQuickSigDest,
                SkippedContentHash = skipContentHash,
                SkippedCheckpointResume = resumeFromCheckpoint,
                DroppedDuplicateDestPrepass = droppedSameDest,
                DroppedDuplicateQuickSigPrepass = droppedQuickSigPrepass,
                CopyRetryAfterIoConflict = copyIoRetries,
                Errors = errors,
                TxtOk = txtOk,
                TxtFail = txtFail,
                CopiedBytes = copiedBytes,
                SuspiciousCount = suspiciousCount,
                ScanSeconds = swScan.Elapsed.TotalSeconds,
                HashDestSeconds = swDestHash.Elapsed.TotalSeconds,
                HashSrcSeconds = swSrcHash.Elapsed.TotalSeconds,
                CopyParallelSeconds = swCopyPhase.Elapsed.TotalSeconds,
                AvgMsPerCandidateInCopyPhase = candidates.Count > 0
                    ? swCopyPhase.Elapsed.TotalMilliseconds / candidates.Count
                    : 0,
                TotalSeconds = swTotal.Elapsed.TotalSeconds,
                SourceHeatmap = sourceHeatmap.OrderByDescending(kv => kv.Value).Take(30).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
                ErrorSamples = errorSamples.ToList()
            };

            if (!o.MinimalLog)
            {
                host.Log(
                    $"📌 Estado import: {report.Candidates:N0} candidatos → {report.Imported:N0} copiados · {report.Skipped:N0} omitidos · {report.Errors:N0} err · " +
                    $"{extracted:N0} desde ZIP/RAR · fase paralela {report.CopyParallelSeconds:F1}s (~{report.AvgMsPerCandidateInCopyPhase:F0} ms/candidato)");
            }

            try
            {
                var repBase = Path.Combine(importDest, $"import_report_{DateTime.Now:yyyyMMdd_HHmmss}");
                File.WriteAllText(repBase + ".json", System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                var csv = new StringBuilder(2048);
                csv.AppendLine("metric,value");
                csv.AppendLine($"candidates,{report.Candidates}");
                csv.AppendLine($"imported,{report.Imported}");
                csv.AppendLine($"skipped_total,{report.Skipped}");
                csv.AppendLine($"skipped_existing_filename,{report.SkippedExistingFileName}");
                csv.AppendLine($"skipped_quicksig_in_dest,{report.SkippedQuickSigInDest}");
                csv.AppendLine($"skipped_content_hash,{report.SkippedContentHash}");
                csv.AppendLine($"skipped_checkpoint_resume,{report.SkippedCheckpointResume}");
                csv.AppendLine($"dropped_dup_dest_prepass,{report.DroppedDuplicateDestPrepass}");
                csv.AppendLine($"dropped_dup_quicksig_prepass,{report.DroppedDuplicateQuickSigPrepass}");
                csv.AppendLine($"copy_io_retries,{report.CopyRetryAfterIoConflict}");
                csv.AppendLine($"copy_parallel_seconds,{report.CopyParallelSeconds:F3}");
                csv.AppendLine($"avg_ms_per_candidate_copy_phase,{report.AvgMsPerCandidateInCopyPhase:F2}");
                csv.AppendLine($"errors,{report.Errors}");
                csv.AppendLine($"txt_ok,{report.TxtOk}");
                csv.AppendLine($"txt_fail,{report.TxtFail}");
                csv.AppendLine($"copied_bytes,{report.CopiedBytes}");
                csv.AppendLine("");
                csv.AppendLine("source_folder,count");
                foreach (var kv in report.SourceHeatmap)
                    csv.AppendLine($"\"{kv.Key.Replace("\"","\"\"")}\",{kv.Value}");
                File.WriteAllText(repBase + ".csv", csv.ToString(), Encoding.UTF8);
                host.Log($"📊 Reporte exportado: {Path.GetFileName(repBase)}.json/.csv");
            }
            catch { }

            host.SetLastImportFailed(failedCandidates.ToList());

            var summary = $"✅ {copied} importados ({extracted} extraídos de ZIP/RAR) · {skipped} duplicados · {errors} errores";
            if (o.GenTxt) summary += $" · TXT: {txtOk} OK / {txtFail} fail";
            summary += $" · {BibliotecaImportEngine.FormatBytes(copiedBytes)}";

            if (copied > 0)
            {
                lock (checkpointIoLock)
                    BibliotecaImportFilesystem.MergeImportCheckpointDeltaIntoMain(importCheckpointPath, importCheckpointDeltaPath);
            }

            host.PostToUi(() => {
                host.Log("");
                host.Log(summary);
                host.Status(summary);
                if (copied > 0)
                {
                    host.NotifyImportCompleted(copied);
                    BibliotecaImportFilesystem.DeleteImportCheckpoint(importCheckpointPath);
                    BibliotecaImportFilesystem.DeleteImportCheckpoint(importCheckpointDeltaPath);
                    BibliotecaImportFilesystem.DeleteImportCheckpoint(importScanCachePath);
                }
            });

            host.Log(
                $"⏱ Fases: scan {swScan.Elapsed.TotalSeconds:F1}s · hash-dest {swDestHash.Elapsed.TotalSeconds:F1}s · hash-src {swSrcHash.Elapsed.TotalSeconds:F1}s · " +
                $"copia-paralela {report.CopyParallelSeconds:F1}s (~{report.AvgMsPerCandidateInCopyPhase:F0} ms/candidato) · total {swTotal.Elapsed.TotalSeconds:F1}s");
            if (!o.MinimalLog && (skipExistingFile + skipQuickSigDest + skipContentHash + resumeFromCheckpoint) > 0)
                host.Log($"   📎 Omitidos en copia: ya existía nombre {skipExistingFile:N0} · firma en destino {skipQuickSigDest:N0} · mismo contenido (hash) {skipContentHash:N0} · checkpoint previo {resumeFromCheckpoint:N0}");
            if (!o.MinimalLog && (droppedSameDest + droppedQuickSigPrepass + copyIoRetries) > 0)
                host.Log($"   📎 Pre-filtro / I/O: mismo destino en lista {droppedSameDest:N0} · misma firma en lista {droppedQuickSigPrepass:N0} · reintentos copia {copyIoRetries:N0}");
            if (!sourceHeatmap.IsEmpty)
            {
                var top = sourceHeatmap.OrderByDescending(kv => kv.Value).Take(5)
                    .Select(kv => $"{kv.Key}:{kv.Value}");
                host.Log($"🗺️ Heatmap top: {string.Join(" · ", top)}");
            }

            if (!ct.IsCancellationRequested)
            {
                if (o.DryRun)
                    host.PipelineQueue("✅ Simulación terminada", persistSnapshot: true);
                else if (!o.PipelineVerify && !o.PipelineTxt)
                    host.PipelineQueue("✅ Importación terminada", persistSnapshot: true);
            }

            // Pipeline post-import: verificación y/o TXTs (misma sesión pesada que la importación)
            if (!o.DryRun && !ct.IsCancellationRequested && (o.PipelineVerify || o.PipelineTxt))
            {
                try
                {
                    var canVerify = o.PipelineVerify && host.SupportsPipelineFullVerify;
                    host.PipelineQueue("✅ Importación\n" +
                        (o.PipelineVerify
                            ? (canVerify ? "⏳ Verificación completa…\n" : "⏭️ Verificación (solo app principal)…\n")
                            : "") +
                        (o.PipelineTxt ? "○ TXTs faltantes (pendiente)" : ""));

                    if (o.PipelineVerify)
                    {
                        host.Log("");
                        host.Log("═══ Pipeline: verificación completa ═══");
                        if (host.SupportsPipelineFullVerify)
                            await host.RunPipelineFullVerifyAsync(importDest, ct)
                                .ConfigureAwait(false);
                        else
                            host.Log("⏭️ Verificación completa omitida: solo está implementada en la app principal (Mantenimiento). Desmarca «Post: verificación» aquí o ejecuta el import desde la app principal.");
                    }

                    if (o.PipelineTxt && !ct.IsCancellationRequested)
                    {
                        host.PipelineQueue(
                            "✅ Importación\n" +
                            (o.PipelineVerify
                                ? (host.SupportsPipelineFullVerify ? "✅ Verificación\n" : "⏭️ Verificación omitida\n")
                                : "") +
                            "⏳ TXTs faltantes…");
                        host.Log("");
                        host.Log("═══ Pipeline: solo TXTs faltantes ═══");
                        if (host.CalibreAvailable)
                            await host.RunPipelineMissingTxtAsync(importDest, ct).ConfigureAwait(false);
                        else
                        {
                            host.Log("ℹ️ Pipeline TXTs omitido: Calibre no disponible");
                            host.Status("⚠️ Calibre no disponible — TXTs del pipeline omitidos");
                        }
                    }

                    host.PipelineQueue(
                        "✅ Importación\n" +
                        (o.PipelineVerify
                            ? (host.SupportsPipelineFullVerify ? "✅ Verificación\n" : "⏭️ Verificación (solo app principal)\n")
                            : "") +
                        (o.PipelineTxt ? "✅ TXTs" : ""),
                        persistSnapshot: true);
                    host.Log("");
                    host.Log("═══ Pipeline post-import terminado ═══");
                }
                catch (OperationCanceledException)
                {
                    host.Log("⏹ Pipeline post-import cancelado");
                    host.PipelineQueue("⏹ Cancelado", persistSnapshot: true);
                }
                catch (Exception pex)
                {
                    host.Log($"❌ Pipeline post-import: {pex.Message}");
                    host.PipelineQueue($"❌ Error en pipeline: {pex.Message}", persistSnapshot: true);
                }
            }

            if (copied == 0 && errors == 0)
            {
                BibliotecaImportFilesystem.DeleteImportCheckpoint(importCheckpointPath);
                BibliotecaImportFilesystem.DeleteImportCheckpoint(importCheckpointDeltaPath);
                await host.RebuildLibraryIndexIfNeededAsync(importDest, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // j3: guardar checkpoint al cancelar para poder reanudar
            BibliotecaImportFilesystem.SaveImportCheckpoint(importCheckpointPath, importDone);
            host.Log($"⏹ Importación cancelada — checkpoint guardado ({importDone.Count} copiados)");
            host.Status("Cancelado");
            host.PipelineQueue("⏹ Importación cancelada", persistSnapshot: true);
        }
        catch (Exception ex)
        {
            host.Log($"❌ Error: {ex.Message}");
            host.Status($"❌ {ex.Message}");
            host.PipelineQueue($"❌ Importación: {ex.Message}", persistSnapshot: true);
        }
    }
}
