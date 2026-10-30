using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using DAQuester.Core;
using DAQuester.DarkAges.Transport;
using Xunit;

namespace DAQuester.Tests;

public sealed class LoginRegistrationTransferTests
{
    static readonly DateTimeOffset Issued = DateTimeOffset.Parse("2026-10-29T20:00:00Z");
    static ServerNotice Source() => new(17, 33, 1, "Your expiration date is 12-31.", Issued.AddMinutes(-20));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_handshake_preserves_original_notice_and_consumes_once_for_actual_avatar(bool trailingNull)
    {
        var source = Source();
        byte[] redirect = Redirect(trailingNull: trailingNull, seed: 255);
        var transfer = Assert.IsType<LoginRegistrationTransfer>(LoginRegistrationTransfer.TryCreate(source, "aGeNt", redirect, Issued));
        Assert.True(transfer.ObserveHandshake(redirect[7..], Issued.AddSeconds(40)));
        var result = transfer.Consume("AGENT", 77, Issued.AddSeconds(44)); // Cookie42 is not avatar77.
        Assert.Same(source, result);
        Assert.Equal((17, 33, source.ObservedAt), (result!.Generation, result.Revision, result.ObservedAt));
        Assert.Null(transfer.Consume("Agent", 77, Issued.AddSeconds(44)));
        Assert.False(transfer.ObserveHandshake(redirect[7..], Issued.AddSeconds(44)));
    }

    [Fact]
    public void Input_buffers_are_not_retained_and_only_a_hash_of_metadata_is_stored()
    {
        var redirect = Redirect(trailingNull: true);
        var handshake = redirect[7..];
        var transfer = Assert.IsType<LoginRegistrationTransfer>(LoginRegistrationTransfer.TryCreate(Source(), "Agent", redirect, Issued));
        Array.Fill<byte>(redirect, 0xEE);
        var storedArrays = typeof(LoginRegistrationTransfer).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(field => field.GetValue(transfer)).OfType<byte[]>().ToArray();
        Assert.Single(storedArrays);
        Assert.Equal(32, storedArrays[0].Length);
        Assert.True(transfer.ObserveHandshake(handshake, Issued));
        Assert.NotNull(transfer.Consume("Agent", 77, Issued));
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("key")]
    [InlineData("name")]
    [InlineData("cookie")]
    [InlineData("trailing-null")]
    [InlineData("extra-byte")]
    public void A_first_mismatched_full_metadata_handshake_permanently_invalidates(string change)
    {
        var redirect = Redirect(trailingNull: true);
        var transfer = Assert.IsType<LoginRegistrationTransfer>(LoginRegistrationTransfer.TryCreate(Source(), "Agent", redirect, Issued));
        var correct = redirect[7..];
        var changed = correct.ToArray();
        switch (change)
        {
            case "seed": changed[0] ^= 1; break;
            case "key": changed[2] ^= 1; break;
            case "name": changed[12] ^= 1; break;
            case "cookie": changed[^2] ^= 1; break;
            case "trailing-null": changed = changed[..^1]; break;
            case "extra-byte": changed = changed.Concat(new byte[] { 0 }).ToArray(); break;
        }
        Assert.False(transfer.ObserveHandshake(changed, Issued));
        Assert.False(transfer.ObserveHandshake(correct, Issued));
        Assert.Null(transfer.Consume("Agent", 77, Issued));
    }

