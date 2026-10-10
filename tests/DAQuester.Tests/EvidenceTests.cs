using Xunit;
using DAQuester.Core;
namespace DAQuester.Tests;
public class EvidenceTests {
 [Fact] public void Completion_reply_survives_following_dialog_close() {
  var h=new EvidenceHistory();var s=FoundationTests.Ready() with{Revision=4,Dialog=new(1,4,42,1,"Callough","Done",true,3,8,0,[])};
  h.Record(s);h.Record(s with{Revision=5,Dialog=null});
  Assert.NotNull(h.Find(1,3,x=>x.Dialog?.Text=="Done"));
 }
 [Fact] public void Old_generation_and_old_revision_are_not_evidence() {
  var h=new EvidenceHistory();h.Record(FoundationTests.Ready() with{Revision=4});
  Assert.Null(h.Find(2,0,_=>true));Assert.Null(h.Find(1,4,_=>true));
 }
}
