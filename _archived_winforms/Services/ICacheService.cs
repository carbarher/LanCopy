using System;

namespace SlskDown.Services
{
    public interface ICacheService
    {
        T? Get<T>(string key);
        void Set<T>(string key, T value, TimeSpan? expiration = null);
        void Remove(string key);
        void Clear();
        bool Contains(string key);
    }
}

