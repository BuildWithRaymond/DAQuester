using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App;

internal sealed class PlayerSettingsPage:UserControl {
 readonly TextBox client=new(){Name="settingsClientPath",Dock=DockStyle.Fill};
 public AppSettings? Result {get;private set;}
 public event Action<AppSettings>? Saved;
 public event Action? Cancelled;
 public PlayerSettingsPage(AppSettings settings,UpdateCoordinator? updates=null,Action? install=null) {
  Name="settingsPage";Dock=DockStyle.Fill;BackColor=Theme.Ink;ForeColor=Theme.Text;Font=Theme.Body();Padding=new(24,16,24,8);
  var scroll=new DarkScrollPanel{Dock=DockStyle.Fill,BackColor=Theme.Ink};
  var layout=new TableLayoutPanel{AutoSize=true,ColumnCount=1,RowCount=12,Margin=Padding.Empty};layout.ColumnStyles.Add(new(SizeType.Percent,100));foreach(int h in new[]{50,52,25,44,42,35,32,55,46,40,48,44})layout.RowStyles.Add(new(SizeType.Absolute,h));
  var title=Theme.Label("Settings",24);title.Font=Theme.Heading();title.Dock=DockStyle.Fill;layout.Controls.Add(title,0,0);
  var intro=Theme.Label("Choose your game client and update preferences. Manage names and passwords in Characters.",10,Theme.Muted);intro.Dock=DockStyle.Fill;layout.Controls.Add(intro,0,1);
  var pathLabel=Theme.Label("Dark Ages application",10,Theme.Text,true);pathLabel.Dock=DockStyle.Fill;layout.Controls.Add(pathLabel,0,2);
  client.Text=settings.ClientPath;var path=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};path.RowStyles.Add(new(SizeType.Percent,100));path.ColumnStyles.Add(new(SizeType.Percent,100));path.ColumnStyles.Add(new(SizeType.Absolute,10));path.ColumnStyles.Add(new(SizeType.Absolute,100));path.Controls.Add(new InputField(client),0,0);
  var browse=Theme.Button("Browse…");browse.Dock=DockStyle.Fill;browse.Width=100;browse.Click+=(_,_)=>{using var picker=new ClientPickerForm(client.Text);if(picker.ShowDialog(this)==DialogResult.OK)client.Text=picker.SelectedPath!;};path.Controls.Add(browse,2,0);layout.Controls.Add(path,0,3);
  var hint=Theme.Label("Used by each character’s Launch button.",9,Theme.Muted);hint.Dock=DockStyle.Fill;layout.Controls.Add(hint,0,4);
  var updateTitle=Theme.Label("Application updates",16);updateTitle.Font=Theme.Heading(16);updateTitle.Dock=DockStyle.Fill;layout.Controls.Add(updateTitle,0,5);
  var automatic=new JournalCheckBox{Name="automaticUpdateChecks",Text="Check for updates when DAQuester opens",Checked=settings.AutomaticUpdateChecks,Dock=DockStyle.Fill,Enabled=updates?.Available==true};layout.Controls.Add(automatic,0,6);
  var updateStatus=Theme.Label("",10,Theme.Muted);updateStatus.Name="updateStatus";updateStatus.Dock=DockStyle.Fill;layout.Controls.Add(updateStatus,0,7);
  var updateActions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
  JournalButton check=Theme.Button("Check for updates"),download=Theme.Button("Download update"),apply=Theme.Button("Install and exit",true);check.Name="checkForUpdates";download.Name="downloadUpdate";apply.Name="installUpdate";check.Width=154;download.Width=150;apply.Width=145;
  updateActions.Controls.AddRange([check,download,apply]);layout.Controls.Add(updateActions,0,8);
  var updateNote=Theme.Label("Update preference is saved immediately. Downloads require your choice. Finish quests and close game clients before installing.",9,Theme.Muted);updateNote.Dock=DockStyle.Fill;layout.Controls.Add(updateNote,0,9);
  void RefreshUpdates(){if(IsDisposed)return;bool available=updates?.Available==true;check.Enabled=available&&!updates!.Working;download.Enabled=available&&!updates!.Working&&updates.AvailableVersion is not null&&updates.PendingVersion is null;apply.Enabled=available&&!updates!.Working&&updates.PendingVersion is not null;updateStatus.Text=available?$"DAQuester {UpdateVersion.Current} · {updates!.Status}":$"DAQuester {UpdateVersion.Current} · Updates are available in the GitHub installer and portable package.";}
  automatic.CheckedChanged+=(_,_)=>{try{settings.AutomaticUpdateChecks=automatic.Checked;settings.Save();if(updates is not null)updates.AutomaticChecks=automatic.Checked;}catch(Exception e){Theme.Error(this,e.Message,"Update settings");}};
  check.Click+=async(_,_)=>{if(updates is not null)await updates.CheckAsync();};
  download.Click+=async(_,_)=>{if(updates is not null)await updates.DownloadAsync();};
  apply.Click+=(_,_)=>{try{install?.Invoke();}catch(Exception e){Theme.Error(this,e.Message,"Install update");}};
  if(updates is not null){updates.Changed+=RefreshUpdates;Disposed+=(_,_)=>updates.Changed-=RefreshUpdates;}RefreshUpdates();
  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var save=Theme.Button("Save settings",true);save.Name="saveSettings";var cancel=Theme.Button("Cancel");cancel.Click+=(_,_)=>{client.Text=settings.ClientPath;Cancelled?.Invoke();};actions.Controls.Add(save);actions.Controls.Add(cancel);layout.Controls.Add(actions,0,10);
  var saved=Theme.Label("",10,Theme.Mint);saved.Name="settingsSaved";saved.Dock=DockStyle.Fill;layout.Controls.Add(saved,0,11);
  save.Click+=(_,_)=>{try{
   string selectedPath=client.Text.Trim();bool clientChanged=!selectedPath.Equals(settings.ClientPath,StringComparison.OrdinalIgnoreCase);
   if(clientChanged&&selectedPath.Length>0&&(!File.Exists(selectedPath)||!selectedPath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Choose an existing Dark Ages .exe application, or leave the path empty to configure it later.");
   Result=JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings,AppSettings.Json),AppSettings.Json)!;Result.ClientPath=selectedPath;Result.Save();settings.ClientPath=selectedPath;saved.Text=clientChanged?"Settings saved. Restart DAQuester to apply the client change.":"Settings saved.";Saved?.Invoke(Result);
  }catch(Exception e){Theme.Error(this,e.Message,"Settings");}};
  scroll.Controls.Add(layout);Controls.Add(scroll);Theme.ApplyControls(this);
 }
}

// Focused dialog hosts also use the same page controls for isolated previews.
