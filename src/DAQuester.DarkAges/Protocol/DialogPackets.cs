using Arbiter.Net.Client;
using Arbiter.Net.Client.Messages;
using Arbiter.Net.Serialization;
using Arbiter.Net.Types;
using DAQuester.Core;

namespace DAQuester.DarkAges.Protocol;

public static class DialogPackets
{
    public static ClientPacket Close(CharacterSnapshot s, Dialog d)
    {
        if (s.Generation != d.Generation || s.Dialog?.Revision != d.Revision || s.Dialog.EntityId != d.EntityId)
            throw new QuestBlockedException("Stale dialog. Reopen NPC.");
        if (!d.IsPursuit)
            throw new QuestBlockedException("Merchant dialog is dismissed locally, without a pursuit request.");
        var message = new ClientPursuitMessage
        {
            EntityType = (EntityTypeFlags)d.EntityType,
            EntityId = d.EntityId,
            PursuitId = d.Pursuit,
            StepId = d.Step,
            ArgsType = DialogArgsType.None
        };
        var builder = new NetworkPacketBuilder(ClientCommand.Pursuit);
        try
        {
            message.Serialize(ref builder);
            return (ClientPacket)builder.ToPacket();
        }
        finally
        {
            builder.Dispose();
        }
    }

    public static ClientPacket Continue(CharacterSnapshot s, Dialog d, string? input = null)
    {
        if (s.Generation != d.Generation || s.Dialog?.Revision != d.Revision || s.Dialog.EntityId != d.EntityId)
            throw new QuestBlockedException("Stale dialog. Reopen NPC.");
        if (input is null && (!d.IsPursuit || !d.HasNext))
            throw new QuestBlockedException("Current dialog has no Next action.");
        if (input is not null && (d.Prompt is null && d.Type is not (2 or 3)))
            throw new QuestBlockedException("Current dialog has no text prompt.");
        IClientMessage m = d.IsPursuit ? new ClientPursuitMessage
        {
            EntityType = (EntityTypeFlags)d.EntityType,
            EntityId = d.EntityId,
            PursuitId = d.Pursuit,
            StepId = checked((ushort)(d.Step + 1)),
            ArgsType = input is null ? DialogArgsType.None : DialogArgsType.TextInput,
            TextInputs = input is null ? [] : [input]
        }

        : new ClientMerchantMessage
        {
            EntityType = (EntityTypeFlags)d.EntityType,
            EntityId = d.EntityId,
            PursuitId = d.Pursuit,
            Arguments = input is null ? [] : [input]
        };
        var builder = new NetworkPacketBuilder(d.IsPursuit ? ClientCommand.Pursuit : ClientCommand.Merchant);
        try
        {
            m.Serialize(ref builder);
            return (ClientPacket)builder.ToPacket();
        }
        finally
        {
            builder.Dispose();
        }
    }

    public static ClientPacket Choice(CharacterSnapshot s, Dialog d, string text)
    {
        var choice = GameSession.ResolveChoice(s, d, text);
        IClientMessage m = d.IsPursuit ? new ClientPursuitMessage
        {
            EntityType = (EntityTypeFlags)d.EntityType,
            EntityId = d.EntityId,
            PursuitId = d.Pursuit,
            StepId = checked((ushort)(d.Step + 1)),
            ArgsType = DialogArgsType.MenuChoice,
            MenuChoice = choice.Option
        }

        : new ClientMerchantMessage
        {
            EntityType = (EntityTypeFlags)d.EntityType,
            EntityId = d.EntityId,
            PursuitId = choice.Pursuit
        };
        var builder = new NetworkPacketBuilder(d.IsPursuit ? ClientCommand.Pursuit : ClientCommand.Merchant);
        try
        {
            m.Serialize(ref builder);
            return (ClientPacket)builder.ToPacket();
        }
        finally
        {
            builder.Dispose();
        }
    }
}
