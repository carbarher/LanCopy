using System;
using System.Collections.Generic;
using System.Linq;

namespace SlskDown.Services
{
    public sealed class PerformanceTracker
    {
        public sealed class MetricSnapshot
        {
            public string MetricName { get; set; }
            public long Count { get; set; }
            public int SampleCount { get; set; }
            public long P50 { get; set; }
            public long P90 { get; set; }
            public long P99 { get; set; }
        }

        private sealed class Metric
        {
            public readonly List<long> Samples = new List<long>();
            public long Count;
        }

        private readonly object gate = new object();
        private readonly Dictionary<string, Metric> metrics = new Dictionary<string, Metric>(StringComparer.OrdinalIgnoreCase);
        private readonly int maxSamplesPerMetric;

        public PerformanceTracker(int maxSamplesPerMetric = 2000)
        {
            this.maxSamplesPerMetric = Math.Max(100, maxSamplesPerMetric);
        }

        public static PerformanceTracker Instance { get; } = new PerformanceTracker();

        public void Reset()
        {
            lock (gate)
            {
                metrics.Clear();
            }
        }

        public List<MetricSnapshot> GetSnapshot()
        {
            lock (gate)
            {
                var snapshot = new List<MetricSnapshot>(metrics.Count);
                foreach (var kvp in metrics)
                {
                    var metricName = kvp.Key;
                    var metric = kvp.Value;
                    if (metric == null)
                    {
                        continue;
                    }

                    if (metric.Samples.Count == 0)
                    {
                        snapshot.Add(new MetricSnapshot
                        {
                            MetricName = metricName,
                            Count = metric.Count,
                            SampleCount = 0,
                            P50 = 0,
                            P90 = 0,
                            P99 = 0
                        });
                        continue;
                    }

                    var samples = metric.Samples.ToArray();
                    Array.Sort(samples);

                    snapshot.Add(new MetricSnapshot
                    {
                        MetricName = metricName,
                        Count = metric.Count,
                        SampleCount = samples.Length,
                        P50 = Percentile(samples, 0.50),
                        P90 = Percentile(samples, 0.90),
                        P99 = Percentile(samples, 0.99)
                    });
                }

                return snapshot.OrderBy(s => s.MetricName, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        public void Track(string metricName, long elapsedMs)
        {
            if (string.IsNullOrWhiteSpace(metricName))
            {
                return;
            }

            if (elapsedMs < 0)
            {
                elapsedMs = 0;
            }

            lock (gate)
            {
                if (!metrics.TryGetValue(metricName, out var metric))
                {
                    metric = new Metric();
                    metrics[metricName] = metric;
                }

                metric.Count++;
                metric.Samples.Add(elapsedMs);

                if (metric.Samples.Count > maxSamplesPerMetric)
                {
                    metric.Samples.RemoveAt(0);
                }
            }
        }

        public (long count, long p50, long p90, long p99) GetPercentiles(string metricName)
        {
            if (string.IsNullOrWhiteSpace(metricName))
            {
                return (0, 0, 0, 0);
            }

            lock (gate)
            {
                if (!metrics.TryGetValue(metricName, out var metric) || metric.Samples.Count == 0)
                {
                    return (0, 0, 0, 0);
                }

                var samples = metric.Samples.ToArray();
                Array.Sort(samples);

                long p50 = Percentile(samples, 0.50);
                long p90 = Percentile(samples, 0.90);
                long p99 = Percentile(samples, 0.99);

                return (metric.Count, p50, p90, p99);
            }
        }

        private static long Percentile(long[] sorted, double p)
        {
            if (sorted.Length == 0)
            {
                return 0;
            }

            if (sorted.Length == 1)
            {
                return sorted[0];
            }

            p = Math.Max(0, Math.Min(1, p));
            var idx = (int)Math.Round(p * (sorted.Length - 1));
            idx = Math.Max(0, Math.Min(sorted.Length - 1, idx));
            return sorted[idx];
        }
    }
}
