using LanCopy.Services;
using Xunit;

namespace LanCopy.Tests;

public sealed class TransferFailureClassifierTests
{
    [Theory]
    [InlineData("svc.accessDenied")]
    [InlineData("remote: svc.outsideShare")]
    [InlineData("svc.readOnly")]
    [InlineData("st.certRejected")]
    public void PermanentErrors_AreRecognized(string error)
        => Assert.True(TransferFailureClassifier.IsPermanent(error));

    [Theory]
    [InlineData("svc.connClosed")]
    [InlineData("The operation timed out")]
    [InlineData("")]
    public void TransientErrors_AreNotRecognizedAsPermanent(string error)
        => Assert.False(TransferFailureClassifier.IsPermanent(error));

    [Fact]
    public void ErrorKey_ExtractsKnownProtocolError()
        => Assert.Equal("svc.accessDenied", TransferFailureClassifier.ErrorKey("System.Exception: svc.accessDenied"));
}