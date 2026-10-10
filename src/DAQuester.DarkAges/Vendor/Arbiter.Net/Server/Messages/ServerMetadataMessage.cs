using Arbiter.Net.Annotations;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server.Types;
using Arbiter.Net.Types;

namespace Arbiter.Net.Server.Messages;

[NetworkCommand(ServerCommand.Metadata)]
public class ServerMetadataMessage : ServerMessage
{
    public MetadataResponseType ResponseType { get; set; }
    public string? Name { get; set; }
    public uint? Checksum { get; set; }
    public IReadOnlyList<byte>? Data { get; set; }
    public List<ServerMetadataEntry>? MetadataFiles { get; set; }

    public override void Deserialize(NetworkPacketReader reader)
    {
        base.Deserialize(reader);
        
        ResponseType = (MetadataResponseType)reader.ReadByte();

        if (ResponseType == MetadataResponseType.Metadata)
        {
            Name = reader.ReadString8();
            Checksum = reader.ReadUInt32();
            
            var contentSize = reader.ReadUInt16();
            Data = reader.ReadBytes(contentSize);
        }
        else if (ResponseType == MetadataResponseType.Listing)
        {
            MetadataFiles = [];

            var count = reader.ReadUInt16();
            for (var i = 0; i < count; i++)
            {
                MetadataFiles.Add(new ServerMetadataEntry
                {
                    Name = reader.ReadString8(),
                    Checksum = reader.ReadUInt32()
                });
            }
        }
    }
    
    public override void Serialize(ref NetworkPacketBuilder builder)
    {
        base.Serialize(ref builder);

        builder.AppendByte((byte)ResponseType);

        if (ResponseType == MetadataResponseType.Metadata)
        {
            builder.AppendString8(Name ?? string.Empty);
            builder.AppendUInt32(Checksum ?? 0);

            var length = Math.Min(Data?.Count ?? 0, ushort.MaxValue);
            var data = Data?.Take(length) ?? [];

            builder.AppendUInt16((ushort)length);
            builder.AppendBytes(data);
        }
        else if (ResponseType == MetadataResponseType.Listing)
        {
            var files = MetadataFiles ?? [];
            builder.AppendUInt16((ushort)files.Count);
            
            foreach (var file in files)
            {
                builder.AppendString8(file.Name);
                builder.AppendUInt32(file.Checksum);
            }
        }
    }
}