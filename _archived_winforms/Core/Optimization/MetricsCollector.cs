using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Colector de métricas ligero para telemetría
    /// </summary>
    public class MetricsCollector
    {
        private readonly ConcurrentDictionary<string, long> counters;
        private readonly ConcurrentDictionary<string, MetricValue> gauges;
        private readonly ConcurrentDictionary<string, HistogramData> histograms;
        private readonly DateTime startTime;
        
        public MetricsCollector()
        {
            counters = new ConcurrentDictionary<string, long>();
            gauges = new ConcurrentDictionary<string, MetricValue>();
            histograms = new ConcurrentDictionary<string, HistogramData>();
            startTime = DateTime.Now;
        }
        
        private class MetricValue
        {
            public double Value { get; set; }
            public DateTime LastUpdated { get; set; }
        }
        
        private class HistogramData
        {
            public double Sum { get; set; }
            public int Count { get; set; }
            public double Min { get; set; } = double.MaxValue;
            public double Max { get; set; } = double.MinValue;
        }
        
        /// <summary>
        /// Incrementa contador
        /// </summary>
        public void IncrementCounter(string name, long value = 1)
        {
            counters.AddOrUpdate(name, value, (k, v) => v + value);
        }
        
        /// <summary>
        /// Establece valor de gauge
        /// </summary>
        public void SetGauge(string name, double value)
        {
            gauges[name] = new MetricValue
            {
                Value = value,
                LastUpdated = DateTime.Now
            };
        }
        
        /// <summary>
        /// Registra valor en histograma
        /// </summary>
        public void RecordValue(string name, double value)
        {
            histograms.AddOrUpdate(name,
                new HistogramData
                {
                    Sum = value,
                    Count = 1,
                    Min = value,
                    Max = value
                },
                (k, existing) =>
                {
                    existing.Sum += value;
                    existing.Count++;
                    existing.Min = Math.Min(existing.Min, value);
                    existing.Max = Math.Max(existing.Max, value);
                    return existing;
                });
        }
        
        /// <summary>
        /// Mide duración de operación
        /// </summary>
        public IDisposable MeasureDuration(string name)
        {
            return new DurationMeasurer(this, name);
        }
        
        private class DurationMeasurer : IDisposable
        {
            private readonly MetricsCollector collector;
            private readonly string name;
            private readonly DateTime startTime;
            
            public DurationMeasurer(MetricsCollector collector, string name)
            {
                this.collector = collector;
                this.name = name;
                this.startTime = DateTime.Now;
            }
            
            public void Dispose()
            {
                var duration = (DateTime.Now - startTime).TotalMilliseconds;
                collector.RecordValue(name, duration);
            }
        }
        
        /// <summary>
        /// Obtiene snapshot de todas las métricas
        /// </summary>
        public MetricsSnapshot GetSnapshot()
        {
            return new MetricsSnapshot
            {
                Timestamp = DateTime.Now,
                Uptime = DateTime.Now - startTime,
                Counters = counters.ToDictionary(k => k.Key, v => v.Value),
                Gauges = gauges.ToDictionary(k => k.Key, v => v.Value.Value),
                Histograms = histograms.ToDictionary(
                    k => k.Key,
                    v => new HistogramSnapshot
                    {
                        Count = v.Value.Count,
                        Sum = v.Value.Sum,
                        Average = v.Value.Count > 0 ? v.Value.Sum / v.Value.Count : 0,
                        Min = v.Value.Min,
                        Max = v.Value.Max
                    })
            };
        }
        
        /// <summary>
        /// Resetea todas las métricas
        /// </summary>
        public void Reset()
        {
            counters.Clear();
            gauges.Clear();
            histograms.Clear();
        }
        
        /// <summary>
        /// Resetea métrica específica
        /// </summary>
        public void ResetMetric(string name)
        {
            counters.TryRemove(name, out _);
            gauges.TryRemove(name, out _);
            histograms.TryRemove(name, out _);
        }
        
        public class MetricsSnapshot
        {
            public DateTime Timestamp { get; set; }
            public TimeSpan Uptime { get; set; }
            public Dictionary<string, long> Counters { get; set; }
            public Dictionary<string, double> Gauges { get; set; }
            public Dictionary<string, HistogramSnapshot> Histograms { get; set; }
        }
        
        public class HistogramSnapshot
        {
            public int Count { get; set; }
            public double Sum { get; set; }
            public double Average { get; set; }
            public double Min { get; set; }
            public double Max { get; set; }
        }
    }
}
