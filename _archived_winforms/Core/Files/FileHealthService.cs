using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SlskDown.Models;

namespace SlskDown.Core.Files
{
    public class FileHealthService
    {
        private readonly ConcurrentDictionary<string, HashSet<string>> _fileSources = new();

        public void RecordSource(string fileHash, string username)
        {
            var sources = _fileSources.GetOrAdd(fileHash, _ => new HashSet<string>());
            lock (sources)
            {
                sources.Add(username);
            }
        }

        public double CalculateHealth(string fileHash)
        {
            if (!_fileSources.TryGetValue(fileHash, out var sources)) return 0;

            int count = sources.Count;
            // Escala: 1 fuente = 20%, 5+ fuentes = 100%
            return Math.Min(100.0, count * 20.0);
        }

        public string GetHealthStatus(string fileHash)
        {
            double health = CalculateHealth(fileHash);
            return health switch
            {
                >= 80 => "Excelente",
                >= 60 => "Buena",
                >= 40 => "Media",
                >= 20 => "Baja",
                _ => "Crítica"
            };
        }
    }
}
