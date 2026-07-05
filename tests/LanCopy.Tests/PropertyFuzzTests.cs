using System.IO;
using System.Text;
using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class PropertyFuzzTests
{
    [Fact]
    public async Task ProtocolLine_RoundTrips_RandomText()
    {
        var random = new Random(12345);
        for (var i = 0; i < 50; i++)
        {
            var original = MakeRandomText(random, random.Next(0, 512));
            using var ms = new MemoryStream();
            await Protocol.WriteLineAsync(ms, original, CancellationToken.None);
            ms.Position = 0;

            var roundTripped = await Protocol.ReadLineAsync(ms, CancellationToken.None);
            Assert.Equal(original, roundTripped);
        }
    }

    [Fact]
    public async Task ProtocolLine_Rejects_OverlongInput()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('a', Protocol.MaxLineBytes + 1) + "\n");
        using var ms = new MemoryStream(bytes);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Protocol.ReadLineAsync(ms, CancellationToken.None));
        Assert.Equal("svc.lineTooLong", ex.Message);
    }

    [Fact]
    public void PathProtection_FuzzInputs_DoNotThrow()
    {
        var random = new Random(24680);
        for (var i = 0; i < 200; i++)
        {
            var path = MakeRandomPath(random);
            _ = SystemProtection.IsProtected(path);
            _ = SystemProtection.IsProtectedForRemote(path);
        }
    }

    private static string MakeRandomText(Random random, int length)
    {
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            var bucket = random.Next(0, 8);
            sb.Append(bucket switch
            {
                0 => (char)random.Next(0x20, 0x7F),
                1 => (char)random.Next(0x0400, 0x04FF),
                2 => (char)random.Next(0x0600, 0x06FF),
                3 => (char)random.Next(0x4E00, 0x4E7F),
                _ => (char)random.Next(0x20, 0x7F)
            });
        }

        return sb.ToString();
    }

    private static string MakeRandomPath(Random random)
    {
        var segments = random.Next(0, 12);
        var sb = new StringBuilder();
        if (random.Next(0, 2) == 0)
            sb.Append(Path.GetPathRoot(AppContext.BaseDirectory));

        for (var i = 0; i < segments; i++)
        {
            if (sb.Length > 0 && sb[^1] != Path.DirectorySeparatorChar)
                sb.Append(Path.DirectorySeparatorChar);

            var len = random.Next(0, 24);
            for (var j = 0; j < len; j++)
            {
                var ch = random.Next(0, 10) switch
                {
                    0 => ':',
                    1 => '*',
                    2 => '?',
                    3 => '<',
                    4 => '>',
                    5 => '|',
                    6 => '.',
                    7 => Path.DirectorySeparatorChar,
                    _ => (char)random.Next(0x20, 0x7E)
                };
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}
