using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace DAQuester.Core;
public class FileQuestJournal(string directory):IQuestJournal {
 readonly SemaphoreSlim gate=new(1,1);
 string PathFor(string name,string quest)=>Path.Combine(directory,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToUpperInvariant()+"|"+quest)))+".json");
 public QuestJob Load(string character,string quest) {
  var p=PathFor(character,quest);
  if(!File.Exists(p))return new(){Character=character,Quest=quest};
  using var stream=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
  using var reader=new StreamReader(stream);
  var j=JsonSerializer.Deserialize<QuestJob>(reader.ReadToEnd())??throw new InvalidDataException("Unreadable quest journal.");
  return j.PermanentCompletion is not null?j:j with{Status="Paused",Reason="Restored. Resume after fresh server state."};
 }
 public async Task SaveAsync(QuestJob job,CancellationToken cancellation=default) {
  await gate.WaitAsync(cancellation);
  try {
   Directory.CreateDirectory(directory);var p=PathFor(job.Character,job.Quest);var temp=p+".tmp";
   var bytes=JsonSerializer.SerializeToUtf8Bytes(job,new JsonSerializerOptions{WriteIndented=true});
   using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)) {
    await stream.WriteAsync(bytes,cancellation);stream.Flush(true);
   }
   for(int attempt=0;;attempt++) {
    cancellation.ThrowIfCancellationRequested();
    try{File.Move(temp,p,true);break;}
    catch(Exception e)when(e is IOException or UnauthorizedAccessException&&(e.HResult&0xFFFF) is 5 or 32 or 33) {
     if(attempt>=5)throw new IOException($"Could not replace quest checkpoint for {job.Character}/{job.Quest}: file locked or write access denied. Progress remains in the previous checkpoint.",e);
     await Task.Delay(100<<Math.Min(attempt,2),cancellation);
    }
   }
  } finally {gate.Release();}
 }
}
