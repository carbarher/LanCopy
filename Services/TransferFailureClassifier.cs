namespace LanCopy.Services;

internal static class TransferFailureClassifier
{
    public static bool IsPermanent(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return message.Contains("svc.accessDenied", StringComparison.Ordinal)
            || message.Contains("svc.outsideShare", StringComparison.Ordinal)
            || message.Contains("svc.invalidPath", StringComparison.Ordinal)
            || message.Contains("svc.pathNotFound", StringComparison.Ordinal)
            || message.Contains("svc.notDirectory", StringComparison.Ordinal)
            || message.Contains("svc.sysProtected", StringComparison.Ordinal)
            || message.Contains("svc.readOnly", StringComparison.Ordinal)
            || message.Contains("svc.rejected", StringComparison.Ordinal)
            || message.Contains("svc.tlsRequired", StringComparison.Ordinal)
            || message.Contains("st.identityChanged", StringComparison.Ordinal)
            || message.Contains("st.certRejected", StringComparison.Ordinal);
    }

    public static string ErrorKey(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "svc.accessDenied";
        foreach (var key in new[] { "svc.accessDenied", "svc.outsideShare", "svc.invalidPath", "svc.pathNotFound", "svc.notDirectory", "svc.sysProtected", "svc.readOnly", "svc.rejected", "svc.tlsRequired", "st.identityChanged", "st.certRejected" })
            if (message.Contains(key, StringComparison.Ordinal)) return key;
        return message;
    }
}