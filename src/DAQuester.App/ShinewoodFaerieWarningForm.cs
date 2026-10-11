namespace DAQuester.App;

public sealed class ShinewoodFaerieWarningForm:ThemedForm {
 public ShinewoodFaerieWarningForm(string character,string? routeError=null){
  Text="Shinewood safety · "+character;ClientSize=new(610,350);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;
  FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;Font=Theme.Body();
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(26),ColumnCount=1,RowCount=4};layout.ColumnStyles.Add(new(SizeType.Percent,100));
  layout.RowStyles.Add(new(SizeType.Absolute,43));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,55));layout.RowStyles.Add(new(SizeType.Absolute,44));
  var title=Theme.Label("Prepare a quick way home",18,Theme.Gold,true);title.Dock=DockStyle.Fill;layout.Controls.Add(title,0,0);
  layout.Controls.Add(new Label{Name="faerieSafetyWarning",Dock=DockStyle.Fill,Text="Shinewood can be dangerous, especially at lower levels. You have no usable town song/scroll or ready dachaidh.\n\nBring an escape item or prepare dachaidh for a quick return. If you continue without one, DAQuester will walk you back out, which may be dangerous."},0,1);
  layout.Controls.Add(new Label{Name="faerieWalkingRoute",Dock=DockStyle.Fill,Text=routeError is null?"Continue only if you are prepared for the walk back through the forest.":"Walking return cannot be verified. Prepare a town song or dachaidh before starting."},0,2);
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Margin=Padding.Empty};
  var cancel=Theme.Button("Cancel");cancel.Name="faerieCancel";cancel.DialogResult=DialogResult.Cancel;cancel.Width=110;cancel.TabIndex=0;
  var proceed=Theme.Button("Continue and walk back");proceed.Name="faerieContinue";proceed.Width=220;proceed.DialogResult=DialogResult.OK;proceed.Enabled=routeError is null;proceed.TabIndex=1;
  buttons.Controls.Add(cancel);buttons.Controls.Add(proceed);layout.Controls.Add(buttons,0,3);Controls.Add(layout);
  AcceptButton=cancel;CancelButton=cancel;Theme.Apply(this);Shown+=(_,_)=>cancel.Focus();
 }
}
