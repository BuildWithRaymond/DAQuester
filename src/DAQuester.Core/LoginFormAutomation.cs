// Adapted from DAOrganizer (MIT); form input contains only a one-use ticket.
namespace DAQuester.Core;

public sealed record LoginFormState(bool MenuReady, bool Visible, bool Blocked, int Focus, int NameLength, int PasswordLength, bool NameMatches, bool PasswordMatches);
public interface ILoginForm
{
    LoginFormState Read(string name, string ticket);
    void Open();
    void Tab();
    void End();
    void Backspace();
    void Type(char character);
    void TypePassword(char character) => Type(character);
    void AcceptName() => Tab();
    void Submit();
    Task Pause(CancellationToken token);
}

public static class LoginFormAutomation
{
    public static async Task Run(ILoginForm form, string name, string ticket, Action<string> progress, CancellationToken token)
    {
        async Task<LoginFormState> Wait(string stage, string expectedName, string expectedTicket, Func<LoginFormState, bool> ready)
        {
            progress(stage);
            for (var attempt = 0; attempt < 400; attempt++)
            {
                token.ThrowIfCancellationRequested();
                var state = form.Read(expectedName, expectedTicket);
                if (!state.Blocked && ready(state))
                    return state;
                await form.Pause(token);
            }

            throw new TimeoutException("Login stopped while " + stage.ToLowerInvariant() + ". No retry submitted.");
        }

        var initial = await Wait("Waiting for login screen", "", "", s => s.MenuReady || s.Visible);
        if (!initial.Visible)
        {
            token.ThrowIfCancellationRequested();
            form.Open();
        }

        await Wait("Opening login dialog", "", "", s => s.Visible);
        async Task Fill(int field, string value, string label)
        {
            var state = await Wait("Focusing " + label, "", "", s => s.Visible);
            for (var i = 0; state.Focus != field && i < 6; i++)
            {
                var previous = state.Focus;
                token.ThrowIfCancellationRequested();
                if (field == 3 && previous == 2)
                    form.AcceptName();
                else
                    form.Tab();
                state = await Wait("Focusing " + label, "", "", s => s.Visible && s.Focus != previous);
            }

            if (state.Focus != field)
                throw new InvalidOperationException("Login could not focus " + label + ".");
            // Dialog focus becomes visible before the child editor finishes activation.
            // Excalibur waits after opening/focusing controls; keep that pause plus readback.
            progress("Waiting for " + label + " input");
            for (var i = 0; i < 10; i++)
                await form.Pause(token);
            state = await Wait("Checking " + label + " focus", "", "", s => s.Visible && s.Focus == field);
            int Length(LoginFormState s) => field == 2 ? s.NameLength : s.PasswordLength;
            while (Length(state) > 0)
            {
                var previous = Length(state);
                token.ThrowIfCancellationRequested();
                form.End();
                form.Backspace();
                state = await Wait("Clearing " + label, "", "", s => s.Visible && s.Focus == field && Length(s) < previous);
            }

            var prefix = "";
            foreach (var ch in value)
            {
                token.ThrowIfCancellationRequested();
                await Wait("Entering " + label, field == 2 ? prefix : name, field == 3 ? prefix : "", s => s.Visible && s.Focus == field);
                if (field == 3)
                    form.TypePassword(ch);
                else
                    form.Type(ch);
                prefix += ch;
                await Wait("Verifying " + label, field == 2 ? prefix : name, field == 3 ? prefix : "", s => s.Visible && s.Focus == field && (field == 2 ? s.NameMatches : s.PasswordMatches));
            }
        }

        await Fill(2, name, "character name");
        await Fill(3, ticket, "password field");
        await Wait("Verifying login fields", name, ticket, s => s.Visible && s.Focus == 3 && s.NameMatches && s.PasswordMatches);
        token.ThrowIfCancellationRequested();
        progress("Submitting login");
        form.Submit();
    }
}
