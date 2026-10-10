using System.Diagnostics.CodeAnalysis;

namespace Arbiter.Net.Server.Messages;

public interface IServerMessageFactory
{
    Type? GetMessageType(ServerCommand command);
    ServerCommand? GetMessageCommand(Type messageType);

    IServerMessage? Create(ServerPacket packet);

    bool TryCreate(ServerPacket packet, [NotNullWhen(true)] out IServerMessage? message);

    bool TryCreate<T>(ServerPacket packet, [NotNullWhen(true)] out T? message) where T : IServerMessage;
}