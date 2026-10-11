namespace DAQuester.Core;

public record EmeraldDawnOptions(string Outcome,bool VisitDruid=false) {
 public static readonly string[] Outcomes=["emerald-knights","necromancer-order","rogue-order"];
 public void Validate(){if(!Outcomes.Contains(Outcome,StringComparer.Ordinal))throw new QuestBlockedException("Emerald Dawn choice required: Emerald Knights, Necromancer Order, or sell to Skan.");}
 public string Label=>Outcome switch{"emerald-knights"=>"Aid the Emerald Knights","necromancer-order"=>"Aid the Necromancer Order","rogue-order"=>"Sell to Skan",_=>"Choose an outcome"};
}
public record EmeraldRecoveryReceipt(DateTimeOffset ObservedAt,string Legend,Dictionary<string,int> Rewards) {
 public uint? ExperienceBaseline {get;init;}
}

public static class EmeraldDawnEvidence {
 public const string Insignia="Emerald Knight Insignia",Recovered="Recovered the Emerald Knight Insignia",Knights="Aided the Emerald Knights",Necromancers="Aided the Necromancer Order";
 // Item image 70 in the guide is a candidate sprite. Inventory proves acquisition.
 public const ushort Sprite=70;
 // Recognition is guide-derived; IDs and navigation buttons are observed at runtime.
 public static bool IsDiscoveryPopup(CharacterSnapshot s)=>s.Connected&&s.Map==542&&s.Dialog is {IsPursuit:true,Type:0,Prompt:null} d&&
  d.Generation==s.Generation&&d.Revision>0&&d.Revision<=s.Revision&&d.EntityType is 1 or 2 or 4&&d.Choices.Length==0&&d.Text.Contains(Insignia,StringComparison.OrdinalIgnoreCase);
 public static bool IsNotice(ServerNotice notice,int? map)=>map is 413 or 414 or 542 or 2052 or 660 or 425 or 701&&notice.Type is 1 or 2 or 3 or 4 or 6&&
  (notice.Text.Contains("Emerald Knight",StringComparison.OrdinalIgnoreCase)||notice.Text.Contains("insignia",StringComparison.OrdinalIgnoreCase));
 public static string? Mark(CharacterSnapshot s,string legend)=>s.LegendsKnown?s.Legends.FirstOrDefault(l=>Matches(l,legend)):null;
 public static bool Matches(string value,string legend)=>value.Equals(legend,StringComparison.OrdinalIgnoreCase)||value.StartsWith(legend+" -",StringComparison.OrdinalIgnoreCase);
 public static string? FinalMark(CharacterSnapshot s)=>Mark(s,Knights)??Mark(s,Necromancers);
 // The guide uses both spellings; keep the exact server mark in the journal.
 public static string? RecoveryMark(CharacterSnapshot s)=>Mark(s,Recovered)??Mark(s,"Found Emerald Knight Insignia");
 public static bool HasRecovery(CharacterSnapshot s)=>RecoveryMark(s) is not null;
 static bool RecoveryMatches(string mark)=>Matches(mark,Recovered)||Matches(mark,"Found Emerald Knight Insignia");
 public static string? OutcomeLegend(string outcome)=>outcome switch{"emerald-knights"=>Knights,"necromancer-order"=>Necromancers,_=>null};
 public static int Count(Transaction t)=>t.InventoryCounts.FirstOrDefault(p=>p.Key.Equals(Insignia,StringComparison.OrdinalIgnoreCase)).Value;
 public static bool Fresh(CharacterSnapshot s,Transaction t)=>s.Connected&&s.InventoryKnown&&s.LegendsKnown&&s.LegendsRevision>0&&
  (s.Generation!=t.Generation||s.LegendsRevision>t.Revision);
 public static bool ProvesRecovery(CharacterSnapshot s,Transaction t)=>t.Kind is "emerald-recovery-dialog" or "emerald-recover"&&t.InventoryBaselineKnown&&Fresh(s,t)&&
  Count(t)>=1&&s.Count(Insignia)==Count(t)&&HasRecovery(s)&&!t.Legends.Any(RecoveryMatches);
 public static bool ProvesDelivery(CharacterSnapshot s,Transaction t,EmeraldDawnOptions options) {
  if(t.Kind is not ("emerald-delivery-dialog" or "emerald-deliver")||!t.InventoryBaselineKnown||!Fresh(s,t)||Count(t)<1||s.Count(Insignia)!=Count(t)-1||!HasRecovery(s))return false;
  if(OutcomeLegend(options.Outcome) is {} legend)return Mark(s,legend) is not null&&!t.Legends.Any(l=>Matches(l,legend));
  return options.Outcome=="rogue-order"&&t.Kind=="emerald-deliver"&&s.Gold is {} gold&&(ulong)gold==(ulong)t.Gold+1000000&&FinalMark(s) is null;
 }
 public static Eligibility Eligibility(CharacterSnapshot s) {
  if(!s.Connected||s.Name is null||s.Id is null||s.Level is null||s.Hp is null)return new(EligibilityKind.Unknown,"Waiting for connected identity, level and health.");
  if(s.Level<11||s.Hp==0)return new(EligibilityKind.Blocked,"Emerald Dawn requires level 11+ and a living character.");
  if(!s.LegendsKnown)return new(EligibilityKind.Unknown,"Waiting for own legend.");
  if(FinalMark(s) is not null)return new(EligibilityKind.Eligible,"Emerald Dawn final legend already recorded");
  if(!s.InventoryKnown||s.Weight is null||s.MaxWeight is null||s.Experience is null)return new(EligibilityKind.Unknown,"Waiting for inventory, carrying capacity and experience.");
  if(HasRecovery(s)&&s.Count(Insignia)==0)return new(EligibilityKind.Blocked,"Recovered insignia is no longer carried; reconcile the prior disposition before continuing.");
  if(s.Count(Insignia)==0&&(s.Inventory.Count>=59||s.MaxWeight-s.Weight<1))return new(EligibilityKind.Blocked,"Free an inventory slot and carrying weight for the Emerald Knight Insignia.");
  return new(EligibilityKind.Eligible,"Level 11+; recover one Emerald Knight Insignia");
 }
}
