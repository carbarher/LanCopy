using System;
using System.Collections.Generic;

namespace SlskDown.Core
{
    public static class SlskDownCore
    {
        public static bool IsAvailable() => false;
        
        public static string ProcessFile(string path) => path ?? "";
        
        public static byte[] ComputeHash(string path) => Array.Empty<byte>();
        
        public static bool ValidateDownload(string path) => false;
        
        public static string GetFileMetadata(string path) => "";
    }
}
