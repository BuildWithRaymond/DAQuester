using System.Text;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class SavedLoginTests
{
    static byte[] Packet(string name, string password) => new[]
    {
        (byte)name.Length
    }.Concat(Encoding.ASCII.GetBytes(name)).Concat(new[] { (byte)password.Length }).Concat(Encoding.ASCII.GetBytes(password)).Concat(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }).ToArray();
    [Fact]
    public void Matching_ticket_replaces_only_password_preserves_client_metadata_and_submits_once()
    {
        var ticket = new SavedLoginTicket("Synthetic", () => "vault-password");
        var original = Packet("Synthetic", ticket.Ticket);
        var rewritten = ticket.Rewrite(original, DateTimeOffset.UtcNow);
        Assert.False(rewritten.Blocked);
        Assert.Equal(Packet("Synthetic", "vault-password"), rewritten.Payload);
        Assert.True(ticket.WasSubmitted);
        Assert.True(ticket.Rewrite(original, DateTimeOffset.UtcNow).Blocked);
    }

    [Fact]
    public void Expired_cancelled_wrong_character_and_unavailable_credentials_never_send_placeholder()
    {
        var ticket = new SavedLoginTicket("Synthetic", () => "secret");
        Assert.True(ticket.Rewrite(Packet("Other", ticket.Ticket), DateTimeOffset.UtcNow).Blocked);
        Assert.True(ticket.Rewrite(Packet("Synthetic", ticket.Ticket), DateTimeOffset.UtcNow.AddMinutes(3)).Blocked);
        var cancel = new SavedLoginTicket("Synthetic", () => "secret");
        cancel.Cancel();
        Assert.True(cancel.Rewrite(Packet("Synthetic", cancel.Ticket), DateTimeOffset.UtcNow).Blocked);
        var missing = new SavedLoginTicket("Synthetic", () => throw new IOException("vault unavailable"));
        Assert.True(missing.Rewrite(Packet("Synthetic", missing.Ticket), DateTimeOffset.UtcNow).Blocked);
    }

    [Fact]
    public void Manual_login_passes_without_reading_vault_and_malformed_packets_block()
    {
        var ticket = new SavedLoginTicket("Synthetic", () => throw new Exception("vault must stay unread"));
        var manual = Packet("Synthetic", "manual-value");
        Assert.Equal(manual, ticket.Rewrite(manual, DateTimeOffset.UtcNow).Payload);
        Assert.False(ticket.WasSubmitted);
        Assert.True(ticket.Rewrite([1], DateTimeOffset.UtcNow).Blocked);
    }

    [Fact]
    public void Failed_vault_read_consumes_ticket_without_claiming_submission()
    {
        var ticket = new SavedLoginTicket("Synthetic", () => null);
        Assert.True(ticket.Rewrite(Packet("Synthetic", ticket.Ticket), DateTimeOffset.UtcNow).Blocked);
        Assert.False(ticket.WasSubmitted);
        Assert.True(ticket.Rewrite(Packet("Synthetic", ticket.Ticket), DateTimeOffset.UtcNow).Blocked);
    }

    [Fact]
    public async Task Form_automation_verifies_each_field_and_submits_once()
    {
        var form = new FakeForm();
        await LoginFormAutomation.Run(form, "Synthetic", "12345678", _ =>
        {
        }, CancellationToken.None);
        Assert.Equal("Synthetic", form.Name);
        Assert.Equal("12345678", form.Password);
        Assert.Equal(1, form.Submissions);
    }

    [Fact]
    public async Task Cancellation_never_submits_form()
    {
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        var form = new FakeForm();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LoginFormAutomation.Run(form, "Synthetic", "12345678", _ =>
        {
        }, stop.Token));
        Assert.Equal(0, form.Submissions);
    }

    sealed class FakeForm : ILoginForm
    {
        public string Name = "", Password = "";
        public int Focus = 2, Submissions;
        bool visible;
        public LoginFormState Read(string name, string ticket) => new(true, visible, false, Focus, Name.Length, Password.Length, Name == name, Password == ticket);
        public void Open() => visible = true;
        public void Tab() => Focus = Focus == 2 ? 3 : 2;
        public void End()
        {
        }

        public void Backspace()
        {
            if (Focus == 2)
                Name = Name[..^1];
            else
                Password = Password[..^1];
        }

        public void Type(char c)
        {
            if (Focus == 2)
                Name += c;
            else
                Password += c;
        }

        public void Submit() => Submissions++;
        public Task Pause(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
