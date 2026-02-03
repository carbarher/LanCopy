using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace SlskDown.Core
{
    // ═══════════════════════════════════════════════════════════════
    // LAZY LOADING
    // ═══════════════════════════════════════════════════════════════
    
    public class LazyDataLoader<T>
    {
        private readonly Func<int, int, List<T>> loadFunc;
        private readonly Dictionary<int, List<T>> cache = new Dictionary<int, List<T>>();
        private readonly int pageSize;
        private readonly int maxCachedPages;
        
        public LazyDataLoader(Func<int, int, List<T>> loadFunc, int pageSize = 100, int maxCachedPages = 10)
        {
            this.loadFunc = loadFunc;
            this.pageSize = pageSize;
            this.maxCachedPages = maxCachedPages;
        }
        
        public List<T> GetPage(int pageIndex)
        {
            if (cache.ContainsKey(pageIndex))
                return cache[pageIndex];
            
            var data = loadFunc(pageIndex * pageSize, pageSize);
            cache[pageIndex] = data;
            
            // Limpiar caché si es muy grande
            if (cache.Count > maxCachedPages)
            {
                var oldestPage = cache.Keys.Min();
                cache.Remove(oldestPage);
            }
            
            return data;
        }
        
        public void ClearCache()
        {
            cache.Clear();
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // ÍNDICES INVERTIDOS
    // ═══════════════════════════════════════════════════════════════
    
    public class SearchIndex
    {
        private readonly Dictionary<string, HashSet<int>> invertedIndex = new Dictionary<string, HashSet<int>>();
        private readonly Dictionary<int, object> items = new Dictionary<int, object>();
        private readonly Func<object, string> textExtractor;
        
        public SearchIndex(Func<object, string> textExtractor)
        {
            this.textExtractor = textExtractor;
        }
        
        public void AddItem(int id, object item)
        {
            items[id] = item;
            
            var text = textExtractor(item);
            var words = TokenizeText(text);
            
            foreach (var word in words)
            {
                if (!invertedIndex.ContainsKey(word))
                    invertedIndex[word] = new HashSet<int>();
                
                invertedIndex[word].Add(id);
            }
        }
        
        public void RemoveItem(int id)
        {
            if (!items.ContainsKey(id))
                return;
            
            var text = textExtractor(items[id]);
            var words = TokenizeText(text);
            
            foreach (var word in words)
            {
                if (invertedIndex.ContainsKey(word))
                {
                    invertedIndex[word].Remove(id);
                    if (invertedIndex[word].Count == 0)
                        invertedIndex.Remove(word);
                }
            }
            
            items.Remove(id);
        }
        
        public List<object> Search(string query)
        {
            var queryWords = TokenizeText(query);
            if (queryWords.Count == 0)
                return new List<object>();
            
            HashSet<int> resultIds = null;
            
            foreach (var word in queryWords)
            {
                if (!invertedIndex.ContainsKey(word))
                    return new List<object>();
                
                if (resultIds == null)
                    resultIds = new HashSet<int>(invertedIndex[word]);
                else
                    resultIds.IntersectWith(invertedIndex[word]);
            }
            
            return resultIds?.Select(id => items[id]).ToList() ?? new List<object>();
        }
        
        private List<string> TokenizeText(string text)
        {
            return text.ToLower()
                .Split(new[] { ' ', '_', '-', '.', '[', ']', '(', ')', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .Distinct()
                .ToList();
        }
        
        public void Clear()
        {
            invertedIndex.Clear();
            items.Clear();
        }
        
        public int ItemCount => items.Count;
        public int WordCount => invertedIndex.Count;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // MÉTRICAS CON PERCENTILES
    // ═══════════════════════════════════════════════════════════════
    
    public class MetricsCollector
    {
        private readonly List<double> values = new List<double>();
        private readonly int maxValues;
        
        public MetricsCollector(int maxValues = 10000)
        {
            this.maxValues = maxValues;
        }
        
        public void Record(double value)
        {
            values.Add(value);
            if (values.Count > maxValues)
                values.RemoveAt(0);
        }
        
        public double GetPercentile(double percentile)
        {
            if (values.Count == 0) return 0;
            
            var sorted = values.OrderBy(v => v).ToList();
            int index = (int)(sorted.Count * percentile);
            return sorted[Math.Min(index, sorted.Count - 1)];
        }
        
        public double P50 => GetPercentile(0.50);
        public double P95 => GetPercentile(0.95);
        public double P99 => GetPercentile(0.99);
        public double Min => values.Count > 0 ? values.Min() : 0;
        public double Max => values.Count > 0 ? values.Max() : 0;
        public double Average => values.Count > 0 ? values.Average() : 0;
        public int Count => values.Count;
        
        public void Clear()
        {
            values.Clear();
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // COMPRESIÓN DE DATOS
    // ═══════════════════════════════════════════════════════════════
    
    public static class CompressionHelper
    {
        public static byte[] Compress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    gzip.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }
        
        public static byte[] Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }
        
        public static string CompressString(string text)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            var compressed = Compress(bytes);
            return Convert.ToBase64String(compressed);
        }
        
        public static string DecompressString(string compressed)
        {
            var bytes = Convert.FromBase64String(compressed);
            var decompressed = Decompress(bytes);
            return System.Text.Encoding.UTF8.GetString(decompressed);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // COMMAND PATTERN (UNDO/REDO)
    // ═══════════════════════════════════════════════════════════════
    
    public interface ICommand
    {
        void Execute();
        void Undo();
        string Description { get; }
    }
    
    public class CommandHistory
    {
        private readonly Stack<ICommand> undoStack = new Stack<ICommand>();
        private readonly Stack<ICommand> redoStack = new Stack<ICommand>();
        private readonly int maxHistory;
        
        public CommandHistory(int maxHistory = 100)
        {
            this.maxHistory = maxHistory;
        }
        
        public void Execute(ICommand command)
        {
            command.Execute();
            undoStack.Push(command);
            redoStack.Clear();
            
            // Limitar historial
            if (undoStack.Count > maxHistory)
            {
                var items = undoStack.ToList();
                undoStack.Clear();
                for (int i = items.Count - maxHistory; i < items.Count; i++)
                    undoStack.Push(items[i]);
            }
        }
        
        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;
        
        public void Undo()
        {
            if (undoStack.Count > 0)
            {
                var command = undoStack.Pop();
                command.Undo();
                redoStack.Push(command);
            }
        }
        
        public void Redo()
        {
            if (redoStack.Count > 0)
            {
                var command = redoStack.Pop();
                command.Execute();
                undoStack.Push(command);
            }
        }
        
        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
        }
        
        public List<string> GetUndoHistory()
        {
            return undoStack.Select(c => c.Description).ToList();
        }
        
        public List<string> GetRedoHistory()
        {
            return redoStack.Select(c => c.Description).ToList();
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // POOL DE CONEXIONES
    // ═══════════════════════════════════════════════════════════════
    
    public class ConnectionPool<T> where T : class
    {
        private class PooledConnection
        {
            public T Connection { get; set; }
            public DateTime LastActivity { get; set; }
            public bool IsInUse { get; set; }
        }
        
        private readonly Dictionary<string, PooledConnection> connections = new Dictionary<string, PooledConnection>();
        private readonly Func<string, Task<T>> connectionFactory;
        private readonly Action<T> connectionDisposer;
        private readonly int maxIdleSeconds;
        private readonly object lockObj = new object();
        
        public ConnectionPool(
            Func<string, Task<T>> connectionFactory,
            Action<T> connectionDisposer,
            int maxIdleSeconds = 300)
        {
            this.connectionFactory = connectionFactory;
            this.connectionDisposer = connectionDisposer;
            this.maxIdleSeconds = maxIdleSeconds;
        }
        
        public async Task<T> GetOrCreateAsync(string key)
        {
            lock (lockObj)
            {
                if (connections.ContainsKey(key))
                {
                    var pooled = connections[key];
                    var idleTime = (DateTime.Now - pooled.LastActivity).TotalSeconds;
                    
                    if (!pooled.IsInUse && idleTime < maxIdleSeconds)
                    {
                        pooled.IsInUse = true;
                        pooled.LastActivity = DateTime.Now;
                        return pooled.Connection;
                    }
                    else if (idleTime >= maxIdleSeconds)
                    {
                        connectionDisposer(pooled.Connection);
                        connections.Remove(key);
                    }
                }
            }
            
            var newConnection = await connectionFactory(key);
            
            lock (lockObj)
            {
                connections[key] = new PooledConnection
                {
                    Connection = newConnection,
                    LastActivity = DateTime.Now,
                    IsInUse = true
                };
            }
            
            return newConnection;
        }
        
        public void Release(string key)
        {
            lock (lockObj)
            {
                if (connections.ContainsKey(key))
                {
                    connections[key].IsInUse = false;
                    connections[key].LastActivity = DateTime.Now;
                }
            }
        }
        
        public void CleanupIdle()
        {
            lock (lockObj)
            {
                var toRemove = connections.Where(kvp =>
                {
                    var idleTime = (DateTime.Now - kvp.Value.LastActivity).TotalSeconds;
                    return !kvp.Value.IsInUse && idleTime >= maxIdleSeconds;
                }).Select(kvp => kvp.Key).ToList();
                
                foreach (var key in toRemove)
                {
                    connectionDisposer(connections[key].Connection);
                    connections.Remove(key);
                }
            }
        }
        
        public void Clear()
        {
            lock (lockObj)
            {
                foreach (var conn in connections.Values)
                {
                    connectionDisposer(conn.Connection);
                }
                connections.Clear();
            }
        }
        
        public int ActiveCount
        {
            get
            {
                lock (lockObj)
                {
                    return connections.Count(kvp => kvp.Value.IsInUse);
                }
            }
        }
        
        public int TotalCount
        {
            get
            {
                lock (lockObj)
                {
                    return connections.Count;
                }
            }
        }
    }
}
