using System;

namespace SlskDown.Services
{
    public interface ILoggingService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? ex = null);
        void LogDebug(string message);
        
        // MÃ©todos de conveniencia (aliases)
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception? exception = null);
        void Debug(string message);
    }
}

