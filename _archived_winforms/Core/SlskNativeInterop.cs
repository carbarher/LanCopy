using System;

namespace SlskDown.Core
{
    public static class SlskNativeInterop
    {
        public static bool IsAvailable() => false;
        
        public static void Initialize() { }
        
        public static void Shutdown() { }
        
        public static string GetVersion() => "0.0.0";
        
        public static bool ProcessData(byte[] data) => false;
    }
}
