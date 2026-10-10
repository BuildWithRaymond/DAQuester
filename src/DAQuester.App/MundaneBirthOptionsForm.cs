using DAQuester.Core;
namespace DAQuester.App;
public sealed class MundaneBirthOptionsForm:ThemedForm {
 readonly ComboBox season=new JournalComboBox(){Name="birthSeason",Dock=DockStyle.Fill};
 readonly NumericUpDown age=new JournalNumericUpDown(){Name="birthAge",Minimum=12,Maximum=40,Value=12,DecimalPlaces=0,Dock=DockStyle.Fill};
 readonly ComboBox town=new JournalComboBox(){Name="birthTown",Dock=DockStyle.Fill};
 readonly Button start=new JournalButton(){Name="birthStart",Text="Confirm birth choices",Width=190,Height=38,Enabled=false,Primary=true};
 public MundaneBirthOptions? Result {get;private set;}
 public MundaneBirthOptionsForm(string character,MundaneBirthOptions? saved=null) {
  Text="Mundane Birth · "+character;ClientSize=new(590,420);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;Font=Theme.Body();FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
  season.Items.AddRange(MundaneBirthOptions.Seasons);town.Items.AddRange(MundaneBirthOptions.Towns);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(26),ColumnCount=2,RowCount=7};
  layout.ColumnStyles.Add(new(SizeType.Absolute,105));layout.ColumnStyles.Add(new(SizeType.Percent,100));
  layout.RowStyles.Add(new(SizeType.Absolute,62));for(int i=0;i<3;i++)layout.RowStyles.Add(new(SizeType.Absolute,42));layout.RowStyles.Add(new(SizeType.Absolute,62));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,44));
  var intro=new Label{Text="Choose what Riona will record on this character's legend before starting.",Dock=DockStyle.Fill};layout.Controls.Add(intro,0,0);layout.SetColumnSpan(intro,2);
  void Field(string text,Control control,int row){layout.Controls.Add(new Label{Text=text,AutoSize=true,Anchor=AnchorStyles.Left},0,row);layout.Controls.Add(control,1,row);}
  Field("Season",season,1);Field("Age",new InputField(age),2);Field("Town",town,3);
  var hint=new Label{Text="Choose a whole number from 12–40. Known examples: 12 → Deoch 25; 40 → before Deoch 1. Other ages are recorded by the game.",Dock=DockStyle.Fill};layout.Controls.Add(hint,0,4);layout.SetColumnSpan(hint,2);
  var note=new Label{Text="Birth is a lasting legend choice. Changing an existing birth can cost up to 100,000 XP. DAQuester stops if Riona says birth is already recorded.",Dock=DockStyle.Fill};layout.Controls.Add(note,0,5);layout.SetColumnSpan(note,2);
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Margin=Padding.Empty};start.Margin=new(0,0,8,0);var cancel=Theme.Button("Cancel");cancel.DialogResult=DialogResult.Cancel;buttons.Controls.Add(cancel);buttons.Controls.Add(start);layout.Controls.Add(buttons,0,6);layout.SetColumnSpan(buttons,2);
  void ValidateChoices(){start.Enabled=season.SelectedIndex>=0&&town.SelectedIndex>=0&&age.Value is >=12 and <=40;}
  season.SelectedIndexChanged+=(_,_)=>ValidateChoices();town.SelectedIndexChanged+=(_,_)=>ValidateChoices();age.ValueChanged+=(_,_)=>ValidateChoices();
  start.Click+=(_,_)=>{try{Result=new(season.SelectedItem!.ToString()!,decimal.ToInt32(age.Value),town.SelectedItem!.ToString()!);Result.Validate();DialogResult=DialogResult.OK;Close();}catch(Exception e){Theme.Error(this,e.Message,"Birth choices");}};
  if(saved is not null){season.SelectedItem=saved.Season;town.SelectedItem=saved.Town;if(saved.Age is >=12 and <=40)age.Value=saved.Age;}
  Controls.Add(layout);AcceptButton=start;CancelButton=cancel;Theme.Apply(this);
 }
}