    [Fact]
    public void A_duplicate_even_matching_handshake_invalidates_unconsumed_proof()
    {
        var redirect = Redirect();
        var transfer = Assert.IsType<LoginRegistrationTransfer>(LoginRegistrationTransfer.TryCreate(Source(), "Agent", redirect, Issued));
        Assert.True(transfer.ObserveHandshake(redirect[7..], Issued));
        Assert.False(transfer.ObserveHandshake(redirect[7..], Issued));
        Assert.Null(transfer.Consume("Agent", 77, Issued));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_invalidation_before_or_after_handshake_is_terminal(bool matched)
    {
        var redirect = Redirect();
        var transfer = Assert.IsType<LoginRegistrationTransfer>(LoginRegistrationTransfer.TryCreate(Source(), "Agent", redirect, Issued));
        if (matched) Assert.True(transfer.ObserveHandshake(redirect[7..], Issued));
        transfer.Invalidate();
        Assert.False(transfer.ObserveHandshake(redirect[7..], Issued));
        Assert.Null(transfer.Consume("Agent", 77, Issued));
        transfer.Invalidate();
        Assert.Null(transfer.Consume("Agent", 77, Issued));
    }

    [Theory]
    [InlineData("no-handshake")]
    [InlineData("wrong-name")]
    [InlineData("zero-id")]
    [InlineData("expired-consume")]
    public void Missing_match_wrong_identity_or_expiry_cannot_release_registration(string failure)
    {
        var redirect = Redirect();
        var transfer = Assert.IsType<LoginRegistrationTransfer>(LoginRegistrationTransfer.TryCreate(Source(), "Agent", redirect, Issued));
        if (failure != "no-handshake") Assert.True(transfer.ObserveHandshake(redirect[7..], Issued));
        Assert.Null(transfer.Consume(failure == "wrong-name" ? "Other" : "Agent", failure == "zero-id" ? 0u : 77u,
            failure == "expired-consume" ? Issued.AddSeconds(45) : Issued));
        Assert.Null(transfer.Consume("Agent", 77, Issued));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(45)]
    [InlineData(46)]
    public void Handshake_outside_the_issuance_window_permanently_expires(int seconds)
    {
        var redirect = Redirect();
        var transfer = Assert.IsType<LoginRegistrationTransfer>(LoginRegistrationTransfer.TryCreate(Source(), "Agent", redirect, Issued));
        Assert.False(transfer.ObserveHandshake(redirect[7..], Issued.AddSeconds(seconds)));
        Assert.False(transfer.ObserveHandshake(redirect[7..], Issued));
        Assert.Null(transfer.Consume("Agent", 77, Issued));
    }

    [Theory]
    [InlineData("generation-zero")]
    [InlineData("revision-zero")]
    [InlineData("unregistered")]
    [InlineData("wrong-type")]
    [InlineData("wrong-name")]
    [InlineData("empty-name")]
    [InlineData("name-nul")]
    public void Creation_requires_registration_identity_and_expected_character(string change)
    {
        var source = Source();
        string character = "Agent";
        switch (change)
        {
            case "generation-zero": source = source with { Generation = 0 }; break;
            case "revision-zero": source = source with { Revision = 0 }; break;
            case "unregistered": source = source with { Text = "Unrelated account message" }; break;
            case "wrong-type": source = source with { Type = 0 }; break;
            case "wrong-name": character = "Other"; break;
            case "empty-name": character = ""; break;
            case "name-nul": character = "Agent\0"; break;
        }
        Assert.Null(LoginRegistrationTransfer.TryCreate(source, character, Redirect(), Issued));
    }

    [Theory]
    [InlineData("count-short")]
    [InlineData("count-long")]
    [InlineData("count-includes-null")]
    [InlineData("key-short")]
    [InlineData("key-long")]
    [InlineData("name-empty")]
    [InlineData("name-short")]
    [InlineData("name-long")]
    [InlineData("name-nonascii")]
    [InlineData("trailing-data")]
    [InlineData("two-nulls")]
    public void Malformed_redirect_metadata_never_creates_a_transfer(string change)
    {
        var redirect = Redirect(trailingNull: change == "count-includes-null");
        switch (change)
        {
            case "count-short": redirect[6]--; break;
            case "count-long": redirect[6]++; break;
            case "count-includes-null": redirect[6]++; break;
            case "key-short": redirect[8] = 8; break;
            case "key-long": redirect[8] = 10; break;
            case "name-empty": redirect[18] = 0; break;
            case "name-short": redirect[18] = 4; break;
            case "name-long": redirect[18] = 255; break;
            case "name-nonascii": redirect[19] = 0xC1; break;
            case "trailing-data": redirect = redirect.Concat(new byte[] { 1 }).ToArray(); break;
            case "two-nulls": redirect = redirect.Concat(new byte[] { 0, 0 }).ToArray(); break;
        }
        Assert.Null(LoginRegistrationTransfer.TryCreate(Source(), "Agent", redirect, Issued));
    }

    [Fact]
    public void Every_truncated_payload_is_rejected_without_throwing()
    {
        var redirect = Redirect();
        for (int length = 0; length < redirect.Length; length++)
            Assert.Null(LoginRegistrationTransfer.TryCreate(Source(), "Agent", redirect[..length], Issued));
    }

    static byte[] Redirect(bool trailingNull = false, byte seed = 3)
    {
        byte[] metadata = new byte[] { seed, 9, 1, 2, 3, 4, 5, 6, 7, 8, 9, 5 }.Concat(Encoding.ASCII.GetBytes("Agent"))
            .Concat(new byte[] { 0, 0, 0, 42 }).ToArray();
        Assert.Equal(21, metadata.Length);
        byte[] result = new byte[7 + metadata.Length + (trailingNull ? 1 : 0)];
        new byte[] { 1, 0, 0, 127 }.CopyTo(result, 0);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(4), 2611);
        result[6] = (byte)metadata.Length;
        metadata.CopyTo(result, 7);
        return result;
    }
}
