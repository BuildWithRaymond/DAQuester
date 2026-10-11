using DAQuester.App.ViewModels;
using Xunit;

namespace DAQuester.Tests;

public class AccountPersistenceTests
{
 [Fact]
 public void InvalidBatchDoesNotWriteAnyPasswordsOrNames()
 {
  int writes = 0, commits = 0;
  Assert.Throws<InvalidOperationException>(() => AccountPersistence.Save([
   new("Elara","Elara","new password"),new("Wren","elara","")], ["Elara", "Wren"],
   _ => "stored", (_, _) => writes++, _ => commits++, _ => true));
  Assert.Equal(0, writes); Assert.Equal(0, commits);
 }
 [Fact]
 public void BlankPasswordPreservesCredentialAndRenamesCaptureBeforeSwap()
 {
  var passwords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Elara"] = "first", ["Wren"] = "second" };
  string[]? committed = null;
  var result = AccountPersistence.Save([new("Elara", "Wren", ""), new("Wren", "Elara", "")], ["Elara", "Wren"],
   n => passwords.GetValueOrDefault(n), (n, p) => passwords[n] = p, n => committed = n, _ => true);
  Assert.Equal(["Wren", "Elara"], result); Assert.Equal(result, committed);
  Assert.Equal("first", passwords["Wren"]); Assert.Equal("second", passwords["Elara"]);
 }
 [Fact]
 public void RemovalOfOwnedCharacterRejectsWholeBatch()
 {
  int writes = 0;
  Assert.Throws<InvalidOperationException>(() => AccountPersistence.Save([new("Elara", "Elara", "change")], ["Elara", "Wren"],
   _ => "saved", (_, _) => writes++, _ => { }, n => n != "Wren")); Assert.Equal(0, writes);
 }
 [Fact]
 public void BlankExistingNameRequiresExplicitRemoval()
 {
  Assert.Throws<InvalidOperationException>(() => AccountPersistence.Save([new("Elara", "", "")], ["Elara"], _ => null, (_, _) => { }, _ => { }, _ => true));
 }
 [Fact]
 public void LargeAccountBatchPreservesOrderAndAllowsNewManualLogin()
 {
  var names = Enumerable.Range(0, 20).Select(i => "A" + new string('b', i + 1)).ToArray(); string[]? saved = null;
  Assert.Equal(names, AccountPersistence.Save(names.Select(n => new AccountEdit("", n, "")), [], _ => null, (_, _) => throw new Exception("No password should be written"), n => saved = n, _ => true));
  Assert.Equal(names, saved);
 }
}
