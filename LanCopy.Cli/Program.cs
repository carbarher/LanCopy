using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using LanCopy.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LanCopy.Cli;

internal static class Program
{
    private const int DefaultServerPort = 8742;
    private const int DefaultApiPort = 3489;
    private const string DefaultApiBaseUrl = "http://127.0.0.1:3489";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                PrintHelp();
                return 0;
            }

            return args[0].ToLowerInvariant() switch
            {
                "peers" => await RunPeersAsync(args[1..]),
                "send" => await RunSendAsync(args[1..]),
                "sync" => await RunSyncAsync(args[1..]),
                "transfer" => await RunTransferAsync(args[1..]),
                "cancel" => await RunCancelAsync(args[1..]),
                "retry" => await RunRetryAsync(args[1..]),
                "api" => await RunApiAsync(args[1..]),
                _ => FailUnknownCommand(args[0])
            };
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static async Task<int> RunPeersAsync(string[] args)
    {
        var waitSeconds = ParseIntOption(args, "--wait", 3, min: 1, max: 30);
        var json = HasFlag(args, "--json");

        var localIp = ResolveLocalIpv4();
        using var discovery = new PeerDiscovery(localIp, DefaultServerPort) { StealthMode = true };
        discovery.Start();

        await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
        var peers = discovery.GetPeers()
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Ip, StringComparer.Ordinal)
            .Select(ToPeerDto)
            .ToArray();

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(peers, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (peers.Length == 0)
        {
            Console.WriteLine("No peers found.");
            return 0;
        }

        foreach (var peer in peers)
            Console.WriteLine($"{peer.Name,-24} {peer.Ip}:{peer.Port}  lastSeen={peer.LastSeenUtc:O}");

        return 0;
    }

    private static async Task<int> RunSendAsync(string[] args)
    {
        var localPath = GetFirstPositional(args);
        if (string.IsNullOrWhiteSpace(localPath))
        {
            Console.Error.WriteLine("Missing source file path.");
            Console.Error.WriteLine("Usage: send <local-file> --to <ip[:port]> [--remote <path>] [--pin <pin>] [--json]");
            return 2;
        }

        if (!File.Exists(localPath))
        {
            Console.Error.WriteLine($"Source file not found: {localPath}");
            return 3;
        }

        var to = GetOption(args, "--to");
        if (string.IsNullOrWhiteSpace(to))
        {
            Console.Error.WriteLine("Missing required option: --to <ip[:port]>");
            return 2;
        }

        var endpoint = ParseEndpoint(to, DefaultServerPort);
        var remotePath = NormalizeRemotePath(GetOption(args, "--remote"), localPath);
        var pin = GetOption(args, "--pin");
        var json = HasFlag(args, "--json");
        var useTls = !HasFlag(args, "--no-tls");
        var useCompress = !HasFlag(args, "--no-compress");

        var fileInfo = new FileInfo(localPath);
        long doneBytes = 0;
        long totalBytes = fileInfo.Length;
        var progress = new Progress<(long done, long total)>(v =>
        {
            doneBytes = v.done;
            totalBytes = v.total;
            if (!json && totalBytes > 0)
            {
                var pct = (int)Math.Clamp((100.0 * doneBytes) / totalBytes, 0, 100);
                Console.Write($"\rUploading... {pct}% ({doneBytes}/{totalBytes} bytes)");
            }
        });

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            using var cli = new LanClient(endpoint.Host, endpoint.Port)
            {
                Pin = string.IsNullOrWhiteSpace(pin) ? null : pin,
                UseTls = useTls,
                UseCompress = useCompress
            };

            await cli.UploadAsync(localPath, remotePath, progress, cts.Token);
            if (!json) Console.WriteLine("\nUpload completed.");

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = "completed",
                    endpoint = endpoint.Raw,
                    remotePath,
                    doneBytes,
                    totalBytes
                }, new JsonSerializerOptions { WriteIndented = true }));
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            if (!json) Console.Error.WriteLine("\nUpload cancelled.");
            else Console.WriteLine(JsonSerializer.Serialize(new { status = "cancelled" }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
        catch (Exception ex)
        {
            if (!json)
                Console.Error.WriteLine($"Upload failed: {ex.Message}");
            else
                Console.WriteLine(JsonSerializer.Serialize(new { status = "failed", error = ex.Message }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
    }

    private static async Task<int> RunSyncAsync(string[] args)
    {
        var localDir = GetFirstPositional(args);
        if (string.IsNullOrWhiteSpace(localDir))
        {
            Console.Error.WriteLine("Missing source directory path.");
            Console.Error.WriteLine("Usage: sync <local-dir> --to <ip[:port]> [--remote-root <path>] [--pin <pin>] [--json]");
            return 2;
        }

        if (!Directory.Exists(localDir))
        {
            Console.Error.WriteLine($"Source directory not found: {localDir}");
            return 3;
        }

        var to = GetOption(args, "--to");
        if (string.IsNullOrWhiteSpace(to))
        {
            Console.Error.WriteLine("Missing required option: --to <ip[:port]>");
            return 2;
        }

        var endpoint = ParseEndpoint(to, DefaultServerPort);
        var remoteRoot = (GetOption(args, "--remote-root") ?? string.Empty).Trim();
        var pin = GetOption(args, "--pin");
        var json = HasFlag(args, "--json");
        var useTls = !HasFlag(args, "--no-tls");
        var useCompress = !HasFlag(args, "--no-compress");

        // Filter out reparse points (symlinks, junctions) — same policy as the GUI.
        var files = Directory.EnumerateFiles(localDir, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0; }
                catch { return false; }
            })
            .Select(path => new FileInfo(path))
            .ToArray();
        var totalBytes = files.Sum(f => f.Length);
        long doneBytes = 0;
        var filesDone = 0;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            using var cli = new LanClient(endpoint.Host, endpoint.Port)
            {
                Pin = string.IsNullOrWhiteSpace(pin) ? null : pin,
                UseTls = useTls,
                UseCompress = useCompress
            };

            foreach (var file in files)
            {
                cts.Token.ThrowIfCancellationRequested();

                var rel = Path.GetRelativePath(localDir, file.FullName).Replace('\\', '/');
                var remotePath = string.IsNullOrWhiteSpace(remoteRoot) ? rel : $"{remoteRoot.TrimEnd('/')}/{rel}";

                var fileBaseDone = doneBytes;
                var progress = new Progress<(long done, long total)>(v =>
                {
                    doneBytes = fileBaseDone + v.done;
                    if (!json && totalBytes > 0)
                    {
                        var pct = (int)Math.Clamp((100.0 * doneBytes) / totalBytes, 0, 100);
                        Console.Write($"\rSyncing... {pct}% ({filesDone}/{files.Length} files)");
                    }
                });

                await cli.UploadAsync(file.FullName, remotePath, progress, cts.Token);
                doneBytes = fileBaseDone + file.Length;
                filesDone++;
            }

            if (!json) Console.WriteLine("\nSync completed.");
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = "completed",
                    endpoint = endpoint.Raw,
                    totalFiles = files.Length,
                    filesDone,
                    totalBytes,
                    doneBytes
                }, new JsonSerializerOptions { WriteIndented = true }));
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            if (!json) Console.Error.WriteLine($"\nSync cancelled ({filesDone}/{files.Length} files done).");
            else Console.WriteLine(JsonSerializer.Serialize(new { status = "cancelled", filesDone, totalFiles = files.Length }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
        catch (Exception ex)
        {
            if (!json)
                Console.Error.WriteLine($"Sync failed: {ex.Message}");
            else
                Console.WriteLine(JsonSerializer.Serialize(new { status = "failed", error = ex.Message }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
    }

    private static Task<int> RunTransferAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Missing transfer subcommand.");
            Console.Error.WriteLine("Usage: transfer <cancel|retry|status> <id> [--api-url URL] [--token TOKEN] [--json]");
            return Task.FromResult(2);
        }

        return args[0].ToLowerInvariant() switch
        {
            "cancel" => RunCancelAsync(args[1..]),
            "retry" => RunRetryAsync(args[1..]),
            "status" => RunTransferStatusAsync(args[1..]),
            _ => Task.FromResult(FailUnknownCommand($"transfer {args[0]}"))
        };
    }

    private static Task<int> RunCancelAsync(string[] args)
        => RunTransferMutationAsync(args, action: "cancel");

    private static Task<int> RunRetryAsync(string[] args)
        => RunTransferMutationAsync(args, action: "retry");

    private static async Task<int> RunTransferStatusAsync(string[] args)
    {
        var transferId = GetFirstPositional(args);
        if (string.IsNullOrWhiteSpace(transferId))
        {
            Console.Error.WriteLine("Usage: transfer status <id> [--api-url URL] [--token TOKEN] [--json]");
            return 2;
        }

        var json = HasFlag(args, "--json");
        var apiBaseUrl = (GetOption(args, "--api-url") ?? DefaultApiBaseUrl).TrimEnd('/');
        var token = ResolveClientToken(GetOption(args, "--token"));
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("Missing API token. Pass --token or set LANCOPY_API_TOKEN.");
            return 2;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{apiBaseUrl}/api/v1/transfers/{Uri.EscapeDataString(transferId)}");
        req.Headers.Add("X-LanCopy-Token", token);
        using var resp = await http.SendAsync(req);
        var payload = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            if (json)
                Console.WriteLine(payload);
            else
                Console.Error.WriteLine($"Status query failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            return 1;
        }

        Console.WriteLine(payload);
        return 0;
    }

    private static async Task<int> RunTransferMutationAsync(string[] args, string action)
    {
        var transferId = GetFirstPositional(args);
        if (string.IsNullOrWhiteSpace(transferId))
        {
            Console.Error.WriteLine($"Usage: transfer {action} <id> [--api-url URL] [--token TOKEN] [--json]");
            return 2;
        }

        var json = HasFlag(args, "--json");
        var apiBaseUrl = (GetOption(args, "--api-url") ?? DefaultApiBaseUrl).TrimEnd('/');
        var token = ResolveClientToken(GetOption(args, "--token"));
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("Missing API token. Pass --token or set LANCOPY_API_TOKEN.");
            return 2;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{apiBaseUrl}/api/v1/transfers/{Uri.EscapeDataString(transferId)}/{action}");
        req.Headers.Add("X-LanCopy-Token", token);
        using var resp = await http.SendAsync(req);
        var payload = await resp.Content.ReadAsStringAsync();

        if ((int)resp.StatusCode is >= 200 and < 300)
        {
            if (json)
            {
                Console.WriteLine(payload);
            }
            else
            {
                if (action == "retry")
                {
                    var body = JsonSerializer.Deserialize<JsonElement>(payload);
                    var retryId = body.TryGetProperty("retryId", out var idEl) ? idEl.GetString() : null;
                    Console.WriteLine(string.IsNullOrWhiteSpace(retryId)
                        ? $"Retry requested for transfer {transferId}."
                        : $"Retry requested for transfer {transferId}. New transfer id: {retryId}");
                }
                else
                {
                    Console.WriteLine($"Cancellation requested for transfer {transferId}.");
                }
            }
            return 0;
        }

        if (json)
            Console.WriteLine(payload);
        else
            Console.Error.WriteLine($"{action} failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
        return 1;
    }

    private static async Task<int> RunApiAsync(string[] args)
    {
        var apiPort = ParseIntOption(args, "--port", DefaultApiPort, min: 1, max: 65535);
        var localIp = ResolveLocalIpv4();
        var providedToken = GetOption(args, "--token");
        var resetToken = HasFlag(args, "--reset-token");
        var token = ResolveApiServerToken(providedToken, resetToken, out var tokenSource);

        var builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.UseUrls($"http://127.0.0.1:{apiPort}");
        builder.Services.AddSingleton(new DiscoveryRuntime(localIp, DefaultServerPort));
        builder.Services.AddSingleton<TransferRuntime>();

        var app = builder.Build();
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api/v1")
                && !ctx.Request.Path.Equals("/api/v1/health", StringComparison.OrdinalIgnoreCase)
                && !ctx.Request.Path.Equals("/api/v1/openapi.json", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = ctx.Request.Headers["X-LanCopy-Token"].ToString();
                if (!string.Equals(candidate, token, StringComparison.Ordinal))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized", message = "Missing/invalid X-LanCopy-Token" });
                    return;
                }
            }
            await next();
        });

        app.MapGet("/api/v1/health", () => Results.Ok(new
        {
            status = "ok",
            serverTimeUtc = DateTimeOffset.UtcNow,
            version = typeof(PeerDiscovery).Assembly.GetName().Version?.ToString() ?? "unknown"
        }));

        app.MapGet("/api/v1/openapi.json", () => Results.Ok(BuildOpenApiDocument()));

        app.MapGet("/api/v1/peers", (DiscoveryRuntime rt) =>
        {
            var peers = rt.CurrentPeers().Select(ToPeerDto);
            return Results.Ok(peers);
        });

        app.MapPost("/api/v1/transfers/send", (SendRequest req, TransferRuntime tr) =>
        {
            var validation = ValidateSendRequest(req);
            if (validation is not null)
                return Results.BadRequest(new { error = validation });

            var id = tr.EnqueueSend(req);
            return Results.Accepted($"/api/v1/transfers/{id}", new { id, statusUrl = $"/api/v1/transfers/{id}" });
        });

        app.MapPost("/api/v1/sync", (SyncRequest req, TransferRuntime tr) =>
        {
            var validation = ValidateSyncRequest(req);
            if (validation is not null)
                return Results.BadRequest(new { error = validation });

            var id = tr.EnqueueSync(req);
            return Results.Accepted($"/api/v1/transfers/{id}", new { id, statusUrl = $"/api/v1/transfers/{id}" });
        });

        app.MapPost("/api/v1/transfers/{id}/cancel", (string id, TransferRuntime tr) =>
        {
            var result = tr.Cancel(id);
            return result switch
            {
                CancelResult.NotFound => Results.NotFound(new { error = "not_found" }),
                CancelResult.AlreadyTerminal => Results.Conflict(new { error = "already_terminal" }),
                CancelResult.CancellationRequested => Results.Accepted($"/api/v1/transfers/{id}", new { id, state = "cancellation_requested" }),
                _ => Results.Problem("Unexpected cancel result")
            };
        });

        app.MapPost("/api/v1/transfers/{id}/retry", (string id, TransferRuntime tr) =>
        {
            var result = tr.Retry(id);
            return result.Status switch
            {
                RetryStatus.NotFound => Results.NotFound(new { error = "not_found" }),
                RetryStatus.NotTerminal => Results.Conflict(new { error = "not_terminal" }),
                RetryStatus.SourceMissing => Results.Conflict(new { error = "source_missing", message = result.Error }),
                RetryStatus.UnsupportedKind => Results.Conflict(new { error = "unsupported_kind" }),
                RetryStatus.Enqueued => Results.Accepted($"/api/v1/transfers/{result.RetryId}", new { id, retryId = result.RetryId, statusUrl = $"/api/v1/transfers/{result.RetryId}" }),
                _ => Results.Problem("Unexpected retry result")
            };
        });

        app.MapGet("/api/v1/transfers/{id}", (string id, TransferRuntime tr) =>
        {
            var status = tr.GetStatus(id);
            return status is null
                ? Results.NotFound(new { error = "not_found" })
                : Results.Ok(status);
        });

        app.MapGet("/api/v1/events", async (HttpContext ctx, TransferRuntime tr) =>
        {
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";
            ctx.Response.ContentType = "text/event-stream";

            await foreach (var ev in tr.ReadEvents(ctx.RequestAborted))
            {
                var payload = JsonSerializer.Serialize(ev);
                await ctx.Response.WriteAsync($"event: {ev.Type}\n", ctx.RequestAborted);
                await ctx.Response.WriteAsync($"data: {payload}\n\n", ctx.RequestAborted);
                await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
            }
        });

        var discovery = app.Services.GetRequiredService<DiscoveryRuntime>();
        await discovery.StartAsync();

        var transfers = app.Services.GetRequiredService<TransferRuntime>();

        app.Lifetime.ApplicationStopping.Register(discovery.Dispose);
        app.Lifetime.ApplicationStopping.Register(transfers.Dispose);

        Console.WriteLine($"LanCopy API listening on http://127.0.0.1:{apiPort}");
        Console.WriteLine("Use header: X-LanCopy-Token");
        Console.WriteLine($"Token source: {tokenSource}");
        Console.WriteLine($"Token: {token}");
        Console.WriteLine("Endpoints: GET /api/v1/health, GET /api/v1/openapi.json, GET /api/v1/peers, POST /api/v1/transfers/send, POST /api/v1/sync, POST /api/v1/transfers/{id}/cancel, POST /api/v1/transfers/{id}/retry, GET /api/v1/transfers/{id}, GET /api/v1/events");

        await app.RunAsync();
        return 0;
    }

    private static string? ValidateSendRequest(SendRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LocalPath)) return "localPath is required";
        if (!File.Exists(req.LocalPath)) return "localPath not found";
        if (string.IsNullOrWhiteSpace(req.To)) return "to is required";
        try { _ = ParseEndpoint(req.To, DefaultServerPort); }
        catch (Exception ex) { return ex.Message; }
        return null;
    }

    private static string? ValidateSyncRequest(SyncRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LocalDir)) return "localDir is required";
        if (!Directory.Exists(req.LocalDir)) return "localDir not found";
        if (string.IsNullOrWhiteSpace(req.To)) return "to is required";
        try { _ = ParseEndpoint(req.To, DefaultServerPort); }
        catch (Exception ex) { return ex.Message; }
        return null;
    }

    private static PeerDto ToPeerDto(PeerDiscovery.PeerInfo p)
        => new()
        {
            Name = p.Name,
            Ip = p.Ip,
            Port = p.Port,
            LastSeenUtc = DateTimeOffset.UtcNow.AddMilliseconds(-(Environment.TickCount64 - p.LastSeen))
        };

    private static string ResolveLocalIpv4()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            return ip?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static bool HasFlag(IEnumerable<string> args, string name)
        => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static string? GetFirstPositional(IEnumerable<string> args)
        => args.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));

    private static int ParseIntOption(string[] args, string name, int defaultValue, int min, int max)
    {
        var raw = GetOption(args, name);
        if (raw is null) return defaultValue;
        if (!int.TryParse(raw, out var value) || value < min || value > max)
            throw new ArgumentOutOfRangeException(name, $"Expected integer between {min} and {max}.");
        return value;
    }

    internal static Endpoint ParseEndpoint(string raw, int defaultPort)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Endpoint is empty.");

        var host = trimmed;
        var port = defaultPort;

        var firstColon = trimmed.IndexOf(':');
        var lastColon = trimmed.LastIndexOf(':');
        if (firstColon > 0 && firstColon == lastColon)
        {
            host = trimmed[..firstColon];
            var portText = trimmed[(firstColon + 1)..];
            if (!int.TryParse(portText, out port) || port is < 1 or > 65535)
                throw new ArgumentException("Invalid endpoint port. Use 1..65535.");
        }

        if (host.Length == 0)
            throw new ArgumentException("Endpoint host is empty.");

        return new Endpoint(host, port, raw);
    }

    internal static string NormalizeRemotePath(string? remotePath, string localPath)
    {
        if (!string.IsNullOrWhiteSpace(remotePath)) return remotePath;
        var fileName = Path.GetFileName(localPath);
        return string.IsNullOrWhiteSpace(fileName) ? localPath : fileName;
    }

    internal static string ResolveClientToken(string? cliToken)
    {
        if (!string.IsNullOrWhiteSpace(cliToken))
            return cliToken.Trim();

        var envToken = Environment.GetEnvironmentVariable("LANCOPY_API_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
            return envToken.Trim();

        if (ApiTokenStore.TryLoad(out var persisted))
            return persisted;

        return string.Empty;
    }

    private static string ResolveApiServerToken(string? providedToken, bool resetToken, out string source)
    {
        if (!string.IsNullOrWhiteSpace(providedToken))
        {
            var token = providedToken.Trim();
            ApiTokenStore.Save(token);
            source = "command-line";
            return token;
        }

        if (resetToken)
        {
            var token = GenerateToken();
            ApiTokenStore.Save(token);
            source = "generated-reset";
            return token;
        }

        var envToken = Environment.GetEnvironmentVariable("LANCOPY_API_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            source = "environment";
            return envToken.Trim();
        }

        if (ApiTokenStore.TryLoad(out var persisted))
        {
            source = "persisted";
            return persisted;
        }

        var generated = GenerateToken();
        ApiTokenStore.Save(generated);
        source = "generated";
        return generated;
    }

    private static string GenerateToken()
        => Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static object BuildOpenApiDocument()
    {
        const string desc = "LanCopy local API (localhost only). All /api/v1/* endpoints except health/openapi require X-LanCopy-Token.";
        return new
        {
            openapi = "3.0.3",
            info = new { title = "LanCopy Local API", version = "v1", description = desc },
            servers = new[] { new { url = "http://127.0.0.1:3489" } },
            components = new
            {
                securitySchemes = new
                {
                    lanCopyToken = new { type = "apiKey", @in = "header", name = "X-LanCopy-Token" }
                }
            },
            security = new[] { new Dictionary<string, string[]> { ["lanCopyToken"] = Array.Empty<string>() } },
            paths = new Dictionary<string, object>
            {
                ["/api/v1/health"] = new
                {
                    get = new
                    {
                        summary = "Health status",
                        security = Array.Empty<object>(),
                        responses = new Dictionary<string, object> { ["200"] = new { description = "OK" } }
                    }
                },
                ["/api/v1/openapi.json"] = new
                {
                    get = new
                    {
                        summary = "OpenAPI document",
                        security = Array.Empty<object>(),
                        responses = new Dictionary<string, object> { ["200"] = new { description = "OpenAPI JSON" } }
                    }
                },
                ["/api/v1/peers"] = new
                {
                    get = new
                    {
                        summary = "List discovered peers",
                        responses = new Dictionary<string, object> { ["200"] = new { description = "Peer list" } }
                    }
                },
                ["/api/v1/transfers/send"] = new
                {
                    post = new
                    {
                        summary = "Enqueue send transfer",
                        requestBody = new
                        {
                            required = true,
                            content = new Dictionary<string, object>
                            {
                                ["application/json"] = new
                                {
                                    example = new { localPath = @"C:\tmp\file.zip", to = "192.168.1.50:8742", remotePath = "file.zip", pin = "1234" }
                                }
                            }
                        },
                        responses = new Dictionary<string, object> { ["202"] = new { description = "Transfer queued" } }
                    }
                },
                ["/api/v1/sync"] = new
                {
                    post = new
                    {
                        summary = "Enqueue sync transfer",
                        requestBody = new
                        {
                            required = true,
                            content = new Dictionary<string, object>
                            {
                                ["application/json"] = new
                                {
                                    example = new { localDir = @"C:\data", to = "192.168.1.50:8742", remoteRoot = "backup", pin = "1234" }
                                }
                            }
                        },
                        responses = new Dictionary<string, object> { ["202"] = new { description = "Transfer queued" } }
                    }
                },
                ["/api/v1/transfers/{id}"] = new
                {
                    get = new
                    {
                        summary = "Get transfer status",
                        parameters = new[] { new { name = "id", @in = "path", required = true, schema = new { type = "string" } } },
                        responses = new Dictionary<string, object> { ["200"] = new { description = "Transfer status" }, ["404"] = new { description = "Not found" } }
                    }
                },
                ["/api/v1/transfers/{id}/cancel"] = new
                {
                    post = new
                    {
                        summary = "Request cancellation",
                        parameters = new[] { new { name = "id", @in = "path", required = true, schema = new { type = "string" } } },
                        responses = new Dictionary<string, object> { ["202"] = new { description = "Cancellation requested" }, ["409"] = new { description = "Already terminal" }, ["404"] = new { description = "Not found" } }
                    }
                },
                ["/api/v1/transfers/{id}/retry"] = new
                {
                    post = new
                    {
                        summary = "Retry a terminal transfer",
                        parameters = new[] { new { name = "id", @in = "path", required = true, schema = new { type = "string" } } },
                        responses = new Dictionary<string, object> { ["202"] = new { description = "Retry queued" }, ["409"] = new { description = "Cannot retry current state" }, ["404"] = new { description = "Not found" } }
                    }
                },
                ["/api/v1/events"] = new
                {
                    get = new
                    {
                        summary = "Server-Sent Events stream",
                        responses = new Dictionary<string, object> { ["200"] = new { description = "SSE stream" } }
                    }
                }
            }
        };
    }

    private static int FailUnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}");
        Console.Error.WriteLine();
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("LanCopy CLI (preview)");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  peers [--wait N] [--json]                                 Discover peers on LAN");
        Console.WriteLine("  send <local-file> --to <ip[:port]> [--remote <path>]     Upload file to remote peer");
        Console.WriteLine("       [--pin <pin>] [--no-tls] [--no-compress] [--json]");
        Console.WriteLine("  sync <local-dir> --to <ip[:port]> [--remote-root <path>] Sync local directory to remote peer");
        Console.WriteLine("       [--pin <pin>] [--no-tls] [--no-compress] [--json]");
        Console.WriteLine("  transfer status <id> [--api-url URL] [--token TOKEN]     Query transfer status");
        Console.WriteLine("  transfer cancel <id> [--api-url URL] [--token TOKEN]     Cancel transfer via local API");
        Console.WriteLine("  transfer retry <id> [--api-url URL] [--token TOKEN]      Retry terminal transfer via local API");
        Console.WriteLine("  api [--port N] [--token VALUE] [--reset-token]           Start local API on 127.0.0.1");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  lancopy-cli peers --wait 5 --json");
        Console.WriteLine("  lancopy-cli send C:\\tmp\\a.zip --to 192.168.1.50:8742 --pin 1234");
        Console.WriteLine("  lancopy-cli sync C:\\data --to 192.168.1.50:8742 --remote-root backup");
        Console.WriteLine("  lancopy-cli transfer cancel 9f5f... --token my-secret-token");
        Console.WriteLine("  lancopy-cli transfer retry 9f5f... --token my-secret-token");
        Console.WriteLine("  lancopy-cli api --port 3489 --reset-token");
    }
}

