using System.Text.Json;
using Xunit;

namespace LanCopy.Tests;

public class CliApiTests
{
    private static string TokenFilePath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LanCopy", "cli-api.json");

    [Fact]
    public void ApiTokenStore_SaveThenLoad_RoundTrips()
    {
        var path = TokenFilePath();
        var backup = File.Exists(path) ? File.ReadAllText(path) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            var token = "unit-test-token-" + Guid.NewGuid().ToString("N");
            LanCopy.Cli.ApiTokenStore.Save(token);

            Assert.True(LanCopy.Cli.ApiTokenStore.TryLoad(out var loaded));
            Assert.Equal(token, loaded);
        }
        finally
        {
            Restore(path, backup);
        }
    }

    [Fact]
    public void ResolveClientToken_UsesPersistedToken_WhenNoCliOrEnv()
    {
        var path = TokenFilePath();
        var backup = File.Exists(path) ? File.ReadAllText(path) : null;
        var envBackup = Environment.GetEnvironmentVariable("LANCOPY_API_TOKEN");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            Environment.SetEnvironmentVariable("LANCOPY_API_TOKEN", null);
            var token = "persisted-token-" + Guid.NewGuid().ToString("N");
            LanCopy.Cli.ApiTokenStore.Save(token);

            var resolved = LanCopy.Cli.Program.ResolveClientToken(null);
            Assert.Equal(token, resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LANCOPY_API_TOKEN", envBackup);
            Restore(path, backup);
        }
    }

    [Fact]
    public void BuildOpenApiDocument_ContainsRetryAndOpenApiPaths()
    {
        var json = JsonSerializer.Serialize(LanCopy.Cli.Program.BuildOpenApiDocument());
        Assert.Contains("/api/v1/openapi.json", json, StringComparison.Ordinal);
        Assert.Contains("/api/v1/transfers/{id}/retry", json, StringComparison.Ordinal);
        Assert.Contains("X-LanCopy-Token", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_InvalidPortOption_ReturnsUsageError()
    {
        var code = await LanCopy.Cli.Program.Main(["api", "--port", "70000"]);
        Assert.Equal(2, code);
    }

    // ── ParseEndpoint tests ───────────────────────────────────────────────────

    [Theory]
    [InlineData("192.168.1.50", 8742, "192.168.1.50", 8742)]
    [InlineData("192.168.1.50:9000", 8742, "192.168.1.50", 9000)]
    [InlineData("myhost", 8742, "myhost", 8742)]
    public void ParseEndpoint_ValidInputs_Parsed(string raw, int defaultPort, string expectedHost, int expectedPort)
    {
        var ep = LanCopy.Cli.Program.ParseEndpoint(raw, defaultPort);
        Assert.Equal(expectedHost, ep.Host);
        Assert.Equal(expectedPort, ep.Port);
    }

    [Theory]
    [InlineData("192.168.1.50:0")]
    [InlineData("192.168.1.50:65536")]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseEndpoint_InvalidInputs_Throws(string raw)
    {
        Assert.ThrowsAny<ArgumentException>(() => LanCopy.Cli.Program.ParseEndpoint(raw, 8742));
    }

    // ── NormalizeRemotePath tests ─────────────────────────────────────────────

    [Fact]
    public void NormalizeRemotePath_ExplicitPath_ReturnsExplicit()
    {
        var result = LanCopy.Cli.Program.NormalizeRemotePath("custom/path.zip", @"C:\local\file.zip");
        Assert.Equal("custom/path.zip", result);
    }

    [Fact]
    public void NormalizeRemotePath_NullPath_UsesFileName()
    {
        var result = LanCopy.Cli.Program.NormalizeRemotePath(null, @"C:\local\file.zip");
        Assert.Equal("file.zip", result);
    }

    private static void Restore(string path, string? backup)
    {
        if (backup is null)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        File.WriteAllText(path, backup);
    }
}
