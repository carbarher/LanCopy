using System;

namespace SlskDown.Models
{
    // Stub para BookMetadata
    public class BookMetadata
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string ISBN { get; set; }
        public string Publisher { get; set; }
        public int? Year { get; set; }
        public string Description { get; set; }
    }

    // SearchResultItem ya está definido en UI\SearchResultsDataSource.cs
    // No se necesita stub aquí

    // Stub para RetryPolicy con CircuitBreaker
    public class RetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
        
        public class CircuitBreaker
        {
            public int FailureThreshold { get; set; } = 5;
            public TimeSpan OpenDuration { get; set; } = TimeSpan.FromMinutes(1);
        }
    }

    // Stub para CircuitBreakerPersistence
    public class CircuitBreakerPersistence
    {
        public void SaveState(string key, object state) { }
        public object LoadState(string key) { return null; }
    }
}

// Los stubs de SqliteConnection y MemoryCache fueron removidos
// porque ya existen en los paquetes NuGet instalados

// Stub para SlskDown.UI
namespace SlskDown.UI
{
    public class DummyUIClass { }
}
