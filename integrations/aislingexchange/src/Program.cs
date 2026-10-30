using System.Text.Json;
using AislingExchange.Broker;

try
{
    var options = new BrokerOptions
    {
        GameServer = Environment.GetEnvironmentVariable("AISLINGEXCHANGE_GAME_SERVER") ?? "",
        WorkerTokenHash = Environment.GetEnvironmentVariable("AISLINGEXCHANGE_WORKER_TOKEN_HASH") ?? "",
        CatalogLimits = JsonSerializer.Deserialize<Dictionary<string, int>>(
            Environment.GetEnvironmentVariable("AISLINGEXCHANGE_CATALOG_LIMITS") ?? "{}") ?? [],
    };
    var directory = Environment.GetEnvironmentVariable("AISLINGEXCHANGE_STATE_DIRECTORY");
    if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
        throw new InvalidOperationException();
    await using var app = BrokerHost.Create(options, directory);
    // Force configuration and exclusive-state validation before accepting traffic.
    _ = app.Services.GetRequiredService<BrokerEngine>();
    await app.RunAsync();
}
catch
{
    Console.Error.WriteLine("AislingExchange broker could not start. Check protected service configuration.");
    Environment.ExitCode = 1;
}
