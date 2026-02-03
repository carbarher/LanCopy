using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SlskDown
{
    public class SearchRequest
    {
        public string Query { get; set; }
        public int MaxResults { get; set; } = 100;
        public bool AutoDownload { get; set; } = false;
    }
    
    public class DownloadRequest
    {
        public string Username { get; set; }
        public string Filename { get; set; }
        public long Size { get; set; }
    }
    
    public class RestAPIServer
    {
        private HttpListener listener;
        private bool isRunning = false;
        private Action<string> logAction;
        private Func<string, Task<List<object>>> searchAction;
        private Func<List<object>> getDownloadsAction;
        private Func<Dictionary<string, object>> getStatsAction;
        private Func<string, string, long, Task> downloadAction;
        
        public RestAPIServer(
            Action<string> logger,
            Func<string, Task<List<object>>> search,
            Func<List<object>> downloads,
            Func<Dictionary<string, object>> stats,
            Func<string, string, long, Task> download)
        {
            logAction = logger;
            searchAction = search;
            getDownloadsAction = downloads;
            getStatsAction = stats;
            downloadAction = download;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // SERVER LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        public void Start(int port = 8080)
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                isRunning = true;
                
                logAction?.Invoke($"🌐 API REST iniciada en http://localhost:{port}/");
                
                Task.Run(() => HandleRequests());
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error iniciando API REST: {ex.Message}");
            }
        }
        
        public void Stop()
        {
            isRunning = false;
            listener?.Stop();
            listener?.Close();
            logAction?.Invoke("🌐 API REST detenida");
        }
        
        // ═══════════════════════════════════════════════════════════════
        // REQUEST HANDLING
        // ═══════════════════════════════════════════════════════════════
        
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
                    {
                        logAction?.Invoke($"❌ Error en API REST: {ex.Message}");
                    }
                }
            }
        }
        
        private async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            try
            {
                // CORS headers
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }
                
                var path = request.Url.AbsolutePath;
                var method = request.HttpMethod;
                
                logAction?.Invoke($"🌐 API: {method} {path}");
                
                // Routing
                if (path == "/api/search" && method == "POST")
                {
                    await HandleSearch(request, response);
                }
                else if (path == "/api/downloads" && method == "GET")
                {
                    await HandleGetDownloads(request, response);
                }
                else if (path == "/api/downloads" && method == "POST")
                {
                    await HandleAddDownload(request, response);
                }
                else if (path == "/api/stats" && method == "GET")
                {
                    await HandleGetStats(request, response);
                }
                else if (path == "/api/health" && method == "GET")
                {
                    await HandleHealth(request, response);
                }
                else
                {
                    response.StatusCode = 404;
                    await WriteJsonResponse(response, new { error = "Endpoint not found" });
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                await WriteJsonResponse(response, new { error = ex.Message });
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // ENDPOINTS
        // ═══════════════════════════════════════════════════════════════
        
        private async Task HandleSearch(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = await ReadRequestBody(request);
            var searchReq = JsonSerializer.Deserialize<SearchRequest>(body);
            
            if (string.IsNullOrEmpty(searchReq?.Query))
            {
                response.StatusCode = 400;
                await WriteJsonResponse(response, new { error = "Query is required" });
                return;
            }
            
            var results = await searchAction(searchReq.Query);
            
            response.StatusCode = 200;
            await WriteJsonResponse(response, new
            {
                query = searchReq.Query,
                count = results.Count,
                results = results.Take(searchReq.MaxResults).Select(r =>
                {
                    var file = r as dynamic;
                    return new
                    {
                        username = file?.Username?.ToString(),
                        filename = file?.FileName?.ToString(),
                        size = file?.Size ?? 0,
                        extension = file?.Extension?.ToString()
                    };
                }).ToList()
            });
        }
        
        private async Task HandleGetDownloads(HttpListenerRequest request, HttpListenerResponse response)
        {
            var downloads = getDownloadsAction();
            
            response.StatusCode = 200;
            await WriteJsonResponse(response, new
            {
                count = downloads.Count,
                downloads = downloads.Select(d =>
                {
                    var task = d as dynamic;
                    return new
                    {
                        username = task?.Username?.ToString(),
                        filename = task?.FileName?.ToString(),
                        size = task?.Size ?? 0,
                        status = task?.Status?.ToString(),
                        progress = task?.Progress ?? 0
                    };
                }).ToList()
            });
        }
        
        private async Task HandleAddDownload(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = await ReadRequestBody(request);
            var downloadReq = JsonSerializer.Deserialize<DownloadRequest>(body);
            
            if (string.IsNullOrEmpty(downloadReq?.Username) || string.IsNullOrEmpty(downloadReq?.Filename))
            {
                response.StatusCode = 400;
                await WriteJsonResponse(response, new { error = "Username and filename are required" });
                return;
            }
            
            await downloadAction(downloadReq.Username, downloadReq.Filename, downloadReq.Size);
            
            response.StatusCode = 201;
            await WriteJsonResponse(response, new
            {
                message = "Download added",
                username = downloadReq.Username,
                filename = downloadReq.Filename
            });
        }
        
        private async Task HandleGetStats(HttpListenerRequest request, HttpListenerResponse response)
        {
            var stats = getStatsAction();
            
            response.StatusCode = 200;
            await WriteJsonResponse(response, stats);
        }
        
        private async Task HandleHealth(HttpListenerRequest request, HttpListenerResponse response)
        {
            response.StatusCode = 200;
            await WriteJsonResponse(response, new
            {
                status = "healthy",
                timestamp = DateTime.Now,
                uptime = DateTime.Now - Process.GetCurrentProcess().StartTime
            });
        }
        
        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════
        
        private async Task<string> ReadRequestBody(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return await reader.ReadToEndAsync();
            }
        }
        
        private async Task WriteJsonResponse(HttpListenerResponse response, object data)
        {
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var buffer = Encoding.UTF8.GetBytes(json);
            
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // HELPER CLASS
    // ═══════════════════════════════════════════════════════════════
    
    public static class Process
    {
        public static System.Diagnostics.Process GetCurrentProcess()
        {
            return System.Diagnostics.Process.GetCurrentProcess();
        }
    }
}
