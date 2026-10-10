using System.Diagnostics.CodeAnalysis;

namespace Arbiter.Net.Client.Messages;

public interface IClientMessageFactory
{
    Type? GetMessageType(ClientCommand command);
    ClientCommand? GetMessageCommand(Type messageType);

    IClientMessage? Create(ClientPacket packet);

    bool TryCreate(ClientPacket packet, [NotNullWhen(true)] out IClientMessage? message);
    bool TryCreate<T>(ClientPacket packet, [NotNullWhen(true)] out T? message) where T : IClientMessage;
}