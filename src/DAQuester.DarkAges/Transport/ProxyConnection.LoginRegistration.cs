using System.Net;
using Arbiter.Net.Client;
using Arbiter.Net.Serialization;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;

namespace DAQuester.DarkAges.Transport;

public sealed partial class ProxyConnection
{
    readonly object loginRegistrationGate = new();
    bool loginSubmitted, loginAccepted;
    string? loginCharacter;
    long loginBaseline;
    LoginRegistrationTransfer? issuedLoginRegistration;

    internal LoginRegistrationTransfer? IncomingLoginRegistration { get; set; }
    internal Func<IPEndPoint, LoginRegistrationTransfer?, Task<int>>? LoginRedirect { get; set; }

    void ObserveLoginSubmission(byte[] payload)
    {
        lock (movementGate)
        lock (loginRegistrationGate)
        {
            issuedLoginRegistration?.Invalidate();
            IncomingLoginRegistration?.Invalidate();
            IncomingLoginRegistration = null;
            Tracker.ClearLoginRegistration();
            loginSubmitted = true;
            loginAccepted = false;
            loginCharacter = null;
            loginBaseline = Tracker.Snapshot.Revision;
            try
            {
                // Read only the character name. Credentials stay in the existing forwarding path.
                var reader = new NetworkPacketReader(new ClientPacket(0x03, payload));
                loginCharacter = reader.ReadString8();
            }
            catch (Exception) { }
        }
    }

    void ObserveLoginHandshake(byte[] payload)
    {
        lock (movementGate)
        lock (loginRegistrationGate)
        {
            issuedLoginRegistration?.Invalidate();
            Tracker.ClearLoginRegistration();
            IncomingLoginRegistration?.ObserveHandshake(payload, DateTimeOffset.UtcNow);
        }
    }

    void ObserveLoginResponse(LoginResponse response)
    {
        lock (loginRegistrationGate)
        {
            loginAccepted = loginSubmitted && !response.Denied && !string.IsNullOrWhiteSpace(loginCharacter);
            if (!loginAccepted) issuedLoginRegistration?.Invalidate();
        }
    }

    LoginRegistrationTransfer? PrepareLoginRegistration(byte[] redirectPayload)
    {
        lock (loginRegistrationGate)
        {
            issuedLoginRegistration?.Invalidate();
            issuedLoginRegistration = null;
            var current = Tracker.Snapshot;
            ServerNotice? source = null;
            string? character = null;
            if (loginSubmitted && loginAccepted && current.Registration is { } notice &&
                notice.Generation == current.Generation && notice.Revision > loginBaseline)
            {
                source = notice;
                character = loginCharacter;
            }
            else if (!loginSubmitted && DaydreamerEvidence.HasCurrentRegistration(current))
            {
                // An established session may itself be redirected. This still needs a
                // new one-shot handshake; it never authorizes a fresh login/reconnect.
                source = current.Registration ?? current.LoginRegistration?.Source;
                character = current.Name;
            }
            if (source is not null && character is not null)
                issuedLoginRegistration = LoginRegistrationTransfer.TryCreate(source, character, redirectPayload, DateTimeOffset.UtcNow);
            return issuedLoginRegistration;
        }
    }

    void AdoptLoginRegistration(uint ownId)
    {
        lock (movementGate)
        lock (loginRegistrationGate)
        {
            var transfer = IncomingLoginRegistration;
            IncomingLoginRegistration = null;
            if (name is not null && transfer?.Consume(name, ownId, DateTimeOffset.UtcNow) is { } source)
            {
                Tracker.AdoptLoginRegistration(source, name, ownId);
                Diagnostic?.Invoke("Current login registration preserved across matched server redirect.");
            }
        }
    }
}
