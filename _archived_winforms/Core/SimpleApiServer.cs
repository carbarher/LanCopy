using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Servidor HTTP simple para API REST sin dependencias externas
    /// </summary>
    public class SimpleApiServer
    {
        private HttpListener listener;
        private bool isRunning;
        private readonly int port;
        private readonly Func<string, object> getStatsFunc;
        private readonly Func<List<object>> getDownloadsFunc;
        private readonly Func<string, Task> startSearchFunc;
        private readonly Action<string> logFunc;
        
        public bool IsRunning => isRunning;
        public string BaseUrl => $"http://localhost:{port}/";
        
        public SimpleApiServer(
            int port,
            Func<string, object> getStats,
            Func<List<object>> getDownloads,
            Func<string, Task> startSearch,
            Action<string> log)
        {
            this.port = port;
            this.getStatsFunc = getStats;
            this.getDownloadsFunc = getDownloads;
            this.startSearchFunc = startSearch;
            this.logFunc = log;
        }
        
        public async Task Start()
        {
            if (isRunning) return;
            
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(BaseUrl);
                listener.Start();
                isRunning = true;
                
                logFunc?.Invoke($"API REST iniciada en {BaseUrl}");
                
                _ = Task.Run(async () => await HandleRequests());
            }
            catch (Exception ex)
            {
                logFunc?.Invoke($"Error iniciando API: {ex.Message}");
                throw;
            }
        }
        
        public void Stop()
        {
            if (!isRunning) return;
            
            isRunning = false;
            listener?.Stop();
            listener?.Close();
            logFunc?.Invoke("API REST detenida");
        }
        
        private async Task HandleRequests()
        {
            while (isRunning)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch (Exception ex)
                {
                    if (isRunning)
                        logFunc?.Invoke($"Error en API: {ex.Message}");
                }
            }
        }
        
        private async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            try
            {
                // CORS
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }
                
                var path = request.Url.AbsolutePath.ToLower();
                
                // Routing
                if (path == "/api/status" && request.HttpMethod == "GET")
                {
                    await HandleGetStatus(response);
                }
                else if (path == "/api/stats" && request.HttpMethod == "GET")
                {
                    await HandleGetStats(response);
                }
                else if (path == "/api/downloads" && request.HttpMethod == "GET")
                {
                    await HandleGetDownloads(response);
                }
                else if (path == "/api/search" && request.HttpMethod == "POST")
                {
                    await HandlePostSearch(request, response);
                }
                else if (path == "/")
                {
                    await HandleRoot(response);
                }
                else
                {
                    response.StatusCode = 404;
                    await WriteJson(response, new { error = "Endpoint no encontrado" });
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                await WriteJson(response, new { error = ex.Message });
            }
            finally
            {
                response.Close();
            }
        }
        
        private async Task HandleRoot(HttpListenerResponse response)
        {
            var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>p2p API</title>
    <style>
        body { font-family: Arial; margin: 40px; background: #1e1e1e; color: #fff; }
        h1 { color: #4CAF50; }
        .endpoint { background: #2d2d2d; padding: 15px; margin: 10px 0; border-radius: 5px; }
        .method { color: #4CAF50; font-weight: bold; }
        code { background: #000; padding: 2px 6px; border-radius: 3px; }
    </style>
</head>
<body>
    <h1>🚀 p2p API REST</h1>
    <p>API simple para control remoto de p2p.exe</p>
    
    <h2>Endpoints Disponibles:</h2>
    
    <div class='endpoint'>
        <span class='method'>GET</span> <code>/api/status</code>
        <p>Estado del servidor y conexión</p>
    </div>
    
    <div class='endpoint'>
        <span class='method'>GET</span> <code>/api/stats</code>
        <p>Estadísticas de descargas y búsquedas</p>
    </div>
    
    <div class='endpoint'>
        <span class='method'>GET</span> <code>/api/downloads</code>
        <p>Lista de descargas activas</p>
    </div>
    
    <div class='endpoint'>
        <span class='method'>POST</span> <code>/api/search</code>
        <p>Iniciar búsqueda (body: {""query"": ""término""})</p>
    </div>
    
    <p style='margin-top: 40px; color: #888;'>
        Ejemplo: <code>curl http://localhost:" + port + @"/api/status</code>
    </p>
</body>
</html>";
            
            response.ContentType = "text/html";
            var buffer = Encoding.UTF8.GetBytes(html);
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        
        private async Task HandleGetStatus(HttpListenerResponse response)
        {
            var status = new
            {
                status = "online",
                version = "1.0",
                timestamp = DateTime.Now,
                uptime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            await WriteJson(response, status);
        }
        
        private async Task HandleGetStats(HttpListenerResponse response)
        {
            var stats = getStatsFunc?.Invoke("all");
            await WriteJson(response, stats ?? new { error = "Stats no disponibles" });
        }
        
        private async Task HandleGetDownloads(HttpListenerResponse response)
        {
            var downloads = getDownloadsFunc?.Invoke();
            await WriteJson(response, downloads ?? new List<object>());
        }
        
        private async Task HandlePostSearch(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                var body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                
                if (data != null && data.TryGetValue("query", out var query))
                {
                    await startSearchFunc?.Invoke(query);
                    await WriteJson(response, new { success = true, query });
                }
                else
                {
                    response.StatusCode = 400;
                    await WriteJson(response, new { error = "Query requerido" });
                }
            }
        }
        
        private async Task WriteJson(HttpListenerResponse response, object data)
        {
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            var buffer = Encoding.UTF8.GetBytes(json);
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
    }
}
