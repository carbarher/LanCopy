using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SlskDownAvalonia.Models;

namespace SlskDownImportBiblioteca;

/// <summary>
/// Lee/escribe el mismo config.json que la app principal, actualizando solo campos de importación
/// sin pasar por cifrado de contraseña (preserva el JSON existente).
/// </summary>
internal static class ImportAppConfigStore
{
    private static string ConfigPath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlskDownAvalonia");
        Directory.CreateDirectory(appData);
        return Path.Combine(appData, "config.json");
    }

    public static async Task<AppConfig> LoadAsync()
    {
        var path = ConfigPath();
        if (!File.Exists(path))
            return new AppConfig();
        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var cfg = await JsonSerializer.DeserializeAsync<AppConfig>(fs).ConfigureAwait(false);
            return cfg ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static async Task SaveImportFieldsAsync(AppConfig cfg)
    {
        var path = ConfigPath();
        try
        {
            JsonObject root;
            if (File.Exists(path))
            {
                var text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                var node = JsonNode.Parse(text);
                root = node as JsonObject ?? new JsonObject();
            }
            else
                root = new JsonObject();

            void SetBool(string k, bool v) => root[k] = v;
            void SetInt(string k, int v) => root[k] = v;
            void SetStr(string k, string v) => root[k] = v;

            SetStr("downloadDirectory", cfg.DownloadDirectory);
            SetStr("importSourceDir", cfg.ImportSourceDir);
            SetBool("importEpub", cfg.ImportEpub);
            SetBool("importMobi", cfg.ImportMobi);
            SetBool("importPdf", cfg.ImportPdf);
            SetBool("importFb2", cfg.ImportFb2);
            SetBool("importAzw3", cfg.ImportAzw3);
            SetBool("importDjvu", cfg.ImportDjvu);
            SetBool("importTxt", cfg.ImportTxt);
            SetBool("importArchive", cfg.ImportArchive);
            SetBool("importGenTxt", cfg.ImportGenTxt);
            SetBool("importSkipDup", cfg.ImportSkipDup);
            SetBool("importHashDedup", cfg.ImportHashDedup);
            SetBool("importHashDedupForce", cfg.ImportHashDedupForce);
            SetBool("importDryRun", cfg.ImportDryRun);
            SetInt("importMinKB", cfg.ImportMinKB);
            SetBool("importFastMode", cfg.ImportFastMode);
            SetBool("importQuickSignatureDedup", cfg.ImportQuickSignatureDedup);
            SetBool("importMinimalLog", cfg.ImportMinimalLog);
            SetBool("importWarmCache", cfg.ImportWarmCache);
            SetBool("importAutoPause", cfg.ImportAutoPause);
            SetBool("importPrioritizeBestFormats", cfg.ImportPrioritizeBestFormats);
            SetBool("importOnlyNewByDate", cfg.ImportOnlyNewByDate);
            SetInt("importOnlyNewDays", cfg.ImportOnlyNewDays);
            SetBool("importQualityScan", cfg.ImportQualityScan);
            SetBool("importNightModeAuto", cfg.ImportNightModeAuto);
            SetInt("importNightStartHour", cfg.ImportNightStartHour);
            SetInt("importNightEndHour", cfg.ImportNightEndHour);
            SetBool("importPipelineFullVerify", cfg.ImportPipelineFullVerify);
            SetBool("importPipelineMissingTxt", cfg.ImportPipelineMissingTxt);

            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            /* no bloquear import */
        }
    }
}
