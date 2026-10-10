using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;

namespace Arbiter.Net.Server.Messages;

public class ServerMessageFactory : IServerMessageFactory
{
    public static ServerMessageFactory Default { get; } = new();

    private readonly Dictionary<ServerCommand, Func<NetworkPacketSource, IServerMessage>> _factoryMappings = new();
    private readonly Dictionary<Type, ServerCommand> _commandMappings = new();
    private readonly Dictionary<ServerCommand, Type> _reverseCommandMappings = new();

    public ServerMessageFactory()
    {
        RegisterAnnotatedMessages();
    }

    private void RegisterAnnotatedMessages()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IServerMessage).IsAssignableFrom(t));

        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<NetworkCommandAttribute>(inherit: true);
            if (attr is null)
            {
                continue;
            }

            var command = (ServerCommand)attr.Command;
            
            // Create compiled factory delegate for performance
            var factory = CreateFactory(type);
            _factoryMappings.Add(command, factory);
            _commandMappings.Add(type, command);
            _reverseCommandMappings.Add(command, type);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<NetworkPacketSource, IServerMessage> CreateFactory(Type type)
    {
        var ctor = type.GetConstructor(Type.EmptyTypes);
        return ctor is null
            ? throw new InvalidOperationException($"Type {type.Name} must have a parameterless constructor")
            : source =>
            {
                var instance = (IServerMessage)ctor.Invoke(null);
                instance.Source = source;
                return instance;
            };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Type? GetMessageType(ServerCommand command) => _reverseCommandMappings.GetValueOrDefault(command);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ServerCommand? GetMessageCommand(Type messageType) => _commandMappings.GetValueOrDefault(messageType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IServerMessage? Create(ServerPacket packet)
    {
        if (!_factoryMappings.TryGetValue(packet.Command, out var factory))
        {
            return null;
        }

        var instance = factory(packet.Source);
        var reader = new NetworkPacketReader(packet);
        instance.Deserialize(reader);

        return instance;
    }

    public bool TryCreate(ServerPacket packet, [NotNullWhen(true)] out IServerMessage? message)
    {
        try
        {
            message = Create(packet);
            return message is not null;
        }
        catch
        {
            message = null;
            return false;
        }
    }

    public bool TryCreate<T>(ServerPacket packet, [NotNullWhen(true)] out T? message) where T : IServerMessage
    {
        message = default;

        if (!TryCreate(packet, out var serverMessage))
        {
            return false;
        }

        if (serverMessage is not T expectedMessage)
        {
            return false;
        }

        message = expectedMessage;
        return true;
    }
}