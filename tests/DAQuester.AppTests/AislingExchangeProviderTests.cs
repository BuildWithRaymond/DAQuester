using System.Net;
using System.Text;
using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class AislingExchangeProviderTests
{
    private sealed class Credentials : IAislingExchangeCredentialStore
    {
        public Task<AislingExchangeCredential?> LoadAsync(string character, string serverProfile, CancellationToken cancellation = default)
            => Task.FromResult<AislingExchangeCredential?>(new(character, serverProfile, new string('s', 48), "quest-supplies", DateTimeOffset.UtcNow.AddMinutes(30)));
        public Task SaveAsync(AislingExchangeCredential credential, CancellationToken cancellation = default) => Task.CompletedTask;
        public Task ClearAsync(string character, string serverProfile, CancellationToken cancellation = default) => Task.CompletedTask;
    }

    private sealed class Handler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public object? Reply { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(Reply, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), Encoding.UTF8, "application/json")
            });
        }
    }

    private static AislingExchangeClient Client(Handler handler) => new(
        AislingExchangeApiRoutes.FromApiBase(new("https://aislingexchange.com/api/quest-supplies/v1/")), new Credentials(), handler);

    [Fact]
    public async Task Disabled_provider_keeps_lookup_available_without_creating_new_requests()
    {
        var operation = Guid.NewGuid();
        var journey = Guid.NewGuid();
        var handler = new Handler { Reply = new AislingExchangeRequestStatus("order-1", operation, "Elara", "test-server", "submitted", []) };
        using var client = Client(handler);
        var provider = new AislingExchangeQuestSupplyProvider(client, "Elara", "test-server", () => false);
        Assert.False(provider.Enabled);
        Assert.Null(await provider.QuoteAsync(new("Elara", "test-server", journey, [new("Apple", 2)]), default));
        await Assert.ThrowsAsync<QuestBlockedException>(() => provider.SubmitAsync(new(operation, journey, "Elara", "test-server", "quote-1", [new("Apple", 2)]), default));
        Assert.Equal(0, handler.Requests);
        Assert.Equal(operation, (await provider.FindAsync(operation, default))!.ClientOperationId);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task Quote_preserves_journey_identity_and_cannot_be_requested_for_another_character()
    {
        var journey = Guid.NewGuid();
        var handler = new Handler { Reply = new AislingExchangeQuote("quote-1", "Elara", "test-server", DateTimeOffset.UtcNow.AddMinutes(1), [new("Apple", 2)], journey.ToString("D")) };
        using var client = Client(handler);
        var provider = new AislingExchangeQuestSupplyProvider(client, "Elara", "test-server", () => true);
        var quote = await provider.QuoteAsync(new("Elara", "test-server", journey, [new("Apple", 2)]), default);
        Assert.Equal("quote-1", quote!.Id);
        Assert.Equal(new QuestSupplyLine("Apple", 2), Assert.Single(quote.Items));
        await Assert.ThrowsAsync<QuestBlockedException>(() => provider.QuoteAsync(new("Other", "test-server", journey, [new("Apple", 2)]), default));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task Existing_obligation_checks_pairing_even_when_new_requests_are_disabled()
    {
        int pairings = 0;
        var handler = new Handler();
        using var client = Client(handler);
        var provider = new AislingExchangeQuestSupplyProvider(client, "Elara", "test-server", () => false,
            _ => { pairings++; throw new QuestBlockedException("AislingExchange awaits the current character proof."); });
        await Assert.ThrowsAsync<QuestBlockedException>(() => provider.FindAsync(Guid.NewGuid(), default));
        Assert.Equal(1, pairings);
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task Empty_availability_quote_falls_back_to_existing_preparation()
    {
        var journey = Guid.NewGuid();
        var handler = new Handler { Reply = new AislingExchangeQuote("quote-1", "Elara", "test-server", DateTimeOffset.UtcNow.AddMinutes(1), [], journey.ToString("D")) };
        using var client = Client(handler);
        var provider = new AislingExchangeQuestSupplyProvider(client, "Elara", "test-server", () => true);
        Assert.Null(await provider.QuoteAsync(new("Elara", "test-server", journey, [new("Apple", 2)]), default));
    }
}
