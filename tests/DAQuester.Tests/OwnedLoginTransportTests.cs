using System.Net;
using System.Net.Sockets;
using System.Text;
using Arbiter.Net.Client;
using DAQuester.Core;
using DAQuester.DarkAges.Transport;
using Xunit;
namespace DAQuester.Tests;
public class OwnedLoginTransportTests {
 static byte[] Payload(string name,string password)=>new[]{(byte)name.Length}.Concat(Encoding.ASCII.GetBytes(name)).Concat(new[]{(byte)password.Length}).Concat(Encoding.ASCII.GetBytes(password)).Concat(new byte[]{1,2,3,4,5,6,7,8}).ToArray();
 static async Task<byte[]> Frame(NetworkStream stream,CancellationToken ct){byte[] header=new byte[3];await stream.ReadExactlyAsync(header,ct);byte[] data=new byte[(header[1]<<8)|header[2]];await stream.ReadExactlyAsync(data,ct);return data;}
 [Fact] public async Task Separate_owned_endpoints_only_rewrite_their_own_character_ticket(){
  using var remote=new TcpListener(IPAddress.Loopback,0);remote.Start();await using var host=new ProxyHost();
  var one=new SavedLoginTicket("First",()=>"first-secret");var two=new SavedLoginTicket("Second",()=>"second-secret");
  int a=await host.StartOwnedAsync((IPEndPoint)remote.LocalEndpoint,one);int b=await host.StartOwnedAsync((IPEndPoint)remote.LocalEndpoint,two);
  using var clientOne=new TcpClient();await clientOne.ConnectAsync(IPAddress.Loopback,a);using var serverOne=await remote.AcceptTcpClientAsync();
  using var clientTwo=new TcpClient();await clientTwo.ConnectAsync(IPAddress.Loopback,b);using var serverTwo=await remote.AcceptTcpClientAsync();
  var crypto=new ClientPacketEncryptor();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(4));
  await clientOne.GetStream().WriteAsync(crypto.Encrypt(new ClientPacket(3,Payload("First",one.Ticket)),0).ToArray(),timeout.Token);
  byte[] frame=await Frame(serverOne.GetStream(),timeout.Token);Assert.Equal(Payload("First","first-secret"),crypto.Decrypt(new ClientPacket(frame[0],frame[1..])).Data);Assert.True(one.WasSubmitted);Assert.False(two.WasSubmitted);
  await clientTwo.GetStream().WriteAsync(crypto.Encrypt(new ClientPacket(3,Payload("Second",two.Ticket)),0).ToArray(),timeout.Token);
  frame=await Frame(serverTwo.GetStream(),timeout.Token);Assert.Equal(Payload("Second","second-secret"),crypto.Decrypt(new ClientPacket(frame[0],frame[1..])).Data);
 }
}
