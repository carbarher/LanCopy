using System;
using System.Collections.Generic;

namespace SlskDown
{
    public partial class MainForm
    {
        private sealed class LimitedSizeSpanishCache
        {
            private readonly int capacity;
            private readonly Dictionary<string, bool> values;
            private readonly Queue<string> order;

            public LimitedSizeSpanishCache(int capacity)
            {
                this.capacity = Math.Max(1, capacity);
                values = new Dictionary<string, bool>(StringComparer.Ordinal);
                order = new Queue<string>(this.capacity);
            }

            public bool TryGetValue(string key, out bool value) => values.TryGetValue(key, out value);

            public void Add(string key, bool value)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return;
                }

                if (values.ContainsKey(key))
                {
                    values[key] = value;
                    return;
                }

                if (values.Count >= capacity)
                {
                    var oldest = order.Dequeue();
                    values.Remove(oldest);
                }

                values[key] = value;
                order.Enqueue(key);
            }

            public void Clear()
            {
                values.Clear();
                order.Clear();
            }
        }

        private sealed class LimitedSizeSimpleCache<T>
            where T : class
        {
            private readonly int capacity;
            private readonly Dictionary<string, T> values;
            private readonly Queue<string> order;

            public LimitedSizeSimpleCache(int capacity)
            {
                this.capacity = Math.Max(1, capacity);
                values = new Dictionary<string, T>(StringComparer.Ordinal);
                order = new Queue<string>(this.capacity);
            }

            public bool TryGetValue(string key, out T value) => values.TryGetValue(key, out value);

            public void Add(string key, T value)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return;
                }

                if (values.ContainsKey(key))
                {
                    values[key] = value;
                    return;
                }

                if (values.Count >= capacity)
                {
                    var oldest = order.Dequeue();
                    values.Remove(oldest);
                }

                values[key] = value;
                order.Enqueue(key);
            }
        }
    }
}
