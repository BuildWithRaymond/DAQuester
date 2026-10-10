using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Messages;

[NetworkCommand(ServerCommand.Group)]
public class ServerGroupMessage : ServerMessage
{
    public ServerGroupAction Action { get; set; }
    public string Name { get; set; } = string.Empty;
    public ServerGroupBox? GroupBox { get; set; }

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);

        Action = (ServerGroupAction)reader.ReadByte();
        
        if (Action == ServerGroupAction.RecruitInfo)
        {
            // Yes the ordering of this is different from the client group box
            GroupBox = new ServerGroupBox
            {
                Leader = reader.ReadString8(),
                Name = reader.ReadString8(),
                Note = reader.ReadString8(),
                MinLevel = reader.ReadByte(),
                MaxLevel = reader.ReadByte(),
                MaxWarriors = reader.ReadByte(),
                CurrentWarriors = reader.ReadByte(),
                MaxWizards = reader.ReadByte(),
                CurrentWizards = reader.ReadByte(),
                MaxMonks = reader.ReadByte(),
                CurrentRogues = reader.ReadByte(),
                MaxPriests = reader.ReadByte(),
                CurrentPriests = reader.ReadByte(),
                MaxRogues = reader.ReadByte(),
                CurrentMonks = reader.ReadByte(),
            };

            Name = GroupBox.Leader;
        }
        else
        {
            Name = reader.ReadString8();
        }
    }

    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);
    
        builder.AppendByte((byte)Action);
    
        if (Action == ServerGroupAction.RecruitInfo)
        {
            if (GroupBox is null)
            {
                return;
            }
            
            builder.AppendString8(GroupBox.Leader);
            builder.AppendString8(GroupBox.Name);
            builder.AppendString8(GroupBox.Note);
            builder.AppendByte(GroupBox.MinLevel);
            builder.AppendByte(GroupBox.MaxLevel);
            builder.AppendByte(GroupBox.MaxWarriors);
            builder.AppendByte(GroupBox.CurrentWarriors);
            builder.AppendByte(GroupBox.MaxWizards);
            builder.AppendByte(GroupBox.CurrentWizards);
            builder.AppendByte(GroupBox.MaxMonks);
            builder.AppendByte(GroupBox.CurrentRogues);
            builder.AppendByte(GroupBox.MaxPriests);
            builder.AppendByte(GroupBox.CurrentPriests);
            builder.AppendByte(GroupBox.MaxRogues);
            builder.AppendByte(GroupBox.CurrentMonks);
        }
        else
        {
            builder.AppendString8(Name);
        }
    }
}