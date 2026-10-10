using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;

namespace Arbiter.Net.Client.Messages;

public class ClientMessageFactory : IClientMessageFactory
{
    public static ClientMessageFactory Default { get; } = new();

    private readonly Dictionary<ClientCommand, Func<NetworkPacketSource, IClientMessage>> _factoryMappings = new();
    private readonly Dictionary<Type, ClientCommand> _commandMappings = new();
    private readonly Dictionary<ClientCommand, Type> _reverseCommandMappings = new();

    public ClientMessageFactory()
    {
        RegisterAnnotatedMessages();
    }

    private void RegisterAnnotatedMessages()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IClientMessage).IsAssignableFrom(t));

        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<NetworkCommandAttribute>();
            if (attr is null)
            {
                continue;
            }

            var command = (ClientCommand)attr.Command;

            // Create compiled factory delegate for performance
            var factory = CreateFactory(type);
            _factoryMappings.Add(command, factory);
            _commandMappings.Add(type, command);
            _reverseCommandMappings.Add(command, type);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<NetworkPacketSource, IClientMessage> CreateFactory(Type type)
    {
        var ctor = type.GetConstructor(Type.EmptyTypes);
        return ctor is null
            ? throw new InvalidOperationException($"Type {type.Name} must have a parameterless constructor")
            : source =>
            {
                var instance = (IClientMessage)ctor.Invoke(null);
                instance.Source = source;
                return instance;
            };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Type? GetMessageType(ClientCommand command) => _reverseCommandMappings.GetValueOrDefault(command);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ClientCommand? GetMessageCommand(Type messageType) => _commandMappings.GetValueOrDefault(messageType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IClientMessage? Create(ClientPacket packet)
    {
        if (!_factoryMappings.TryGetValue(packet.Command, out var factory))
        {
            return null;
        }

        var instance = factory(packet.Source);
        instance.Source = packet.Source;

        var reader = new NetworkPacketReader(packet);
        instance.Deserialize(reader);

        return instance;
    }

    public bool TryCreate(ClientPacket packet, [NotNullWhen(true)] out IClientMessage? message)
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

    public bool TryCreate<T>(ClientPacket packet, [NotNullWhen(true)] out T? message) where T : IClientMessage
    {
        message = default;

        if (!TryCreate(packet, out var clientMessage))
        {
            return false;
        }

        if (clientMessage is not T expectedMessage)
        {
            return false;
        }

        message = expectedMessage;
        return true;
    }
}