internal sealed class DiscoveryRuntime : IDisposable
{
    private readonly PeerDiscovery _discovery;

    public DiscoveryRuntime(string localIp, int tcpPort)
    {
        _discovery = new PeerDiscovery(localIp, tcpPort) { StealthMode = true };
    }

    public Task StartAsync()
    {
        _discovery.Start();
        return Task.CompletedTask;
    }

    public IReadOnlyList<PeerDiscovery.PeerInfo> CurrentPeers() => _discovery.GetPeers();

    public void Dispose() => _discovery.Dispose();
}

internal static class ApiTokenStore
{
    private static readonly string TokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LanCopy",
        "cli-api.json");

    public static bool TryLoad(out string token)
    {
        token = string.Empty;
        if (!File.Exists(TokenPath))
            return false;

        try
        {
            var json = File.ReadAllText(TokenPath);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            if (!doc.TryGetProperty("token", out var tokenEl))
                return false;

            var value = tokenEl.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            token = value;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static void Save(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Token cannot be empty.", nameof(token));

        var dir = Path.GetDirectoryName(TokenPath)!;
        Directory.CreateDirectory(dir);

        var payload = JsonSerializer.Serialize(new
        {
            token = trimmed,
            updatedUtc = DateTimeOffset.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true });

        // Temp con GUID — evita colisión si dos procesos CLI llaman Save concurrentemente
        // (mismo patrón defensivo que JsonStore.WriteRawAtomic y CertTrust.Save).
        var tempPath = TokenPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(tempPath, payload);
        File.Move(tempPath, TokenPath, overwrite: true);
    }
}

internal enum CancelResult
{
    NotFound,
    AlreadyTerminal,
    CancellationRequested
}

internal enum RetryStatus
{
    NotFound,
    NotTerminal,
    UnsupportedKind,
    SourceMissing,
    Enqueued
}

internal readonly record struct RetryResult(RetryStatus Status, string? RetryId = null, string? Error = null);

internal sealed class TransferRuntime : IDisposable
{
    private readonly ConcurrentDictionary<string, TransferJob> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<TransferEvent> _events = Channel.CreateBounded<TransferEvent>(new BoundedChannelOptions(2048)
    {
        SingleWriter = false,
        SingleReader = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly object _persistLock = new();
    private readonly string _persistPath;
    private DateTimeOffset _lastPersistAtUtc = DateTimeOffset.MinValue;
    private static readonly TimeSpan PersistProgressInterval = TimeSpan.FromSeconds(1);

    public TransferRuntime() : this(persistPathOverride: null)
    {
    }

    internal TransferRuntime(string? persistPathOverride)
    {
        if (string.IsNullOrWhiteSpace(persistPathOverride))
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LanCopy");
            Directory.CreateDirectory(dir);
            _persistPath = Path.Combine(dir, "cli-transfer-jobs.json");
        }
        else
        {
            var persistDir = Path.GetDirectoryName(persistPathOverride);
            if (string.IsNullOrWhiteSpace(persistDir))
                throw new ArgumentException("Persist path must include a directory.", nameof(persistPathOverride));
            Directory.CreateDirectory(persistDir);
            _persistPath = persistPathOverride;
        }
        LoadPersistedJobs();
    }

    public string EnqueueSend(SendRequest req)
    {
        var endpoint = Program.ParseEndpoint(req.To!, 8742);
        var remotePath = Program.NormalizeRemotePath(req.RemotePath, req.LocalPath!);
        var pin = string.IsNullOrWhiteSpace(req.Pin) ? null : req.Pin;
        var cts = new CancellationTokenSource();

        var status = new TransferStatus
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = "send",
            State = "queued",
            LocalPath = req.LocalPath!,
            RemotePath = remotePath,
            To = endpoint.Raw,
            UseTls = req.UseTls ?? true,
            UseCompress = req.UseCompress ?? true,
            StartedUtc = DateTimeOffset.UtcNow
        };

        var job = new TransferJob(status, cts, pin);
        _jobs[status.Id] = job;
        Publish("queued", status);

        _ = Task.Run(async () =>
        {
            SetState(status, "running");
            try
            {
                var info = new FileInfo(status.LocalPath);
                status.TotalBytes = info.Exists ? info.Length : 0;

                using var cli = new LanClient(endpoint.Host, endpoint.Port)
                {
                    Pin = pin,
                    UseTls = status.UseTls,
                    UseCompress = status.UseCompress
                };

                var progress = new Progress<(long done, long total)>(v =>
                {
                    status.DoneBytes = v.done;
                    status.TotalBytes = v.total;
                    Publish("progress", status);
                });

                await cli.UploadAsync(status.LocalPath, status.RemotePath, progress, cts.Token);
                SetState(status, "completed");
            }
            catch (OperationCanceledException)
            {
                SetState(status, "canceled");
            }
            catch (Exception ex)
            {
                status.Error = ex.Message;
                SetState(status, "failed");
            }
            finally
            {
                status.FinishedUtc = DateTimeOffset.UtcNow;
                Publish("final", status);
                cts.Dispose();
            }
        });

        return status.Id;
    }

    public string EnqueueSync(SyncRequest req)
    {
        var endpoint = Program.ParseEndpoint(req.To!, 8742);
        var remoteRoot = (req.RemoteRoot ?? string.Empty).Trim();
        var pin = string.IsNullOrWhiteSpace(req.Pin) ? null : req.Pin;
        var cts = new CancellationTokenSource();

        var status = new TransferStatus
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = "sync",
            State = "queued",
            LocalPath = req.LocalDir!,
            RemotePath = remoteRoot,
            To = endpoint.Raw,
            UseTls = req.UseTls ?? true,
            UseCompress = req.UseCompress ?? true,
            StartedUtc = DateTimeOffset.UtcNow
        };

        var job = new TransferJob(status, cts, pin);
        _jobs[status.Id] = job;
        Publish("queued", status);

        _ = Task.Run(async () =>
        {
            SetState(status, "running");
            try
            {
                var files = Directory.EnumerateFiles(status.LocalPath, "*", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .ToArray();

                status.TotalFiles = files.Length;
                status.TotalBytes = files.Sum(f => f.Length);
                Publish("progress", status);

                using var cli = new LanClient(endpoint.Host, endpoint.Port)
                {
                    Pin = pin,
                    UseTls = status.UseTls,
                    UseCompress = status.UseCompress
                };

                foreach (var file in files)
                {
                    cts.Token.ThrowIfCancellationRequested();

                    var rel = Path.GetRelativePath(status.LocalPath, file.FullName).Replace('\\', '/');
                    var remotePath = string.IsNullOrWhiteSpace(remoteRoot) ? rel : $"{remoteRoot.TrimEnd('/')}/{rel}";

                    var baseDone = status.DoneBytes;
                    var progress = new Progress<(long done, long total)>(v =>
                    {
                        status.DoneBytes = baseDone + v.done;
                        Publish("progress", status);
                    });

                    await cli.UploadAsync(file.FullName, remotePath, progress, cts.Token);
                    status.DoneBytes = baseDone + file.Length;
                    status.DoneFiles++;
                    Publish("progress", status);
                }

                SetState(status, "completed");
            }
            catch (OperationCanceledException)
            {
                SetState(status, "canceled");
            }
            catch (Exception ex)
            {
                status.Error = ex.Message;
                SetState(status, "failed");
            }
            finally
            {
                status.FinishedUtc = DateTimeOffset.UtcNow;
                Publish("final", status);
                cts.Dispose();
            }
        });

        return status.Id;
    }

    public CancelResult Cancel(string id)
    {
        if (!_jobs.TryGetValue(id, out var job))
            return CancelResult.NotFound;

        if (IsTerminal(job.Status.State))
            return CancelResult.AlreadyTerminal;

        job.Status.CancellationRequested = true;
        Publish("cancel_requested", job.Status);
        // Race: the job's finally{cts.Dispose()} may have already run between the IsTerminal
        // check above and this Cancel() call — guard against ObjectDisposedException.
        try { job.Cancellation?.Cancel(); }
        catch (ObjectDisposedException) { /* job completed concurrently; cancellation is moot */ }
        return CancelResult.CancellationRequested;
    }

    public RetryResult Retry(string id)
    {
        if (!_jobs.TryGetValue(id, out var job))
            return new RetryResult(RetryStatus.NotFound);

        if (!IsTerminal(job.Status.State))
            return new RetryResult(RetryStatus.NotTerminal);

        if (job.Status.Kind.Equals("send", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(job.Status.LocalPath))
                return new RetryResult(RetryStatus.SourceMissing, Error: $"Source file not found: {job.Status.LocalPath}");

            var retryId = EnqueueSend(new SendRequest
            {
                LocalPath = job.Status.LocalPath,
                To = job.Status.To,
                RemotePath = job.Status.RemotePath,
                Pin = job.Pin,
                UseTls = job.Status.UseTls,
                UseCompress = job.Status.UseCompress
            });
            return new RetryResult(RetryStatus.Enqueued, RetryId: retryId);
        }

        if (job.Status.Kind.Equals("sync", StringComparison.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(job.Status.LocalPath))
                return new RetryResult(RetryStatus.SourceMissing, Error: $"Source directory not found: {job.Status.LocalPath}");

            var retryId = EnqueueSync(new SyncRequest
            {
                LocalDir = job.Status.LocalPath,
                To = job.Status.To,
                RemoteRoot = job.Status.RemotePath,
                Pin = job.Pin,
                UseTls = job.Status.UseTls,
                UseCompress = job.Status.UseCompress
            });
            return new RetryResult(RetryStatus.Enqueued, RetryId: retryId);
        }

        return new RetryResult(RetryStatus.UnsupportedKind);
    }

    public TransferStatus? GetStatus(string id)
        => _jobs.TryGetValue(id, out var job) ? job.Status.Clone() : null;

    public IAsyncEnumerable<TransferEvent> ReadEvents(CancellationToken ct)
        => _events.Reader.ReadAllAsync(ct);

    private void SetState(TransferStatus status, string state)
    {
        status.State = state;
        Publish("state", status);
    }

    private void Publish(string type, TransferStatus status)
    {
        var snapshot = status.Clone();
        _events.Writer.TryWrite(new TransferEvent
        {
            Type = type,
            TimestampUtc = DateTimeOffset.UtcNow,
            Job = snapshot
        });

        PersistSnapshotThrottled(force: type != "progress");
    }

    private void PersistSnapshotThrottled(bool force)
    {
        if (!force)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastPersistAtUtc < PersistProgressInterval)
                return;
        }

        PersistSnapshot();
        _lastPersistAtUtc = DateTimeOffset.UtcNow;
    }

    private void PersistSnapshot()
    {
        lock (_persistLock)
        {
            var list = _jobs.Values
                .Select(j => j.Status.Clone())
                .OrderByDescending(j => j.StartedUtc)
                .Take(500)
                .ToArray();

            var temp = _persistPath + ".tmp";
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temp, json);
            File.Move(temp, _persistPath, overwrite: true);
        }
    }

    private void LoadPersistedJobs()
    {
        if (!File.Exists(_persistPath)) return;

        try
        {
            var json = File.ReadAllText(_persistPath);
            var list = JsonSerializer.Deserialize<List<TransferStatus>>(json) ?? [];
            foreach (var status in list)
            {
                if (!IsTerminal(status.State))
                {
                    status.State = "failed";
                    status.Error = "Recovered after service restart";
                    status.FinishedUtc ??= DateTimeOffset.UtcNow;
                }

                _jobs[status.Id] = new TransferJob(status, cancellation: null, pin: null);
            }
        }
        catch (Exception ex)
        {
            // Preserve startup robustness; broken persistence file should not stop API boot.
            Log.Warn("cli", "transfer-snapshot-load-failed", new { path = _persistPath, error = ex.Message });
        }
    }

    private static bool IsTerminal(string state)
        => state is "completed" or "failed" or "canceled";

    public void Dispose()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.Cancellation is null) continue;
            try { job.Cancellation.Cancel(); }
            catch (ObjectDisposedException)
            {
                // Job already finished and disposed its CTS.
            }
        }

        try { PersistSnapshot(); }
        catch (Exception ex) { Log.Warn("cli", "dispose-persist-failed", new { error = ex.Message }); }
        _events.Writer.TryComplete();
    }
}

