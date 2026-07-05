using System.Text.Json;
using Xunit;

namespace LanCopy.Tests;

public sealed class LocalizationValidationTests
{
    [Fact]
    public void LocaleFiles_Parse_AndIncludeCriticalKeys()
    {
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "i18n"));
        var files = Directory.EnumerateFiles(dir, "*.json").ToList();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("wizard.title", out var wizardTitle) && wizardTitle.ValueKind == JsonValueKind.String, file);
        }

        foreach (var language in new[] { "en.json", "es.json" })
        {
            var file = Path.Combine(dir, language);
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("audit.title", out var auditTitle) && auditTitle.ValueKind == JsonValueKind.String, file);
            Assert.True(root.TryGetProperty("st.connectFailedWithHint", out var connectHint) && connectHint.ValueKind == JsonValueKind.String, file);
            Assert.Contains("8743", connectHint.GetString());
            Assert.True(root.TryGetProperty("st.tlsPeerMismatch", out var tlsMismatch) && tlsMismatch.ValueKind == JsonValueKind.String, file);
        }
    }
}
