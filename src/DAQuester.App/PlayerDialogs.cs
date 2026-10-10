using System.Diagnostics;
using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App;

internal sealed class QuestLibraryPage:UserControl {
 readonly DataGridView table=new DarkDataGridView(){Name="questLibraryTable",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells};
 readonly TextBox search=new JournalTextBox(){Name="questLibrarySearch",PlaceholderText="Search quests, requirements, or rewards",Dock=DockStyle.Fill};
 readonly ComboBox filter=new JournalComboBox(){Name="questLibraryFilter",Dock=DockStyle.Fill};
 readonly Label count=Theme.Label("",9,Theme.Muted);
 readonly JournalButton choose=Theme.Button("Choose quest",true),guide=Theme.Button("Open guide");
 readonly List<LibraryEntry> entries=[];
 record LibraryEntry(string Name,string Levels,string Items,string Rewards,string Source,string? RecipeId);
 public string? SelectedQuestId {get;private set;}
 public event Action<string>? QuestChosen;
 public QuestLibraryPage(IEnumerable<QuestDefinition> recipes) {
  Name="libraryPage";Dock=DockStyle.Fill;BackColor=Theme.Ink;ForeColor=Theme.Text;Font=Theme.Body();Padding=new(24,16,24,8);
  var definitions=recipes.ToDictionary(q=>q.Name,StringComparer.OrdinalIgnoreCase);
  using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppSettings.Content,"catalog.json")));
  foreach(var q in doc.RootElement.GetProperty("entries").EnumerateArray()){
   string Get(string key)=>q.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
   string name=Get("name");entries.Add(new(name,Get("level"),Get("requiredItems"),Get("rewards"),Get("source"),definitions.GetValueOrDefault(name)?.Id));
  }
  var top=new Panel{Dock=DockStyle.Top,Height=123};var title=Theme.Label("Quest library",24);title.Font=Theme.Heading();title.SetBounds(0,0,650,43);top.Controls.Add(title);
  var intro=Theme.Label($"{entries.Count} legend quests to explore. {definitions.Count} are ready for automatic journeys.",10,Theme.Muted);intro.SetBounds(0,47,800,28);intro.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;top.Controls.Add(intro);
  var tools=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=38,ColumnCount=3,RowCount=1,Margin=Padding.Empty};tools.RowStyles.Add(new(SizeType.Percent,100));tools.ColumnStyles.Add(new(SizeType.Percent,100));tools.ColumnStyles.Add(new(SizeType.Absolute,14));tools.ColumnStyles.Add(new(SizeType.Absolute,230));tools.Controls.Add(new InputField(search),0,0);tools.Controls.Add(filter,2,0);top.Controls.Add(tools);
  filter.Items.AddRange(["All legend quests","Ready to automate","Guide only"]);filter.SelectedIndex=0;
  foreach(string name in new[]{"Quest","Level","Bring with you","Rewards & legend","Availability"})table.Columns.Add(name,name);
  table.Columns[0].FillWeight=140;table.Columns[1].FillWeight=50;table.Columns[2].FillWeight=135;table.Columns[3].FillWeight=240;table.Columns[4].FillWeight=100;
  foreach(DataGridViewColumn column in table.Columns)column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
  var bottom=new Panel{Dock=DockStyle.Bottom,Height=76};count.Dock=DockStyle.Top;count.Height=30;count.TextAlign=ContentAlignment.MiddleLeft;
  var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft};actions.Controls.Add(choose);actions.Controls.Add(guide);bottom.Controls.Add(actions);bottom.Controls.Add(count);
  choose.Name="chooseQuest";choose.Click+=(_,_)=>{if(Selected() is {RecipeId:not null} entry){SelectedQuestId=entry.RecipeId;QuestChosen?.Invoke(entry.RecipeId);}};
  guide.Click+=(_,_)=>{if(Selected() is {} entry&&Uri.TryCreate(entry.Source,UriKind.Absolute,out var url)&&url.Scheme==Uri.UriSchemeHttps)Process.Start(new ProcessStartInfo(url.AbsoluteUri){UseShellExecute=true});};
  search.TextChanged+=(_,_)=>RefreshEntries();filter.SelectedIndexChanged+=(_,_)=>RefreshEntries();table.SelectionChanged+=(_,_)=>UpdateActions();
  Controls.Add(table);Controls.Add(bottom);Controls.Add(top);Theme.ApplyControls(this);table.DefaultCellStyle.WrapMode=DataGridViewTriState.True;RefreshEntries();
 }
 LibraryEntry? Selected()=>table.SelectedRows.Count>0?table.SelectedRows[0].Tag as LibraryEntry:null;
 void UpdateActions(){choose.Enabled=Selected()?.RecipeId is not null;guide.Enabled=Selected() is not null;}
 void RefreshEntries(){
  table.Rows.Clear();string text=search.Text.Trim();
  foreach(var entry in entries.Where(q=>(filter.SelectedIndex!=1||q.RecipeId is not null)&&(filter.SelectedIndex!=2||q.RecipeId is null)&&(text.Length==0||$"{q.Name} {q.Levels} {q.Items} {q.Rewards}".Contains(text,StringComparison.OrdinalIgnoreCase)))){
   int index=table.Rows.Add(entry.Name,entry.Levels,entry.Items,entry.Rewards,entry.RecipeId is null?"Guide only":"Ready to automate");table.Rows[index].Tag=entry;table.Rows[index].Cells[4].Style.ForeColor=entry.RecipeId is null?Theme.Muted:Theme.Mint;
  }
  count.Text=$"{table.Rows.Count} quests shown · guide-only quests need manual play";UpdateActions();
 }
}

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
  var intro=Theme.Label("Choose your game client and update preferences. Manage character names and passwords in Account manager.",10,Theme.Muted);intro.Dock=DockStyle.Fill;layout.Controls.Add(intro,0,1);
  var pathLabel=Theme.Label("Dark Ages application",10,Theme.Text,true);pathLabel.Dock=DockStyle.Fill;layout.Controls.Add(pathLabel,0,2);
  client.Text=settings.ClientPath;var path=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};path.RowStyles.Add(new(SizeType.Percent,100));path.ColumnStyles.Add(new(SizeType.Percent,100));path.ColumnStyles.Add(new(SizeType.Absolute,10));path.ColumnStyles.Add(new(SizeType.Absolute,100));path.Controls.Add(new InputField(client),0,0);
  var browse=Theme.Button("Browse…");browse.Dock=DockStyle.Fill;browse.Width=100;browse.Click+=(_,_)=>{using var picker=new ClientPickerForm(client.Text);if(picker.ShowDialog(this)==DialogResult.OK)client.Text=picker.SelectedPath!;};path.Controls.Add(browse,2,0);layout.Controls.Add(path,0,3);
  var hint=Theme.Label("Used when you choose Open Dark Ages.",9,Theme.Muted);hint.Dock=DockStyle.Fill;layout.Controls.Add(hint,0,4);
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
internal sealed class QuestLibraryForm:ThemedForm {
 readonly QuestLibraryPage page;
 public string? SelectedQuestId=>page.SelectedQuestId;
 public QuestLibraryForm(IEnumerable<QuestDefinition> recipes){Text="DAQuester · Quest library";Size=new(1160,760);MinimumSize=new(950,630);StartPosition=FormStartPosition.CenterParent;page=new(recipes);page.QuestChosen+=_=>{DialogResult=DialogResult.OK;Close();};Controls.Add(page);Theme.Apply(this);}
}
internal sealed class PlayerSettingsForm:ThemedForm {
 readonly PlayerSettingsPage page;
 public AppSettings? Result=>page.Result;
 public PlayerSettingsForm(AppSettings settings,UpdateCoordinator? updates=null,Action? install=null){Text="DAQuester · Settings";ClientSize=new(720,840);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;page=new(settings,updates,install);page.Saved+=_=>{DialogResult=DialogResult.OK;Close();};page.Cancelled+=()=>{DialogResult=DialogResult.Cancel;Close();};Controls.Add(page);Theme.Apply(this);}
}
internal sealed class SavedLoginForm:ThemedForm {
 public SavedLoginForm(string name,bool preview=false){Text="DAQuester · Account manager";ClientSize=new(1000,600);StartPosition=FormStartPosition.CenterParent;MaximizeBox=false;MinimizeBox=false;var page=new AccountManagerPage(AppSettings.Load(),[name],preview);page.Saved+=()=>{DialogResult=DialogResult.OK;Close();};Controls.Add(page);Theme.Apply(this);}
}
