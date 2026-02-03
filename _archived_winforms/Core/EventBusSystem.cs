using System;
using System.Collections.Generic;

namespace SlskDown.Core
{
    /// <summary>
    /// Event Bus para comunicación desacoplada (estilo Nicotine+)
    /// </summary>
    public class EventBusSystem
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();
        private readonly object lockObj = new object();
        
        public void Subscribe<T>(Action<T> handler)
        {
            lock (lockObj)
            {
                var eventType = typeof(T);
                if (!subscribers.ContainsKey(eventType))
                    subscribers[eventType] = new List<Delegate>();
                
                subscribers[eventType].Add(handler);
            }
        }
        
        public void Unsubscribe<T>(Action<T> handler)
        {
            lock (lockObj)
            {
                var eventType = typeof(T);
                if (subscribers.ContainsKey(eventType))
                {
                    subscribers[eventType].Remove(handler);
                }
            }
        }
        
        public void Publish<T>(T eventData)
        {
            List<Delegate> handlers;
            
            lock (lockObj)
            {
                var eventType = typeof(T);
                if (!subscribers.ContainsKey(eventType))
                    return;
                
                handlers = new List<Delegate>(subscribers[eventType]);
            }
            
            foreach (var handler in handlers)
            {
                try
                {
                    ((Action<T>)handler)(eventData);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Event handler error: {ex.Message}");
                }
            }
        }
        
        public void Clear()
        {
            lock (lockObj)
            {
                subscribers.Clear();
            }
        }
        
        public int GetSubscriberCount<T>()
        {
            lock (lockObj)
            {
                var eventType = typeof(T);
                return subscribers.ContainsKey(eventType) ? subscribers[eventType].Count : 0;
            }
        }
    }
    
    // Eventos del sistema
    public class SearchCompletedEvent
    {
        public string Query { get; set; }
        public int ResultCount { get; set; }
        public TimeSpan Duration { get; set; }
    }
    
    public class DownloadStartedEvent
    {
        public string Filename { get; set; }
        public string Username { get; set; }
        public long Size { get; set; }
    }
    
    public class DownloadCompletedEvent
    {
        public string Filename { get; set; }
        public string Username { get; set; }
        public long Size { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
    }
    
    public class ConnectionStateChangedEvent
    {
        public bool IsConnected { get; set; }
        public string Reason { get; set; }
    }
}
