namespace LanCopy.Services;

public static class PeerTrustPolicy
{
    public static bool IsAllowed(CertTrust.PeerTrustLevel level, string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd))
            return true;

        if (level is CertTrust.PeerTrustLevel.OwnerDevice or CertTrust.PeerTrustLevel.Trusted)
            return true;

        // Risky operations require Trusted/Owner by default.
        if (IsHighRiskCommand(cmd))
            return false;

        return level switch
        {
            CertTrust.PeerTrustLevel.Unknown => cmd switch
            {
                "disconnect_notice" or "health" or "caps" or "text" => true,
                _ => false
            },
            CertTrust.PeerTrustLevel.Paired => true,
            _ => true
        };
    }

    private static bool IsHighRiskCommand(string cmd)
    {
        return cmd switch
        {
            "delete" => true,
            "power" => true,
            "delta_hashes" => true,
            "put_delta_blocks" => true,
            _ => false
        };
    }
}
