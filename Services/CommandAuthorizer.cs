namespace LanCopy.Services;

public static class CommandAuthorizer
{
    public static bool IsAllowed(string host, string cmd)
    {
        var trustLevel = CertTrust.GetTrustLevel(host);
        if (!PeerTrustPolicy.IsAllowed(trustLevel, cmd))
            return false;

        var permissions = PeerPermissionStore.Shared.Get(host);
        return IsAllowed(permissions, cmd);
    }

    public static bool IsAllowed(CertTrust.PeerTrustLevel trustLevel, PeerPermissionStore.Permissions permissions, string cmd)
    {
        if (!PeerTrustPolicy.IsAllowed(trustLevel, cmd))
            return false;

        return IsAllowed(permissions, cmd);
    }

    private static bool IsAllowed(PeerPermissionStore.Permissions permissions, string cmd)
    {
        return cmd switch
        {
            "list" or "search" or "stat" or "caps" or "health" => permissions.Browse,
            "get" or "get_chunk" or "sha1" or "sha256" or "hash" => permissions.Download,
            "put" or "put_resume" or "put_delta_blocks" => permissions.Upload,
            "rename" or "mkdir" => permissions.Modify,
            "delete" => permissions.Delete,
            "delta_hashes" => permissions.Sync,
            "text" => permissions.Browse,
            "power" => permissions.Power,
            "disconnect_notice" => true,
            _ => false
        };
    }
}
