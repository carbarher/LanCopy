using System;
using System.Collections.Generic;

namespace SlskDown.Core
{
    public static class RustCore
    {
        public static bool IsAvailable() => false;
        
        public static string NormalizeAuthorName(string name) => name ?? "";
        
        public static List<string> GroupAuthorVariants(List<string> authors) => authors ?? new List<string>();
        
        public static string ValidateFilePath(string path) => path ?? "";
        
        public static bool ValidateFile(string path) => false;
        
        public static string ComputeHash(string path) => "";
        
        public static string ComputeBlake3(byte[] data) => "";
        
        public static List<string> FilterResults(List<string> results) => results ?? new List<string>();
        
        public static List<string> DeduplicateResults(List<string> results) => results ?? new List<string>();
        
        public static List<string> SortResults(List<string> results) => results ?? new List<string>();
        
        public static string NormalizeString(string input) => input ?? "";
        
        public static double ComputeSimilarity(string a, string b) => 0.0;
        
        public static bool IsSpanishContent(string content) => false;
    }
}
