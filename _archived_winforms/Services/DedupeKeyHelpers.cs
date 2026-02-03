using System;
using System.IO;

namespace SlskDown.Services
{
    public static class DedupeKeyHelpers
    {
        public static string BuildRemotePathKey(string remotePath, long sizeBytes)
        {
            var pathPart = (remotePath ?? string.Empty).Trim().ToLowerInvariant();
            return $"{pathPart}|{sizeBytes}";
        }

        public static string BuildRemoteFileKey(string remotePathOrName, long sizeBytes, bool normalizeFileName)
        {
            var fileName = Path.GetFileName(remotePathOrName ?? string.Empty);
            var namePart = normalizeFileName
                ? NormalizeFileNameForDedupe(fileName)
                : (fileName ?? string.Empty).Trim().ToLowerInvariant();

            return $"{namePart}|{sizeBytes}";
        }

        public static string BuildProviderFileKey(string fileNameOrPath, string username, long sizeBytes, bool normalizeFileName)
        {
            var fileName = Path.GetFileName(fileNameOrPath ?? string.Empty);
            var namePart = normalizeFileName
                ? NormalizeFileNameForDedupe(fileName)
                : (fileName ?? string.Empty).Trim().ToLowerInvariant();
            var userPart = (username ?? string.Empty).Trim().ToLowerInvariant();
            return $"{namePart}|{userPart}|{sizeBytes}";
        }

        public static string NormalizeFileNameForDedupe(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            var normalized = fileName.Trim().ToLowerInvariant();
            normalized = normalized.Replace('_', ' ').Replace('.', ' ').Replace('-', ' ');
            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized;
        }
    }
}
