using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AislingExchange.Broker;

public sealed partial class BrokerEngine
{
    private static CredentialRecord Authenticate(BrokerState s, string? bearer, DateTimeOffset now, string scope = "quest-supplies")
    {
        if (bearer is null || bearer.Length is < 32 or > 128) throw new BrokerFault(401, "UNAUTHORIZED");
        var hash = Hash(bearer);
        var c = s.Credentials.SingleOrDefault(c => FixedEquals(c.TokenHash, hash));
        if (c is null || c.Revoked || c.ExpiresAt <= now || c.Scope != scope) throw new BrokerFault(401, "UNAUTHORIZED");
        return c;
    }
    private static OperationRecord FindOwned(BrokerState s, CredentialRecord c, Guid id, string group) => s.Operations.SingleOrDefault(o =>
        o.Id == id && o.Kind == (group == "requests" ? "supply" : "return") && Owns(s, c, o)) ?? throw new BrokerFault(404, "NOT_FOUND");
    private static bool Owns(BrokerState s, CredentialRecord c, OperationRecord o) => o.Subject == c.Subject && HasInstallationAccess(s, c, o.InstallationId);
    private static bool HasInstallationAccess(BrokerState s, CredentialRecord c, Guid installation) => installation == c.InstallationId
        || s.Recoveries.Any(r => r.Subject == c.Subject && r.InstallationId == c.InstallationId && r.OriginalInstallations.Contains(installation));
    private static void MatchIdentity(CredentialRecord c, string character, string gameServer)
    {
        if (!SameCharacter(c.Character, character) || c.GameServer != gameServer) throw new BrokerFault(403, "CHARACTER_NOT_ALLOWED");
    }
    private static bool IsReady(BrokerState s, DateTimeOffset now) => s.Catalog.Ready && s.Catalog.Items.Count > 0 && s.Catalog.UpdatedAt > now.AddMinutes(-2);
    private static bool Terminal(OperationRecord o) => o.State is "received" or "complete" or "cancelled" or "rejected";
    // A source-confirmed dispatch no longer owns the character's outstanding
    // dispatch slot. Delivery remains separate and may receive later updates.
    private static bool DispatchSettled(OperationRecord o) => Terminal(o)
        || o.Kind == "supply" && o.State is "sent" or "partial" && o.Units.Count > 0
            && o.Units.All(unit => unit.SourceAccepted || unit.Cancelled);
    private static object PairingView(PairingRecord p, DateTimeOffset now, bool includeNonce = false) => new
    {
        id = p.Id, character = p.Character, gameServer = p.GameServer, receiver = "Exchanger", expiresAt = p.ExpiresAt,
        state = p.ExpiresAt <= now || p.State is "expired" or "reproof_required" ? "expired" : p.State == "issued" ? "observed" : p.State,
        nonce = includeNonce ? p.Nonce : null,
    };
    private static BrokerReply View(OperationRecord o, int status = 200) => Reply(status, new
    {
        id = o.Id, o.ClientOperationId, character = o.Character, gameServer = o.GameServer, state = o.State, o.ErrorCode, o.Sequence,
        units = o.Units.Select(u => new { u.Id, u.Item, u.SourceAccepted, u.RecipientObserved, u.Cancelled, u.Collected, u.Banked, u.ClientReported }),
    });
    private static BrokerReply Ok(object value) => Reply(200, value);
    private static BrokerReply Reply(int status, object value) => new(status, JsonSerializer.SerializeToElement(value, Wire.Json));
    private static BrokerReply Error(int status, string code) => Reply(status, new { code });
    private static void RequireId(Guid id) { if (id == Guid.Empty) throw new BrokerFault(400, "INVALID_REQUEST"); }
    private static void RequireEmpty(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || body.EnumerateObject().Any()) throw new BrokerFault(400, "INVALID_REQUEST");
    }
    private static bool Identifier(string? value) => value is { Length: >= 1 and <= 128 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    private static bool CharacterName(string? value) => value is { Length: >= 1 and <= 30 } && value.All(char.IsAsciiLetter);
    private static bool SameCharacter(string? left, string? right) => left is not null && right is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static string Subject(string gameServer, string character) => gameServer + "/" + character.ToUpperInvariant();
    private static string Secret() => Base64Url(RandomNumberGenerator.GetBytes(32));
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static bool Base64Secret(string? value) => value is { Length: 43 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(verifier)));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool HashMatches(string? value, string expected) => value is { Length: >= 32 and <= 128 } && FixedEquals(Hash(value), expected.ToUpperInvariant());
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static string BodyHash(JsonElement body)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) WriteCanonical(writer, body);
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(p.Name);
                WriteCanonical(writer, p.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
    public void Dispose() => store.Dispose();
}
