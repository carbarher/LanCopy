using LanCopy.Services;
using Xunit;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LanCopy.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("2.0.0", "1.9.9", 1)]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("1.0.0.1", "1.0.0.0", 1)]
    public void CompareVersions_Works(string a, string b, int expectedSign)
    {
        var r = UpdateChecker.CompareVersions(a, b);
        Assert.Equal(expectedSign, Math.Sign(r));
    }

    [Fact]
    public void CurrentVersion_IsParseable()
    {
        var v = UpdateChecker.CurrentVersion;
        Assert.False(string.IsNullOrWhiteSpace(v));
        Assert.Equal(0, UpdateChecker.CompareVersions(v, v));
    }

    [Fact]
    public void ParseSha256Manifest_ReturnsFullSha256FirstToken()
    {
        var expected = new string('a', 64);
        var hash = UpdateChecker.ParseSha256Manifest(expected.ToUpperInvariant() + "  LanCopy.exe");
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void ParseSha256Manifest_RejectsInvalidOrShortHash()
    {
        Assert.Null(UpdateChecker.ParseSha256Manifest("ABCDEF1234  LanCopy.exe"));
        Assert.Null(UpdateChecker.ParseSha256Manifest(new string('g', 64) + "  LanCopy.exe"));
    }

    [Fact]
    public void ParseReleaseAssetManifest_AcceptsSignedJsonManifest()
    {
        var expected = new string('b', 64);
        var json = JsonSerializer.Serialize(new { sha256 = expected.ToUpperInvariant(), signature = "abc" });
        var manifest = UpdateChecker.ParseReleaseAssetManifest(json);
        Assert.NotNull(manifest);
        Assert.Equal(expected, manifest!.Sha256);
        Assert.Equal("abc", manifest.Signature);
    }

    [Fact]
    public void VerifyReleaseManifestSignature_RequiresValidSignatureWhenKeyIsPinned()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = key.ExportSubjectPublicKeyInfoPem();
        var hash = new string('c', 64);
        var assetName = "LanCopy.exe";
        var payload = Encoding.UTF8.GetBytes($"{assetName}\n{hash}\n");
        var signature = Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256));
        var manifest = new UpdateChecker.ReleaseAssetManifest(hash, signature);

        Assert.True(UpdateChecker.VerifyReleaseManifestSignature(manifest, assetName, publicKey));
        Assert.False(UpdateChecker.VerifyReleaseManifestSignature(manifest, "Other.exe", publicKey));
        Assert.False(UpdateChecker.VerifyReleaseManifestSignature(manifest with { Signature = null }, assetName, publicKey));
    }

    [Fact]
    public void VerifyReleaseManifestSignature_MatchesReleaseSigningScriptPayload()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = key.ExportSubjectPublicKeyInfoPem();
        var assetName = "LanCopy-win-x64.zip";
        var hash = new string('d', 64);
        var payload = Encoding.UTF8.GetBytes(assetName + "\n" + hash + "\n");
        var signature = Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256));
        var json = JsonSerializer.Serialize(new { sha256 = hash, signature });

        var manifest = UpdateChecker.ParseReleaseAssetManifest(json);

        Assert.NotNull(manifest);
        Assert.True(UpdateChecker.VerifyReleaseManifestSignature(manifest!, assetName, publicKey));
    }

    [Fact]
    public void ReleaseManifestPublicKeyPem_IsPinnedAndImportable()
    {
        Assert.False(string.IsNullOrWhiteSpace(UpdateChecker.ReleaseManifestPublicKeyPem));
        using var key = ECDsa.Create();
        key.ImportFromPem(UpdateChecker.ReleaseManifestPublicKeyPem);
    }

    [Fact]
    public void PinnedReleaseManifestPublicKey_VerifiesKnownReleaseSignature()
    {
        var manifest = new UpdateChecker.ReleaseAssetManifest(
            "1d800fba305d45f6342e74ba5ff8ae440ccecd1545d59f8d7cdf20438c856592",
            "7sV43h7xBypkxpfnIq5qWKO8Q1OJ2+DYDQvmWTTXZJW/YN7unJwpriPY9PP6ehU2sQVKx75dwYrkJ8lWPGnJJw==");

        Assert.True(UpdateChecker.VerifyReleaseManifestSignature(
            manifest,
            "LanCopy-win-x64.exe",
            UpdateChecker.ReleaseManifestPublicKeyPem));
    }

    [Fact]
    public async Task ComputeSha256Hex_ProducesStableHash()
    {
        var path = Path.Combine(Path.GetTempPath(), "LanCopyHash_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(path, "hello");
            var hash = await UpdateChecker.ComputeSha256HexAsync(path);
            Assert.Equal(UpdateChecker.ComputeSha256Hex(path), hash);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
