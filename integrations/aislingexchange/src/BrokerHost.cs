using System.Net;
using System.Text.Json;
namespace AislingExchange.Broker;

public static class BrokerHost
{
    public static WebApplication Create(BrokerOptions options, string directory)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrelHttpsConfiguration();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.AddServerHeader = false;
            server.Limits.MaxRequestBodySize = 16384;
            server.Limits.MaxRequestHeadersTotalSize = 8192;
            server.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddSingleton(_ => new BrokerEngine(directory, options));
        var app = builder.Build();
        var rate = new RequestRateGate();
        app.Run(async context =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.Remove("Server");
                context.Response.Headers.Remove("X-Powered-By");
                return Task.CompletedTask;
            });
            BrokerReply reply;
            try
            {
                // Forwarded scheme/identity headers are deliberately not trusted.
                if (!context.Request.IsHttps) reply = Failure(400, "HTTPS_REQUIRED");
                else if (!rate.Admit(context.Connection.RemoteIpAddress, context.Request.Path))
                {
                    context.Response.Headers.RetryAfter = "60";
                    reply = Failure(429, "RATE_LIMITED");
                }
                else if (context.Request.ContentLength > 16384) reply = Failure(413, "REQUEST_TOO_LARGE");
                else
                {
                    JsonElement body = default;
                    if (context.Request.Method == "POST")
                    {
                        if (!context.Request.HasJsonContentType()) throw new BrokerFault(415, "JSON_REQUIRED");
                        body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body, Wire.Json, context.RequestAborted);
                    }
                    string? bearer = null;
                    if (context.Request.Headers.Authorization is { Count: 1 } authorization
                        && authorization[0] is { } text && text.StartsWith("Bearer ", StringComparison.Ordinal)) bearer = text[7..];
                    var key = context.Request.Headers["Idempotency-Key"];
                    reply = app.Services.GetRequiredService<BrokerEngine>().Execute(context.Request.Method,
                        context.Request.Path.Value ?? "", body, bearer, key.Count == 1 ? key[0] : null);
                }
            }
            catch (BrokerFault error) { reply = Failure(error.Status, error.Code); }
            catch (BadHttpRequestException error) { reply = Failure(error.StatusCode == 413 ? 413 : 400, "INVALID_REQUEST"); }
            catch (JsonException) { reply = Failure(400, "INVALID_REQUEST"); }
            catch (Exception) { reply = Failure(503, "SERVICE_UNAVAILABLE"); }
            context.Response.StatusCode = reply.Status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(reply.Json.GetRawText(), context.RequestAborted);
        });
        return app;
    }

    private static BrokerReply Failure(int status, string code) => new(status, JsonSerializer.SerializeToElement(new { code }));
}

internal sealed class RequestRateGate
{
    private readonly object gate = new();
    private readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);
    private long minute = -1;
    private int total;
    private int pairings;
    internal bool Admit(IPAddress? remote, string path)
    {
        lock (gate)
        {
            var current = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            if (current != minute)
            {
                minute = current;
                counts.Clear();
                total = 0;
                pairings = 0;
            }
            var address = remote?.ToString() ?? "unknown";
            counts.TryGetValue(address, out var count);
            var pairing = path == "/api/quest-supplies/v1/pairings";
            if (total >= 1200 || count >= 240 || pairing && pairings >= 60 || !counts.ContainsKey(address) && counts.Count >= 2048) return false;
            total++;
            if (pairing) pairings++;
            counts[address] = count + 1;
            return true;
        }
    }
}
