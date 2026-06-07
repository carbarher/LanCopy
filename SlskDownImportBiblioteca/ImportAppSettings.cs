using System;

namespace SlskDownImportBiblioteca;

internal sealed class ImportAppSettings
{
    public string DownloadDirectory { get; set; } = string.Empty;
    public string ImportSourceDir { get; set; } = string.Empty;

    public bool ImportEpub { get; set; } = true;
    public bool ImportMobi { get; set; } = true;
    public bool ImportPdf { get; set; } = true;
    public bool ImportFb2 { get; set; } = true;
    public bool ImportAzw3 { get; set; } = true;
    public bool ImportDjvu { get; set; } = true;
    public bool ImportTxt { get; set; } = true;

    public bool ImportArchive { get; set; } = true;
    public bool ImportGenTxt { get; set; } = true;
    public bool ImportSkipDup { get; set; } = true;
    public bool ImportHashDedup { get; set; } = true;
    public bool ImportHashDedupForce { get; set; } = false;
    public bool ImportDryRun { get; set; } = false;
    public int ImportMinKB { get; set; } = 200;
    public bool ImportFastMode { get; set; } = false;
    public bool ImportQuickSignatureDedup { get; set; } = true;
    public bool ImportMinimalLog { get; set; } = false;
    public bool ImportWarmCache { get; set; } = true;
    public bool ImportAutoPause { get; set; } = true;
    public bool ImportPrioritizeBestFormats { get; set; } = true;
    public bool ImportOnlyNewByDate { get; set; } = false;
    public int ImportOnlyNewDays { get; set; } = 30;
    public bool ImportQualityScan { get; set; } = true;
    public bool ImportNightModeAuto { get; set; } = false;
    public int ImportNightStartHour { get; set; } = 1;
    public int ImportNightEndHour { get; set; } = 7;
    public bool ImportPipelineMissingTxt { get; set; } = false;

    public bool ImportSourceCleanupEnabled { get; set; } = true;
    public bool ImportDeleteUnknownOnCleanup { get; set; } = false;
    public bool PurgeSourceAfterImport { get; set; } = false;
    public bool DownloadOnlyPublicDomain { get; set; } = true;

    public static ImportAppSettings Defaults() => new();
}
