using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SlskDownBibliotecaImport.Services;

namespace SlskDownBibliotecaImport;

/// <summary>
/// Puente hacia UI, Soulseek (pausas), Calibre y tareas post-import del host (verificación / índice).
/// </summary>
public interface IBibliotecaImportHost
{
    void LogClear();
    void Log(string line);
    void Status(string line);
    void PipelineQueue(string text, bool persistSnapshot = false);
    int ActiveDownloads { get; }
    /// <summary>Encola trabajo en el hilo UI (no bloquea). Para el bucle paralelo de copia.</summary>
    void PostToUi(Action action);
    /// <summary>Ejecuta en el hilo UI de forma síncrona. Tramos cortos: simulación, init de barra, etc.</summary>
    void InvokeOnUiThread(Action action);
    bool CalibreAvailable { get; }
    /// <summary>False en la herramienta independiente: <see cref="RunPipelineFullVerifyAsync"/> no aplica.</summary>
    bool SupportsPipelineFullVerify { get; }
    Task<ConversionResult> EnqueueCalibreTxtAsync(string inputPath, string outputDir, CancellationToken ct = default);
    bool ShouldEnqueueTxtForImport(string destFileName, string destFullPath, long sizeBytes, bool qualityScan);
    void NotifyImportCompleted(int copied);
    Task RebuildLibraryIndexIfNeededAsync(string importDest, CancellationToken ct);
    Task RunPipelineFullVerifyAsync(string importDest, CancellationToken ct);
    Task RunPipelineMissingTxtAsync(string importDest, CancellationToken ct);
    void SetLastImportFailed(IReadOnlyList<ImportCandidate> failed);
    void SetLastImportManifestPath(string? path);
}
