using System.Net;
using System.Net.Sockets;
using Arbiter.Net;
using Arbiter.Net.Client;
using Arbiter.Net.Server;
using Arbiter.Net.Security;
using Arbiter.Net.Serialization;
using DAQuester.DarkAges.Protocol;
namespace DAQuester.DarkAges.Transport;
public sealed partial class ProxyConnection(TcpClient client,TcpClient remote,long generation,Func<IPEndPoint,Task<int>> redirect):IAsyncDisposable {
 readonly SemaphoreSlim toServer=new(1,1);
 readonly SemaphoreSlim toClient=new(1,1);
 readonly CancellationTokenSource lifetime=new();
 readonly ClientPacketEncryptor clientCrypto=new();
 readonly ServerPacketEncryptor serverCrypto=new();
 byte sequence,walkCounter;
 byte clientSequence;
 bool presenting;
 long visibleMoves;
 public long VisibleMoves=>Interlocked.Read(ref visibleMoves);
 string? name;
 bool identified;
 readonly HashSet<byte> serverOpcodes=[];
 public StateTracker Tracker {get;}=new(generation);
 public event Action<string>? Interrupted;
 public event Action<string>? Diagnostic;
 public event Action<ProxyConnection>? Authenticated;
 public event Action<ClientPacket>? DialogAction;
 public bool Connected=>!lifetime.IsCancellationRequested&&client.Connected&&remote.Connected;
 public async Task RunAsync(CancellationToken cancellation) {
  using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,lifetime.Token);
  client.NoDelay=true;remote.NoDelay=true;
  try {
   var a=Pump(client.GetStream(),true,linked.Token);
   var b=Pump(remote.GetStream(),false,linked.Token);
   await Task.WhenAny(a,b);linked.Cancel();
   try{await Task.WhenAll(a,b);}catch(OperationCanceledException){}
  }catch(Exception e) {Diagnostic?.Invoke(e.GetType().Name+" in transport");}
  finally {lifetime.Cancel();Tracker.Disconnect();Interrupted?.Invoke("Disconnected");}
 }
 async Task Pump(NetworkStream source,bool fromClient,CancellationToken token) {
  var frames=new FrameBuffer();var bytes=new byte[8192];
  while(!token.IsCancellationRequested) {
   int n=await source.ReadAsync(bytes,token);if(n==0)break;
   frames.Append(bytes[..n]);
   while(frames.Read() is {} frame) {
    if(fromClient) {
     await toServer.WaitAsync(token);
     try {
      var wire=new ClientPacket(frame[3],frame[4..]);
      var plain=(ClientPacket)clientCrypto.Decrypt(wire);
      if((byte)plain.Command==0x10) {
       var r=new NetworkPacketReader(plain);byte seed=r.ReadByte();var key=r.ReadBytes(r.ReadByte());name=r.ReadString8();
       var p=new NetworkEncryptionParameters(seed,key,name);clientCrypto.Parameters=p;serverCrypto.Parameters=p;
      }
      if((byte)plain.Command==0x06) {
       CancelMovement();
       Interrupted?.Invoke("Manual movement paused automation.");
       if(plain.Data.Length>=2)plain.Data[1]=walkCounter++;
      }
      if((byte)plain.Command is 0x39 or 0x3A or 0x43 or 0x11){CancelMovement();Interrupted?.Invoke("Manual dialog or turn paused automation.");}
      if((byte)plain.Command is 0x39 or 0x3A)DialogAction?.Invoke(plain);
      var encrypted=clientCrypto.Encrypt(plain,sequence);
      if(ClientPacketEncryptor.IsEncrypted((byte)plain.Command))sequence++;
      await remote.GetStream().WriteAsync(encrypted.ToArray(),token);
     }finally{toServer.Release();}
    } else {
     var wire=new ServerPacket(frame[3],frame[4..]);
     var plain=(ServerPacket)serverCrypto.Decrypt(wire);
     if(serverOpcodes.Add((byte)plain.Command))Diagnostic?.Invoke("Server opcode "+((byte)plain.Command).ToString("X2")+" observed");
     if((byte)plain.Command==0x00&&plain.Data.Length>=7&&plain.Data[0]==0) {
      var r=new NetworkPacketReader(plain);r.ReadByte();r.ReadUInt32();byte seed=r.ReadByte();var key=r.ReadBytes(r.ReadByte());
      await toServer.WaitAsync(token);
      try{var p=new NetworkEncryptionParameters(seed,key);clientCrypto.Parameters=p;serverCrypto.Parameters=p;}
      finally{toServer.Release();}
     }
     if((byte)plain.Command==0x03) {
      var r=new NetworkPacketReader(plain);var address=r.ReadIPv4Address();int port=r.ReadUInt16();
      int local=await redirect(new(address,port));
      // Unique one-shot listener per redirect prevents cross-character handoffs.
      wire.Data[0]=1;wire.Data[1]=0;wire.Data[2]=0;wire.Data[3]=127;
      wire.Data[4]=(byte)(local>>8);wire.Data[5]=(byte)local;
      frame=wire.ToArray();Interrupted?.Invoke("Server redirect. Resume on replacement connection.");
     }
     if(((byte)plain.Command is 0x05 or 0x52)&&name is not null&&plain.Data.Length>=4) {
      var r=new NetworkPacketReader(plain);Tracker.Identity(name,r.ReadUInt32());
      if(!identified){identified=true;Authenticated?.Invoke(this);}
     }
     await ForwardToClient(frame,plain,token);
    }
   }
  }
 }
 async Task ForwardToClient(byte[] frame,ServerPacket plain,CancellationToken ct) {
  await toClient.WaitAsync(ct);
  try {
   ServerPacket? presentation=null;
   try {lock(movementGate){
    var before=Tracker.Snapshot;
    Tracker.Observe((byte)plain.Command,plain.Data); // Fully decode before confirming a queued step.
    presentation=ObserveMovement(before,(byte)plain.Command,plain.Data);
    if(Tracker.WorldMap is not null)ResetMovement();
   }}
   catch(Exception e) {CancelMovement();Interrupted?.Invoke("Unrecognized server state packet "+((byte)plain.Command).ToString("X2"));Diagnostic?.Invoke("State decode "+((byte)plain.Command).ToString("X2")+": "+e.GetType().Name);}
   bool encrypted=ServerPacketEncryptor.IsEncrypted((byte)plain.Command);
   if(presenting&&encrypted)frame=serverCrypto.Encrypt(plain,clientSequence++).ToArray();
   else if(encrypted)clientSequence=unchecked((byte)(frame[4]+1));
   await client.GetStream().WriteAsync(frame,lifetime.Token);
   if(presentation is not null) {
    presenting=true;await client.GetStream().WriteAsync(serverCrypto.Encrypt(presentation,clientSequence++).ToArray(),lifetime.Token);
   }
  }finally{toClient.Release();}
 }
 public async Task PresentMerchantCloseAsync(long expectedGeneration,long dialogRevision,CancellationToken ct) {
  await toClient.WaitAsync(ct);
  try {
   ct.ThrowIfCancellationRequested();
   var s=Tracker.Snapshot;
   if(!Connected||!s.Connected||s.Generation!=expectedGeneration)throw new IOException("Session generation no longer active.");
   if(s.Dialog is not {IsPursuit:false} d||d.Revision!=dialogRevision)throw new DAQuester.Core.QuestBlockedException("Stale merchant dialog.");
   // Pinned Arbiter CloseCurrentDialog: merchant menus close only in the client.
   // Do not publish an invented server snapshot or use this as quest evidence.
   presenting=true;
   await client.GetStream().WriteAsync(serverCrypto.Encrypt(new ServerPacket(0x30,[0x0A,0]),clientSequence++).ToArray(),lifetime.Token);
  }finally{toClient.Release();}
 }
 public async Task SendAsync(ClientPacket packet,long expectedGeneration,CancellationToken token,DAQuester.Core.Tile? movementOrigin=null,bool presentMovement=false,int movementCapacity=1,Func<bool>? movementAllowed=null) {
  await toServer.WaitAsync(token);
  try {
   token.ThrowIfCancellationRequested();
   if(!Connected||Tracker.Snapshot.Generation!=expectedGeneration||!Tracker.Snapshot.Connected)throw new IOException("Session generation no longer active.");
   if((byte)packet.Command is 0x39 or 0x3A)DialogAction?.Invoke(packet);
   bool movement=(byte)packet.Command==0x06&&packet.Data.Length>=1;
   if(movement)await toClient.WaitAsync(token);
   try{
    token.ThrowIfCancellationRequested();
    lock(movementGate){
     if(movement){
      if(movementAllowed?.Invoke()==false)throw new MovementReplanException();
      RegisterMovement(packet.Data[0],expectedGeneration,movementOrigin,presentMovement,movementCapacity);
      if(packet.Data.Length>=2)packet.Data[1]=walkCounter++;
     }
     if((byte)packet.Command==0x38)refreshRequested=true;
    }
    var encrypted=clientCrypto.Encrypt(packet,sequence);
    if(ClientPacketEncryptor.IsEncrypted((byte)packet.Command))sequence++;
    await remote.GetStream().WriteAsync(encrypted.ToArray(),lifetime.Token);
    if(movement&&presentMovement){
     MovementIntent intent;lock(movementGate){intent=pendingSteps.Last().Intent;}
     presenting=true;await client.GetStream().WriteAsync(serverCrypto.Encrypt(PlayerPresentation.Move(intent),clientSequence++).ToArray(),lifetime.Token);
    }
   }finally{if(movement)toClient.Release();}
  } finally {toServer.Release();}
 }
 public ValueTask DisposeAsync() {lifetime.Cancel();client.Dispose();remote.Dispose();return ValueTask.CompletedTask;}
}
