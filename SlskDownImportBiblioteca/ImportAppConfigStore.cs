using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace SlskDownImportBiblioteca;

/// <summary>
/// Configuracion propia del importador independiente.
/// Si no existe, migra valores relevantes desde el config compartido legado.
/// </summary>
internal static class ImportAppConfigStore
{
    internal readonly record struct ConfigStoreResult(bool Success, string? ErrorMessage)
    {
        public static ConfigStoreResult Ok() => new(true, null);
        public static ConfigStoreResult Fail(string message) => new(false, message);
    }

    private static string ConfigPath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlskDownImportBiblioteca");
        Directory.CreateDirectory(appData);
        return Path.Combine(appData, "config.json");
    }

    private static string LegacySharedConfigPath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlskDownAvalonia");
        return Path.Combine(appData, "config.json");
    }

    public static async Task<ImportAppSettings> LoadAsync()
    {
        var path = ConfigPath();
        if (!File.Exists(path))
        {
            var migrated = await TryMigrateFromLegacySharedConfigAsync(path).ConfigureAwait(false);
            return migrated ?? ImportAppSettings.Defaults();
        }

        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var cfg = await JsonSerializer.DeserializeAsync<ImportAppSettings>(fs).ConfigureAwait(false);
            return cfg ?? ImportAppSettings.Defaults();
        }
        catch
        {
            return ImportAppSettings.Defaults();
        }
    }

    public static async Task<ConfigStoreResult> SaveAsync(ImportAppSettings cfg)
    {
        var path = ConfigPath();
        try
        {
            var tmp = path + ".tmp";
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);
            return ConfigStoreResult.Ok();
        }
        catch (Exception ex)
        {
            return ConfigStoreResult.Fail(ex.Message);
        }
    }

    private static async Task<ImportAppSettings?> TryMigrateFromLegacySharedConfigAsync(string newPath)
    {
        var legacyPath = LegacySharedConfigPath();
        if (!File.Exists(legacyPath))
            return null;

        try
        {
            var text = await File.ReadAllTextAsync(legacyPath).ConfigureAwait(false);
            var root = JsonNode.Parse(text) as JsonObject;
            if (root == null)
                return null;

            var cfg = ImportAppSettings.Defaults();

            static bool GetBool(JsonObject rootObj, string key, bool fallback)
                => rootObj[key]?.GetValue<bool>() ?? fallback;
            static int GetInt(JsonObject rootObj, string key, int fallback)
                => rootObj[key]?.GetValue<int>() ?? fallback;
            static string GetStr(JsonObject rootObj, string key, string fallback)
                => rootObj[key]?.GetValue<string>() ?? fallback;

            cfg.DownloadDirectory = GetStr(root, "downloadDirectory", cfg.DownloadDirectory);
            cfg.ImportSourceDir = GetStr(root, "importSourceDir", cfg.ImportSourceDir);
            cfg.ImportEpub = GetBool(root, "importEpub", cfg.ImportEpub);
            cfg.ImportMobi = GetBool(root, "importMobi", cfg.ImportMobi);
            cfg.ImportPdf = GetBool(root, "importPdf", cfg.ImportPdf);
            cfg.ImportFb2 = GetBool(root, "importFb2", cfg.ImportFb2);
            cfg.ImportAzw3 = GetBool(root, "importAzw3", cfg.ImportAzw3);
            cfg.ImportDjvu = GetBool(root, "importDjvu", cfg.ImportDjvu);
            cfg.ImportTxt = GetBool(root, "importTxt", cfg.ImportTxt);
            cfg.ImportArchive = GetBool(root, "importArchive", cfg.ImportArchive);
            cfg.ImportGenTxt = GetBool(root, "importGenTxt", cfg.ImportGenTxt);
            cfg.ImportSkipDup = GetBool(root, "importSkipDup", cfg.ImportSkipDup);
            cfg.ImportHashDedup = GetBool(root, "importHashDedup", cfg.ImportHashDedup);
            cfg.ImportHashDedupForce = GetBool(root, "importHashDedupForce", cfg.ImportHashDedupForce);
            cfg.ImportDryRun = GetBool(root, "importDryRun", cfg.ImportDryRun);
            cfg.ImportMinKB = GetInt(root, "importMinKB", cfg.ImportMinKB);
            cfg.ImportFastMode = GetBool(root, "importFastMode", cfg.ImportFastMode);
            cfg.ImportQuickSignatureDedup = GetBool(root, "importQuickSignatureDedup", cfg.ImportQuickSignatureDedup);
            cfg.ImportMinimalLog = GetBool(root, "importMinimalLog", cfg.ImportMinimalLog);
            cfg.ImportWarmCache = GetBool(root, "importWarmCache", cfg.ImportWarmCache);
            cfg.ImportAutoPause = GetBool(root, "importAutoPause", cfg.ImportAutoPause);
            cfg.ImportPrioritizeBestFormats = GetBool(root, "importPrioritizeBestFormats", cfg.ImportPrioritizeBestFormats);
            cfg.ImportOnlyNewByDate = GetBool(root, "importOnlyNewByDate", cfg.ImportOnlyNewByDate);
            cfg.ImportOnlyNewDays = GetInt(root, "importOnlyNewDays", cfg.ImportOnlyNewDays);
            cfg.ImportQualityScan = GetBool(root, "importQualityScan", cfg.ImportQualityScan);
            cfg.ImportNightModeAuto = GetBool(root, "importNightModeAuto", cfg.ImportNightModeAuto);
            cfg.ImportNightStartHour = GetInt(root, "importNightStartHour", cfg.ImportNightStartHour);
            cfg.ImportNightEndHour = GetInt(root, "importNightEndHour", cfg.ImportNightEndHour);
            cfg.ImportPipelineMissingTxt = GetBool(root, "importPipelineMissingTxt", cfg.ImportPipelineMissingTxt);
            cfg.ImportSourceCleanupEnabled = GetBool(root, "importSourceCleanupEnabled", cfg.ImportSourceCleanupEnabled);
            cfg.DownloadOnlyPublicDomain = GetBool(root, "downloadOnlyPublicDomain", cfg.DownloadOnlyPublicDomain);

            await SaveAsync(cfg).ConfigureAwait(false);
            return cfg;
        }
        catch
        {
            return null;
        }
    }
}