internal sealed class TransferJob
{
    public TransferJob(TransferStatus status, CancellationTokenSource? cancellation, string? pin)
    {
        Status = status;
        Cancellation = cancellation;
        Pin = pin;
    }

    public TransferStatus Status { get; }
    public CancellationTokenSource? Cancellation { get; }
    public string? Pin { get; }
}

internal sealed class SendRequest
{
    public string? LocalPath { get; set; }
    public string? To { get; set; }
    public string? RemotePath { get; set; }
    public string? Pin { get; set; }
    public bool? UseTls { get; set; }
    public bool? UseCompress { get; set; }
}

internal sealed class SyncRequest
{
    public string? LocalDir { get; set; }
    public string? To { get; set; }
    public string? RemoteRoot { get; set; }
    public string? Pin { get; set; }
    public bool? UseTls { get; set; }
    public bool? UseCompress { get; set; }
}

internal sealed class PeerDto
{
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Port { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
}

internal sealed class TransferStatus
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string State { get; set; } = "queued";
    public string LocalPath { get; set; } = "";
    public string RemotePath { get; set; } = "";
    public string To { get; set; } = "";
    public bool UseTls { get; set; }
    public bool UseCompress { get; set; }
    public bool CancellationRequested { get; set; }
    public long DoneBytes { get; set; }
    public long TotalBytes { get; set; }
    public int DoneFiles { get; set; }
    public int TotalFiles { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset? FinishedUtc { get; set; }
    public string? Error { get; set; }

    public TransferStatus Clone() => new()
    {
        Id = Id,
        Kind = Kind,
        State = State,
        LocalPath = LocalPath,
        RemotePath = RemotePath,
        To = To,
        UseTls = UseTls,
        UseCompress = UseCompress,
        CancellationRequested = CancellationRequested,
        DoneBytes = DoneBytes,
        TotalBytes = TotalBytes,
        DoneFiles = DoneFiles,
        TotalFiles = TotalFiles,
        StartedUtc = StartedUtc,
        FinishedUtc = FinishedUtc,
        Error = Error
    };
}

internal sealed class TransferEvent
{
    public string Type { get; set; } = "state";
    public DateTimeOffset TimestampUtc { get; set; }
    public TransferStatus Job { get; set; } = new();
}

internal readonly record struct Endpoint(string Host, int Port, string Raw);
