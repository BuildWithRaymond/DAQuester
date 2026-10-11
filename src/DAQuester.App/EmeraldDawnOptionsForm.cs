using DAQuester.Core;
namespace DAQuester.App;

public sealed class EmeraldDawnOptionsForm:ThemedForm {
 readonly ComboBox outcome=new JournalComboBox(){Name="emeraldOutcome",Dock=DockStyle.Fill};
 readonly CheckBox druid=new JournalCheckBox(){Name="emeraldVisitDruid",Text="Visit Gans first · keep the insignia",Dock=DockStyle.Fill};
 readonly Label consequence=new(){Name="emeraldConsequence",Dock=DockStyle.Fill};
 readonly Button start=new JournalButton(){Name="emeraldStart",Text="Confirm outcome",Width=170,Height=38,Enabled=false,Primary=true};
 public EmeraldDawnOptions? Result {get;private set;}
 public EmeraldDawnOptionsForm(string character,EmeraldDawnOptions? saved=null) {
  Text="Emerald Dawn · "+character;ClientSize=new(610,370);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;Font=Theme.Body();FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
  outcome.Items.AddRange(["Aid the Emerald Knights","Aid the Necromancer Order","Sell to Skan"]);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(26),ColumnCount=1,RowCount=6};layout.ColumnStyles.Add(new(SizeType.Percent,100));
  foreach(int height in new[]{58,42,66,40})layout.RowStyles.Add(new(SizeType.Absolute,height));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,44));
  layout.Controls.Add(new Label{Text="Who receives the Emerald Knight Insignia? Choose this character's lasting outcome before starting.",Dock=DockStyle.Fill},0,0);
  outcome.AccessibleName="Emerald Dawn outcome";layout.Controls.Add(outcome,0,1);layout.Controls.Add(consequence,0,2);layout.Controls.Add(druid,0,3);
  layout.Controls.Add(new Label{Text="Gans offers a conversation with no reward. Your chosen recipient still receives the insignia afterward. Choices are saved when the journey begins.",Dock=DockStyle.Fill},0,4);
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Margin=Padding.Empty};var cancel=Theme.Button("Cancel");cancel.DialogResult=DialogResult.Cancel;buttons.Controls.Add(cancel);buttons.Controls.Add(start);layout.Controls.Add(buttons,0,5);
  outcome.SelectedIndexChanged+=(_,_)=>{start.Enabled=outcome.SelectedIndex>=0;consequence.Text=outcome.SelectedIndex switch{0=>"Dislaidir receives the insignia. Goal: Aided the Emerald Knights legend.",1=>"Sennaan receives the insignia. Goal: Aided the Necromancer Order legend.",2=>"Skan buys the insignia for 1,000,000 gold. This sale gives no second legend.",_=>"Choose a recipient. Each final outcome gives away the insignia."};};
  start.Click+=(_,_)=>{Result=new(EmeraldDawnOptions.Outcomes[outcome.SelectedIndex],druid.Checked);Result.Validate();DialogResult=DialogResult.OK;Close();};
  if(saved is not null){outcome.SelectedIndex=Array.IndexOf(EmeraldDawnOptions.Outcomes,saved.Outcome);druid.Checked=saved.VisitDruid;}
  else consequence.Text="Choose a recipient. Each final outcome gives away the insignia.";
  Controls.Add(layout);AcceptButton=start;CancelButton=cancel;Theme.Apply(this);
 }
}
