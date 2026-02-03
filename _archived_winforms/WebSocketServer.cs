using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown
{
    public class WebSocketServer
    {
        private HttpListener listener;
        private List<WebSocket> clients = new List<WebSocket>();
        private bool isRunning = false;
        private Action<string> logAction;
        
        public WebSocketServer(Action<string> logger)
        {
            logAction = logger;
        }
        
        public void Start(int port = 8081)
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/ws/");
                listener.Start();
                isRunning = true;
                
                logAction?.Invoke($"WebSocket server iniciado en ws://localhost:{port}/ws/");
                
                Task.Run(() => AcceptClients());
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error iniciando WebSocket: {ex.Message}");
            }
        }
        
        public void Stop()
        {
            isRunning = false;
            
            foreach (var client in clients.ToList())
            {
                client?.Dispose();
            }
            
            clients.Clear();
            listener?.Stop();
            listener?.Close();
            
            logAction?.Invoke("WebSocket server detenido");
        }
        
        private async Task AcceptClients()
        {
            while (isRunning)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    
                    if (context.Request.IsWebSocketRequest)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null);
                        var webSocket = wsContext.WebSocket;
                        
                        clients.Add(webSocket);
                        logAction?.Invoke($"Cliente WebSocket conectado (total: {clients.Count})");
                        
                        _ = Task.Run(() => HandleClient(webSocket));
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        logAction?.Invoke($"Error aceptando cliente WebSocket: {ex.Message}");
                    }
                }
            }
        }
        
        private async Task HandleClient(WebSocket webSocket)
        {
            var buffer = new byte[4096];
            
            try
            {
                while (webSocket.State == WebSocketState.Open && isRunning)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error en cliente WebSocket: {ex.Message}");
            }
            finally
            {
                clients.Remove(webSocket);
                webSocket?.Dispose();
                logAction?.Invoke($"Cliente WebSocket desconectado (total: {clients.Count})");
            }
        }
        
        public async Task BroadcastDownloadUpdate(object task)
        {
            if (!isRunning || clients.Count == 0) return;
            
            try
            {
                var message = JsonSerializer.Serialize(new
                {
                    type = "download_update",
                    timestamp = DateTime.Now,
                    data = task
                });
                
                var buffer = Encoding.UTF8.GetBytes(message);
                
                foreach (var client in clients.ToList())
                {
                    if (client.State == WebSocketState.Open)
                    {
                        await client.SendAsync(
                            new ArraySegment<byte>(buffer),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error broadcasting update: {ex.Message}");
            }
        }
        
        public async Task BroadcastSearchResults(List<object> results)
        {
            if (!isRunning || clients.Count == 0) return;
            
            try
            {
                var message = JsonSerializer.Serialize(new
                {
                    type = "search_results",
                    timestamp = DateTime.Now,
                    count = results.Count,
                    data = results
                });
                
                var buffer = Encoding.UTF8.GetBytes(message);
                
                foreach (var client in clients.ToList())
                {
                    if (client.State == WebSocketState.Open)
                    {
                        await client.SendAsync(
                            new ArraySegment<byte>(buffer),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error broadcasting search: {ex.Message}");
            }
        }
        
        public async Task BroadcastNotification(string title, string message, string type = "info")
        {
            if (!isRunning || clients.Count == 0) return;
            
            try
            {
                var msg = JsonSerializer.Serialize(new
                {
                    type = "notification",
                    timestamp = DateTime.Now,
                    title = title,
                    message = message,
                    notificationType = type
                });
                
                var buffer = Encoding.UTF8.GetBytes(msg);
                
                foreach (var client in clients.ToList())
                {
                    if (client.State == WebSocketState.Open)
                    {
                        await client.SendAsync(
                            new ArraySegment<byte>(buffer),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error broadcasting notification: {ex.Message}");
            }
        }
        
        public int GetConnectedClients()
        {
            return clients.Count(c => c.State == WebSocketState.Open);
        }
    }
}
