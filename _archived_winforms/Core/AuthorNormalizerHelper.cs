using System;

namespace SlskDown.Database
{
    public static class AuthorNormalizer
    {
        public static string Normalize(string author) => author?.Trim() ?? "";
        
        public static string NormalizeForSearch(string author) => author?.ToLowerInvariant().Trim() ?? "";
        
        public static string GetCanonicalName(string author) => author?.Trim() ?? "";
    }
}
