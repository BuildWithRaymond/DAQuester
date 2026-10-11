namespace DAQuester.App;

internal sealed class AccountManagerPage:UserControl {
 readonly AppSettings settings;
 readonly bool preview;
 readonly DataGridView table=new DarkDataGridView(){Name="accountLoginsTable",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,MultiSelect=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,EditMode=DataGridViewEditMode.EditOnEnter};
 readonly Label message=Theme.Label("",10,Theme.Muted);
 readonly JournalButton add=Theme.Button("Add account"),remove=Theme.Button("Remove selected"),save=Theme.Button("Save accounts",true),discard=Theme.Button("Discard edits");
 record Origin(string Name,bool PasswordSaved);
 bool loading,dirty;
 public event Action? Saved;
 public event Action<string>? LaunchRequested;
 public event Action<string>? HomeRequested;
 readonly Func<string,bool> canChange;
 readonly Font statusFont=Theme.Body(9);
 readonly Func<string,string> journeyStatus;
 public AccountManagerPage(AppSettings settings,IEnumerable<string> names,bool preview=false,Func<string,bool>? canChange=null,Func<string,string>? journeyStatus=null){
  this.settings=settings;this.preview=preview;
  this.canChange=canChange??(_=>true);
  this.journeyStatus=journeyStatus??(_=>"");
  Disposed+=(_,_)=>statusFont.Dispose();
  Name="loginPage";Dock=DockStyle.Fill;BackColor=Theme.Ink;ForeColor=Theme.Text;Font=Theme.Body();Padding=new(24,16,24,8);
  var header=new Panel{Dock=DockStyle.Top,Height=112};
  var title=Theme.Label("Characters",24);title.Font=Theme.Heading();title.Dock=DockStyle.Top;title.Height=50;
  var intro=Theme.Label("Every character, ready for their next journey. Launch with a saved password or sign in manually.\nSet home here saves a connected character’s current location. Blank passwords keep existing saved passwords.",10,Theme.Muted);intro.Dock=DockStyle.Fill;
  header.Controls.Add(intro);header.Controls.Add(title);
  table.Columns.Add(new DataGridViewTextBoxColumn{Name="Character",HeaderText="Character name",FillWeight=130,MinimumWidth=140});
  table.Columns.Add(new DataGridViewTextBoxColumn{Name="Password",HeaderText="New password",FillWeight=180,MinimumWidth=160});
  table.Columns.Add(new DataGridViewTextBoxColumn{Name="Status",HeaderText="Status",FillWeight=190,MinimumWidth=180,ReadOnly=true});
  table.Columns[2].DefaultCellStyle.WrapMode=DataGridViewTriState.True;
  table.Columns.Add(new DataGridViewButtonColumn{Name="Launch",HeaderText="Dark Ages",Text="Launch",UseColumnTextForButtonValue=true,FillWeight=90,MinimumWidth=88,ReadOnly=true});
  table.Columns.Add(new DataGridViewButtonColumn{Name="Home",HeaderText="Return location",Text="Set home here",UseColumnTextForButtonValue=true,FillWeight=120,MinimumWidth=126,ReadOnly=true});
  table.CellPainting+=(_,e)=>{
   if(e.RowIndex<0||e.ColumnIndex<3)return;e.PaintBackground(e.ClipBounds,true);var bounds=Rectangle.Inflate(e.CellBounds,-6,-7);using var shape=JournalButton.Rounded(bounds,5);using var fill=new SolidBrush(e.ColumnIndex==3?Theme.Raised:Theme.Surface);using var pen=new Pen(Theme.Line);using var font=Theme.Body(8.5f);e.Graphics!.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(pen,shape);TextRenderer.DrawText(e.Graphics,e.FormattedValue?.ToString()??"",font,bounds,e.ColumnIndex==3?Theme.Gold:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);e.Handled=true;
  };
  table.CellContentClick+=(_,e)=>{
   if(e.RowIndex<0||e.ColumnIndex<3)return;table.EndEdit();var row=table.Rows[e.RowIndex];string name=row.Cells[0].Value?.ToString()?.Trim()??"";
   if(row.Tag is not Origin origin||!origin.Name.Equals(name,StringComparison.OrdinalIgnoreCase)||(row.Cells[1].Value?.ToString()?.Length??0)>0||!settings.Characters.Contains(name,StringComparer.OrdinalIgnoreCase)){message.ForeColor=Theme.Gold;message.Text="Save this character’s edits before launching or setting home.";return;}
   if(e.ColumnIndex==3)LaunchRequested?.Invoke(name);else HomeRequested?.Invoke(name);
  };
  foreach(DataGridViewColumn column in table.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
  table.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
  table.CellFormatting+=(_,e)=>{if(e.ColumnIndex!=1||e.RowIndex<0)return;string value=e.Value?.ToString()??"";var origin=table.Rows[e.RowIndex].Tag as Origin;e.Value=value.Length>0?"●●●●●●●●":origin?.PasswordSaved==true?"Leave blank to keep saved password":"Enter a password";e.CellStyle!.ForeColor=value.Length>0?Theme.Text:Theme.Muted;e.FormattingApplied=true;};
  table.EditingControlShowing+=(_,e)=>{if(e.Control is TextBox editor){bool password=table.CurrentCell?.ColumnIndex==1;editor.UseSystemPasswordChar=password;if(password)editor.Text=table.CurrentCell?.Value?.ToString()??"";}};
  table.CellValueChanged+=(_,e)=>{if(loading||e.RowIndex<0||e.ColumnIndex>=2)return;dirty=true;UpdateRow(table.Rows[e.RowIndex]);UpdateActions();};
  table.SelectionChanged+=(_,_)=>UpdateActions();
  var bottom=new Panel{Dock=DockStyle.Bottom,Height=110,Padding=new(0,12,0,0)};
  message.Name="accountSaveStatus";message.Dock=DockStyle.Top;message.Height=42;
  var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,WrapContents=false};
  add.Name="addAccount";remove.Name="removeAccount";save.Name="saveAccounts";discard.Name="discardAccounts";
  add.Width=138;remove.Width=158;discard.Width=145;save.Width=146;
  actions.Controls.AddRange([add,remove,discard,save]);bottom.Controls.Add(actions);bottom.Controls.Add(message);
  add.Click+=(_,_)=>{int row=table.Rows.Add("","","New account");table.Rows[row].Tag=new Origin("",false);dirty=true;table.ClearSelection();table.CurrentCell=table.Rows[row].Cells[0];table.Rows[row].Selected=true;table.BeginEdit(true);UpdateActions();};
  remove.Click+=(_,_)=>{table.EndEdit();foreach(var row in table.SelectedRows.Cast<DataGridViewRow>().ToArray())table.Rows.Remove(row);dirty=true;UpdateActions();};
  discard.Click+=(_,_)=>LoadAccounts(settings.Characters);
  save.Click+=(_,_)=>SaveAccounts();
  Controls.Add(table);Controls.Add(bottom);Controls.Add(header);Theme.ApplyControls(this);LoadAccounts(names);
 }
 void LoadAccounts(IEnumerable<string> names){
  loading=true;
  try{table.CancelEdit();table.Rows.Clear();foreach(string name in names.Where(n=>n.Length>0&&n.All(char.IsAsciiLetter)).Distinct(StringComparer.OrdinalIgnoreCase)){
    int index=table.Rows.Add(name,"","");table.Rows[index].Tag=new Origin(name,!preview&&CredentialVault.Exists(name));UpdateRow(table.Rows[index]);
   }dirty=false;
  }finally{loading=false;}
  UpdateActions();
 }
 void UpdateRow(DataGridViewRow row){
  var origin=row.Tag as Origin;string name=row.Cells[0].Value?.ToString()?.Trim()??"",password=row.Cells[1].Value?.ToString()??"";
  string saved=password.Length>0?"New password · unsaved":origin is null||name!=origin.Name?"Name changed · unsaved":origin.PasswordSaved?"Password saved":"No saved password";
  string status=journeyStatus(name);row.Cells[2].Value=status.Length==0?saved:status+"\n"+saved;row.Height=status.Length==0?45:64;row.Cells[2].Style.Font=statusFont;
 }
 public void RefreshStatus(){foreach(DataGridViewRow row in table.Rows)UpdateRow(row);}
 void UpdateActions(){
  if(loading)return;remove.Enabled=table.SelectedRows.Count>0;save.Enabled=dirty||table.Rows.Count>0;discard.Enabled=dirty;
  message.ForeColor=Theme.Muted;message.Text=dirty?"Unsaved changes. Save accounts to apply them.":table.Rows.Count==0?"Add your first character to get started.":$"{table.Rows.Count} accounts · click a cell to edit · passwords stay masked";
 }
 void SaveAccounts(){
  try{
   table.EndEdit();
   var rows=table.Rows.Cast<DataGridViewRow>().Select(r=>(Name:r.Cells[0].Value?.ToString()?.Trim()??"",Password:r.Cells[1].Value?.ToString()??"",Original:r.Tag as Origin)).Where(r=>r.Name.Length>0||r.Password.Length>0||r.Original is {Name.Length:>0}).ToArray();
   if(rows.Any(r=>r.Name.Length==0||!r.Name.All(char.IsAsciiLetter)))throw new InvalidDataException("Enter a character name using letters only for every account.");
   if(rows.Select(r=>r.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=rows.Length)throw new InvalidDataException("Each character name must appear only once. Rename or remove duplicate rows.");
   foreach(string existing in settings.Characters)if(!rows.Any(r=>r.Name.Equals(existing,StringComparison.OrdinalIgnoreCase))&&!canChange(existing))throw new InvalidDataException("Stop "+existing+"’s active operation before removing or renaming that character.");
   // Read every original before writing any renamed target, including name swaps.
   var passwords=preview?[]:rows.Select(row=>(row.Name,Password:row.Password.Length>0?row.Password:row.Original is {PasswordSaved:true} original&&!original.Name.Equals(row.Name,StringComparison.OrdinalIgnoreCase)?CredentialVault.Load(original.Name):null)).Where(row=>row.Password is not null).ToArray();
   foreach(var row in passwords)CredentialVault.Save(row.Name,row.Password!);
   var value=System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(settings,AppSettings.Json),AppSettings.Json)!;
   value.Characters=rows.Select(r=>r.Name).ToArray();value.Save();settings.Characters=value.Characters;
   LoadAccounts(settings.Characters);message.ForeColor=Theme.Mint;message.Text=$"Saved {rows.Length} accounts. Your journal is up to date.";Saved?.Invoke();
  }catch(Exception e){message.ForeColor=Theme.Rose;message.Text=e.Message;}
 }
}
