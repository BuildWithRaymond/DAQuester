namespace Arbiter.Net.Security;

public interface INetworkPacketEncryptor
{
    NetworkEncryptionParameters Parameters { get; set; }

    bool ShouldEncrypt(byte command);
    
    NetworkPacket Encrypt(NetworkPacket packet, byte sequence);
    NetworkPacket Decrypt(NetworkPacket packet);
}