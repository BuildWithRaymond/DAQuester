using Xunit;
using DAQuester.DarkAges.Launcher;
namespace DAQuester.Tests;
public class LauncherTests {
 [Fact] public void Unknown_client_is_rejected_without_launch() {
  var p=Path.Combine(Path.GetTempPath(),"DAQuester-tests",Guid.NewGuid().ToString()+".exe");Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllBytes(p,[0,1,2]);
  Assert.Throws<InvalidOperationException>(()=>ClientLauncher.Validate(p));
 }
}
