using System;
using System.Collections.Generic;

namespace SlskDownBibliotecaImport;

public sealed class ImportCandidate
{
    public string ArchivePath { get; init; } = "";
    public string EntryName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string DestFileName { get; init; } = "";
    public long SizeBytes { get; init; }
    public bool IsArchived => !string.IsNullOrEmpty(ArchivePath);
}

public sealed class ImportScanResult
{
    public List<ImportCandidate> Candidates { get; init; } = new();
    public int RarMultiVolume { get; init; }
    public int ZipCorrupted { get; init; }
    public int BelowMinSize { get; init; }
}

public sealed class BibliotecaImportRuntimeState
{
    public bool WantArchive { get; set; }
    public bool GenTxt { get; set; }
    public bool GenTxtRequested { get; set; }
    public bool SkipDup { get; set; }
    public bool DryRun { get; set; }
    public bool HashDedup { get; set; }
    /// <summary>Si es true, el autotune no desactiva hash por tamaño de lote (más lento, dedup por contenido más fiable).</summary>
    public bool HashDedupForce { get; set; }
    public bool FastMode { get; set; }
    public bool QuickSig { get; set; }
    public bool MinimalLog { get; set; }
    public bool WarmCache { get; set; }
    public bool AutoPause { get; set; }
    public bool Prioritize { get; set; }
    public bool OnlyNew { get; set; }
    public int OnlyNewDays { get; set; }
    public bool QualityScan { get; set; }
    public bool NightAuto { get; set; }
    public int NightStart { get; set; }
    public int NightEnd { get; set; }
    public long MinBytes { get; set; }
    public bool PipelineVerify { get; set; }
    public bool PipelineTxt { get; set; }
}

public sealed class ImportRunReport
{
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public string SourceDir { get; set; } = "";
    public string DestDir { get; set; } = "";
    public int Candidates { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    /// <summary>Desglose de omitidos (suma puede ser &lt; Skipped si hubo solapamiento de categorías en contadores legacy).</summary>
    public int SkippedExistingFileName { get; set; }
    public int SkippedQuickSigInDest { get; set; }
    public int SkippedContentHash { get; set; }
    public int SkippedCheckpointResume { get; set; }
    public int DroppedDuplicateDestPrepass { get; set; }
    public int DroppedDuplicateQuickSigPrepass { get; set; }
    public int CopyRetryAfterIoConflict { get; set; }
    public int Errors { get; set; }
    public int TxtOk { get; set; }
    public int TxtFail { get; set; }
    public long CopiedBytes { get; set; }
    public int SuspiciousCount { get; set; }
    public double ScanSeconds { get; set; }
    public double HashDestSeconds { get; set; }
    public double HashSrcSeconds { get; set; }
    /// <summary>Tiempo del bucle paralelo de copia/extracción (por candidato, con skips).</summary>
    public double CopyParallelSeconds { get; set; }
    /// <summary>ms medio por candidato procesado en la fase paralela (incluye skips rápidos).</summary>
    public double AvgMsPerCandidateInCopyPhase { get; set; }
    public double TotalSeconds { get; set; }
    public Dictionary<string, int> SourceHeatmap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ErrorSamples { get; set; } = new();
}
