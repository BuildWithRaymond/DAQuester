using DAQuester.Core;
using DAQuester.DarkAges;
using DAQuester.DarkAges.Navigation;
using DAQuester.DarkAges.Transport;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;
namespace DAQuester.Tests;
public class GatherNavigationTests {
 [Fact] public async Task Long_patrol_walk_stops_after_server_inventory_reaches_six() {
  string folder=Path.Combine(Path.GetTempPath(),"DAQuester-navigation-"+Guid.NewGuid());Directory.CreateDirectory(Path.Combine(folder,"maps"));
  await File.WriteAllBytesAsync(Path.Combine(folder,"maps","lod500.map"),new byte[5*5*6]);
  using(var writer=new BinaryWriter(File.Create(Path.Combine(folder,"ia.dat")))){writer.Write(2);writer.Write(25);writer.Write(Encoding.ASCII.GetBytes("sotp.dat".PadRight(13,'\0')));writer.Write(26);writer.Write((byte)0);}
  string routes=Path.Combine(folder,"routes.json");await File.WriteAllTextAsync(routes,JsonSerializer.Serialize(new[]{new MapRecord(500,"Mileth",5,5,[])}));
  using var remote=new TcpListener(IPAddress.Loopback,0);remote.Start();using var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
  var connected=new TaskCompletionSource<ProxyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
  await using var host=new ProxyHost();host.Connected+=c=>connected.SetResult(c);await host.StartAsync(port,(IPEndPoint)remote.LocalEndpoint);
  using var client=new TcpClient();await client.ConnectAsync(IPAddress.Loopback,port);using var server=await remote.AcceptTcpClientAsync();var connection=await connected.Task;
  connection.Tracker.Identity("Nuclear",42);connection.Tracker.Observe(0x15,[1,244,5,5,0,0,0,0,0,0]);connection.Tracker.Observe(4,[0,1,0,1,0,0,0,0]);connection.Tracker.Observe(0x58,[0]);
  var session=new GameSession(connection,new(folder,routes),100);using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(3));
  int sent=0;var crypto=new Arbiter.Net.Server.ServerPacketEncryptor();
  var respond=Task.Run(async()=>{
   try{for(int x=1;x<4;x++) {
    byte[] header=new byte[3];await server.GetStream().ReadExactlyAsync(header,limit.Token);byte[] data=new byte[(header[1]<<8)|header[2]];await server.GetStream().ReadExactlyAsync(data,limit.Token);Interlocked.Increment(ref sent);
    await server.GetStream().WriteAsync(crypto.Encrypt(new Arbiter.Net.Server.ServerPacket(11,[1,0,(byte)x,0,1,0,0,0,0,0]),(byte)(20+x*2)).ToArray(),limit.Token);
    byte[] inventory=new byte[]{1,0,0,0,6}.Concat(Encoding.ASCII.GetBytes("Branch")).Concat(new byte[]{0,0,0,6,0,0,0,0,0,0,0,0,0}).ToArray();
    await server.GetStream().WriteAsync(crypto.Encrypt(new Arbiter.Net.Server.ServerPacket(15,inventory),(byte)(21+x*2)).ToArray(),limit.Token);
   }}catch(OperationCanceledException){}
  });
  var command=JsonSerializer.Deserialize<GameCommand>("{\"Kind\":\"navigate\",\"Map\":500,\"Target\":{\"X\":4,\"Y\":1},\"StopWhen\":{\"Item\":\"Branch\",\"Count\":6}}")!;
  await session.ExecuteAsync(command,limit.Token);limit.Cancel();await respond;
  Assert.Equal(6,session.Snapshot.Count("Branch"));Assert.Equal(new Tile(2,1),session.Snapshot.Position);Assert.Equal(1,sent);
 }
}
