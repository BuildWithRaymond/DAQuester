using DAQuester.Core;
using Xunit;
namespace DAQuester.Tests;
public class JournalLockTests {
 static string Folder()=>Path.Combine(Path.GetTempPath(),"DAQuester-journal-lock-"+Guid.NewGuid());
 [Fact] public async Task Temporary_reader_lock_retries_checkpoint_replace_then_commits() {
  string folder=Folder();var journal=new FileQuestJournal(folder);var job=new QuestJob{Character="Vows",Quest="perfect-hairstyle",Reason="old"};await journal.SaveAsync(job);
  using var reader=new FileStream(Directory.GetFiles(folder,"*.json").Single(),FileMode.Open,FileAccess.Read,FileShare.Read);
  var save=journal.SaveAsync(job with{Reason="new"});await Task.Delay(150);
  Assert.False(save.IsCompleted,"Checkpoint failed immediately under reader lock: "+save.Exception?.GetBaseException().Message);
  reader.Dispose();await save;Assert.Equal("new",System.Text.Json.JsonSerializer.Deserialize<QuestJob>(File.ReadAllText(Directory.GetFiles(folder,"*.json").Single()))!.Reason);
 }
 [Fact] public async Task Cancellation_during_locked_replace_preserves_original_checkpoint() {
  string folder=Folder();var journal=new FileQuestJournal(folder);var job=new QuestJob{Character="Vows",Reason="old"};await journal.SaveAsync(job);
  using var reader=new FileStream(Directory.GetFiles(folder,"*.json").Single(),FileMode.Open,FileAccess.Read,FileShare.Read);using var stop=new CancellationTokenSource(100);
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>journal.SaveAsync(job with{Reason="new"},stop.Token));
  Assert.Equal("old",System.Text.Json.JsonSerializer.Deserialize<QuestJob>(File.ReadAllText(Directory.GetFiles(folder,"*.json").Single()))!.Reason);
 }
 [Fact] public async Task Persistent_reader_lock_fails_bounded_with_original_json_intact() {
  string folder=Folder();var journal=new FileQuestJournal(folder);var job=new QuestJob{Character="Vows",Quest="perfect-hairstyle",Reason="old"};await journal.SaveAsync(job);
  using var reader=new FileStream(Directory.GetFiles(folder,"*.json").Single(),FileMode.Open,FileAccess.Read,FileShare.Read);
  var failure=await Assert.ThrowsAsync<IOException>(async()=>await journal.SaveAsync(job with{Reason="new"}).WaitAsync(TimeSpan.FromSeconds(4)));
  Assert.Contains("checkpoint",failure.Message,StringComparison.OrdinalIgnoreCase);Assert.Contains("Vows",failure.Message);Assert.Equal("old",System.Text.Json.JsonSerializer.Deserialize<QuestJob>(File.ReadAllText(Directory.GetFiles(folder,"*.json").Single()))!.Reason);
 }
}
