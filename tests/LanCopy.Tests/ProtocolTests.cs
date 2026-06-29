using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public class ProtocolTests
{
    [Theory]
    [InlineData(0, Protocol.BufferSize)]
    [InlineData(64 * 1024, Protocol.MinBufferSize)]
    [InlineData(256 * 1024, Protocol.MinBufferSize)]
    [InlineData(2 * 1024 * 1024, 256 * 1024)]
    [InlineData(64 * 1024 * 1024, Protocol.BufferSize)]
    [InlineData(2L * 1024 * 1024 * 1024, 1024 * 1024)]
    public void SelectCopyBufferSize_ReturnsExpectedBucket(long size, int expected)
    {
        var actual = Protocol.SelectCopyBufferSize(size);
        Assert.Equal(expected, actual);
    }
}
