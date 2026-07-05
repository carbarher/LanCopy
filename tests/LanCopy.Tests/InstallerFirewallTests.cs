using Xunit;

namespace LanCopy.Tests;

public sealed class InstallerFirewallTests
{
    [Fact]
    public void WindowsInstaller_ConfiguresTransferAndDiscoveryFirewallRules()
    {
        var installerPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "installer", "LanCopy.iss"));

        var script = File.ReadAllText(installerPath);

        Assert.Contains("LanCopy TCP 8742", script);
        Assert.Contains("protocol=TCP localport=8742", script);
        Assert.Contains("LanCopy UDP Discovery 8743", script);
        Assert.Contains("protocol=UDP localport=8743", script);
        Assert.Contains("profile=private", script);
        Assert.Contains("program=\"\"{app}\\{#MyAppExeName}\"\"", script);
    }
}
