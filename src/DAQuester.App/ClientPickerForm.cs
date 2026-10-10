namespace DAQuester.App;

internal sealed class ClientPickerForm:ThemedForm {
 readonly TextBox path=new(){Name="clientFolderPath",Dock=DockStyle.Fill};
 readonly TableLayoutPanel entries=new(){AutoSize=true,ColumnCount=1,Margin=Padding.Empty,BackColor=Theme.Surface};
 readonly Label info=Theme.Label("Choose your Dark Ages .exe application.",9,Theme.Muted);
 readonly JournalButton choose=Theme.Button("Use this client",true);
 string current="";
 public string? SelectedPath {get;private set;}
 public ClientPickerForm(string initial){
  Text="DAQuester · Choose Dark Ages";ClientSize=new(770,600);MinimumSize=new(660,510);StartPosition=FormStartPosition.CenterParent;Padding=new(24);
  entries.ColumnStyles.Add(new(SizeType.Percent,100));
  var top=new Panel{Dock=DockStyle.Top,Height=107};var title=Theme.Label("Find your game client",24);title.Font=Theme.Display(24);title.SetBounds(0,0,650,45);top.Controls.Add(title);
  var row=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=42,ColumnCount=5,RowCount=1,Margin=Padding.Empty};row.RowStyles.Add(new(SizeType.Percent,100));row.ColumnStyles.Add(new(SizeType.Absolute,54));row.ColumnStyles.Add(new(SizeType.Absolute,10));row.ColumnStyles.Add(new(SizeType.Percent,100));row.ColumnStyles.Add(new(SizeType.Absolute,10));row.ColumnStyles.Add(new(SizeType.Absolute,110));
  var up=Theme.Button("Up");up.Dock=DockStyle.Fill;up.Click+=(_,_)=>LoadDirectory(Directory.GetParent(current)?.FullName??current);row.Controls.Add(up,0,0);row.Controls.Add(new InputField(path),2,0);var open=Theme.Button("Open folder");open.Dock=DockStyle.Fill;open.Click+=(_,_)=>LoadDirectory(path.Text);row.Controls.Add(open,4,0);top.Controls.Add(row);
  var bottom=new Panel{Dock=DockStyle.Bottom,Height=70};var actions=new FlowLayoutPanel{Dock=DockStyle.Right,Width=296,FlowDirection=FlowDirection.RightToLeft,Padding=new(0,15,0,0)};choose.Enabled=false;choose.Click+=(_,_)=>{if(SelectedPath is null)return;DialogResult=DialogResult.OK;Close();};var cancel=Theme.Button("Cancel");cancel.DialogResult=DialogResult.Cancel;actions.Controls.Add(choose);actions.Controls.Add(cancel);info.Dock=DockStyle.Fill;info.Padding=new(0,12,8,0);bottom.Controls.Add(info);bottom.Controls.Add(actions);
  var scroll=new DarkScrollPanel{Dock=DockStyle.Fill,BackColor=Theme.Surface};scroll.Controls.Add(entries);Controls.Add(scroll);Controls.Add(bottom);Controls.Add(top);CancelButton=cancel;Theme.Apply(this);
  string folder=Path.GetDirectoryName(initial)??"";LoadDirectory(Directory.Exists(folder)?folder:Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
 }
 void LoadDirectory(string folder){
  try{
   string directory=Path.GetFullPath(folder.Trim());var folders=Directory.GetDirectories(directory).OrderBy(Path.GetFileName).ToArray();var files=Directory.GetFiles(directory,"*.exe").OrderBy(Path.GetFileName).ToArray();
   current=directory;path.Text=current;SelectedPath=null;choose.Enabled=false;entries.SuspendLayout();foreach(Control c in entries.Controls.Cast<Control>().ToArray()){entries.Controls.Remove(c);c.Dispose();}entries.RowStyles.Clear();entries.RowCount=0;
   foreach(string target in folders.Concat(files)){bool isFolder=Directory.Exists(target);var b=Theme.Button((isFolder?"›   ":"    ")+Path.GetFileName(target));b.Quiet=true;b.Dock=DockStyle.Fill;b.Margin=Padding.Empty;b.Height=40;b.Click+=(_,_)=>{if(isFolder)LoadDirectory(target);else{SelectedPath=target;choose.Enabled=true;info.Text=Path.GetFileName(target);foreach(JournalButton button in entries.Controls){button.Active=button==b;button.Invalidate();}}};int row=entries.RowCount++;entries.RowStyles.Add(new(SizeType.Absolute,40));entries.Controls.Add(b,0,row);}
   info.Text=files.Length==0?"No .exe files here. Open a folder to keep looking.":$"{files.Length} applications · choose the Dark Ages client";entries.ResumeLayout(true);
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException){entries.ResumeLayout(true);info.Text="Can't open this folder. Check the path or choose another folder.";}
 }
}
