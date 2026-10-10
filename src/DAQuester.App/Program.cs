namespace DAQuester.App;
internal static class Program {
 [STAThread] static void Main(string[] args) {
  if(args.Contains("--mcp")||args.Contains("--control")) {Environment.ExitCode=ControlMain.RunAsync(args).GetAwaiter().GetResult();return;}
  bool smoke=args.Contains("--smoke");
  if(smoke)AppSettings.UseIsolatedData(Path.Combine(Environment.CurrentDirectory,"artifacts","smoke-data-"+Guid.NewGuid().ToString("N")));
  else Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
  if(args.Contains("--check-updates")){
   Directory.CreateDirectory("artifacts");
   try{var backend=new VelopackUpdates();if(!backend.IsInstalled)throw new InvalidOperationException("Use the installer or portable package for update checks.");var available=backend.CheckAsync().GetAwaiter().GetResult();File.WriteAllText("artifacts/update-check.json",System.Text.Json.JsonSerializer.Serialize(new{current=UpdateVersion.Current,available,pending=backend.PendingVersion,repository=Core.UpdateCoordinator.RepositoryUrl}));}
   catch(Exception e){Environment.ExitCode=1;File.WriteAllText("artifacts/update-check.json",System.Text.Json.JsonSerializer.Serialize(new{error=e.Message}));}
   return;
  }
  using var instance=new Mutex(true,smoke?"Local\\DAQuester.Smoke."+Environment.ProcessId:"Local\\DAQuester",out bool ownsInstance);
  if(!ownsInstance)return;
  ApplicationConfiguration.Initialize();
  try {
   using var form=new MainForm();
   if(args.Contains("--smoke")) {
    Directory.CreateDirectory("artifacts");File.WriteAllText("artifacts/app-smoke.txt","Running dark UI smoke checks.");
    form.Show();Application.DoEvents();Directory.CreateDirectory("artifacts");
    var selector=(ComboBox)form.Controls.Find("questSelector",true).Single();
    if(selector.Items.Count!=4)throw new InvalidOperationException("All four executable quests must be selectable.");
    selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="entered-muisir-village");Application.DoEvents();
    if(selector.Text!="Entered Muisir Village")throw new InvalidOperationException("Muisir quest must be visible.");
    var once=(CheckBox)form.Controls.Find("repeatQuest",true).Single();if(once.Enabled||once.Checked)throw new InvalidOperationException("Muisir legend quest must not repeat.");
    selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="perfect-hairstyle");Application.DoEvents();
    if(selector.Text!="Perfect Hairstyle")throw new InvalidOperationException("Hairstyle name must be visible.");
    var weekly=(CheckBox)form.Controls.Find("repeatQuest",true).Single();
    if(!weekly.Checked||weekly.Enabled||weekly.Text!="Weekly until Suomi legend")throw new InvalidOperationException("Hairstyle must show automatic weekly attempts until the legend.");
    selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="mundane-birth");Application.DoEvents();
    if(selector.Text!="Mundane Birth")throw new InvalidOperationException("Selected quest name must be visible: "+selector.Text);
    if(form.BackColor.GetBrightness()>.15f)throw new InvalidOperationException("The player workspace must use the midnight dark theme.");
    var developer=(CheckBox?)form.Controls.Find("developerMode",true).SingleOrDefault();
    if(developer is null||developer.Checked)throw new InvalidOperationException("Developer mode must be available and off by default in smoke tests.");
    var diagnostics=form.Controls.Find("developerTools",true).Single();
    if(diagnostics.Visible)throw new InvalidOperationException("Raw diagnostics must be hidden in player mode.");
    var grid=(DataGridView)form.Controls.Find("characterRoster",true).Single();
    if(grid.Rows.Cast<DataGridViewRow>().Any(row=>row.Cells[10].Value?.ToString()!="Mundane Birth"))throw new InvalidOperationException("Disconnected rows must show the selected quest journal.");
    if(form.FormBorderStyle!=FormBorderStyle.None)throw new InvalidOperationException("The main window must use fully dark custom window chrome.");
    Render(form,"app-smoke");
    foreach(var quest in selector.Items.Cast<Core.QuestDefinition>().ToArray()){selector.SelectedItem=quest;Application.DoEvents();Render(form,"quest-"+quest.Id);}
    selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="mundane-birth");
    var darkSelector=(JournalComboBox)selector;darkSelector.ShowPopup();Application.DoEvents();Render(darkSelector.Popup!,"quest-dropdown");darkSelector.Popup!.Close();
    developer.Checked=true;Application.DoEvents();if(!diagnostics.Visible)throw new InvalidOperationException("Developer mode must reveal diagnostics.");
    for(int i=0;i<30;i++)form.Log("Smoke test diagnostic sample "+i);Application.DoEvents();Render(form,"developer-smoke");
    developer.Checked=false;form.Size=form.MinimumSize;Application.DoEvents();
    Render(form,"compact-smoke");
    using(var library=new QuestLibraryForm(selector.Items.Cast<Core.QuestDefinition>())){
     library.Show();Application.DoEvents();Render(library,"library-smoke");
     var filter=(ComboBox)library.Controls.Find("questLibraryFilter",true).Single();var table=(DataGridView)library.Controls.Find("questLibraryTable",true).Single();var search=(TextBox)library.Controls.Find("questLibrarySearch",true).Single();
     filter.SelectedIndex=1;Application.DoEvents();if(table.Rows.Count!=4)throw new InvalidOperationException("The library must distinguish four available automatic quests.");
     search.Text="Muisir";Application.DoEvents();if(table.Rows.Count!=1||table.Rows[0].Cells[0].Value?.ToString()!="Entered Muisir Village")throw new InvalidOperationException("Quest library search must preserve availability filtering.");Render(library,"library-search-smoke");library.Close();
    }
    var smokeBackend=new SmokeUpdateBackend();var smokeUpdates=new Core.UpdateCoordinator(smokeBackend);
    using(var settings=new PlayerSettingsForm(AppSettings.Load(),smokeUpdates)){
     settings.Show();Application.DoEvents();var automatic=(CheckBox)settings.Controls.Find("automaticUpdateChecks",true).Single();
     var check=(Button)settings.Controls.Find("checkForUpdates",true).Single();var download=(Button)settings.Controls.Find("downloadUpdate",true).Single();var install=(Button)settings.Controls.Find("installUpdate",true).Single();
     if(automatic.Checked||!check.Enabled||download.Enabled||install.Enabled)throw new InvalidOperationException("Updates must default off with explicit check, download and install actions.");
     Render(settings,"settings-smoke");automatic.Checked=true;if(!AppSettings.Load().AutomaticUpdateChecks)throw new InvalidOperationException("Optional update checks must persist.");
     smokeUpdates.StartAsync().GetAwaiter().GetResult();Application.DoEvents();if(smokeBackend.Downloads!=0||!download.Enabled||install.Enabled)throw new InvalidOperationException("Automatic checks must not download updates.");Render(settings,"update-available-smoke");
     download.PerformClick();Application.DoEvents();if(smokeBackend.Downloads!=1||download.Enabled||!install.Enabled)throw new InvalidOperationException("Downloading must expose a separate install action.");Render(settings,"update-ready-smoke");settings.Close();
    }
    using(var login=new SavedLoginForm("Nuclear",preview:true)){login.Show();Application.DoEvents();var save=login.Controls.Find("savePassword",true).Single();var point=login.PointToClient(save.PointToScreen(Point.Empty));if(point.Y+save.Height>login.ClientSize.Height-login.Padding.Bottom)throw new InvalidOperationException("Saved login actions must remain fully visible.");Render(login,"saved-login-smoke");login.Close();}
    using(var picker=new ClientPickerForm(AppSettings.Load().ClientPath)){picker.Show();Application.DoEvents();Render(picker,"client-picker-smoke");picker.Close();}
    var calibration=System.Text.Json.JsonSerializer.Deserialize<Core.Calibration>(File.ReadAllText(Path.Combine(AppSettings.Content,"calibration","datester-7.41.json")),AppSettings.Json)!;
    using(var review=new CalibrationForm(new SmokeSession(),new DarkAges.Diagnostics.ReplayRecorder(),calibration,Path.Combine(AppSettings.DataPath,"smoke-calibration.json"))){review.Show();Application.DoEvents();Render(review,"calibration-smoke");review.Close();}
    using var options=new MundaneBirthOptionsForm("Nuclear");options.Show();Application.DoEvents();
    var season=(ComboBox)options.Controls.Find("birthSeason",true).Single();var town=(ComboBox)options.Controls.Find("birthTown",true).Single();var age=(NumericUpDown)options.Controls.Find("birthAge",true).Single();var start=(Button)options.Controls.Find("birthStart",true).Single();
    if(start.Enabled||season.SelectedIndex!=-1||town.SelectedIndex!=-1||season.Items.Count!=4||town.Items.Count!=6||age.Minimum!=12||age.Maximum!=40||age.DecimalPlaces!=0)throw new InvalidOperationException("Birth choices must require explicit season and town, with integer age 12-40.");
    season.SelectedItem="Fall";town.SelectedItem="Piet";age.Value=25;Application.DoEvents();if(!start.Enabled)throw new InvalidOperationException("Valid birth choices must enable confirmation.");
    Render(options,"birth-options-smoke");
    start.PerformClick();if(options.Result!=new Core.MundaneBirthOptions("Fall",25,"Piet"))throw new InvalidOperationException("Birth form did not return the exact chosen settings.");
    if(options.BackColor.GetBrightness()>.15f)throw new InvalidOperationException("Birth choices must use the shared dark theme.");
    File.WriteAllText("artifacts/app-smoke.txt","Fully dark window chrome and rendered surfaces; player/developer visibility; four quest policies; selected journals; library search and availability; settings, login and file picker; birth choice gating, exact settings; standard/compact rendering passed.");return;
   }
   Application.Run(form);
  }catch(Exception e){if(args.Contains("--smoke")){Directory.CreateDirectory("artifacts");File.WriteAllText("artifacts/app-smoke.txt",e.ToString());Environment.ExitCode=1;}else Theme.Error(null,e.Message,"DAQuester startup");}
 }
 static void Render(Form form,string name){
  CheckButtons(form,form);
  using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(0,0,bitmap.Width,bitmap.Height));bitmap.Save("artifacts/"+name+".png");
  for(int y=0;y<bitmap.Height-8;y+=4)for(int x=0;x<bitmap.Width-8;x+=4){bool light=true;for(int dy=0;dy<8&&light;dy++)for(int dx=0;dx<8;dx++){var c=bitmap.GetPixel(x+dx,y+dy);if(c.R<185||c.G<185||c.B<185){light=false;break;}}if(light)throw new InvalidOperationException($"Light native surface found in {name} at {x},{y}.");}
 }
 static void CheckButtons(Control root,Form form){foreach(Control control in root.Controls){if(control is Button&&control.Visible){var ancestors=new List<Control>();for(Control? p=control.Parent;p is not null&&p!=form;p=p.Parent)ancestors.Add(p);if(!ancestors.Any(p=>p is DarkScrollPanel)){var bounds=new Rectangle(form.PointToClient(control.PointToScreen(Point.Empty)),control.Size);var visible=bounds;foreach(var p in ancestors)visible=Rectangle.Intersect(visible,new(form.PointToClient(p.PointToScreen(Point.Empty)),p.ClientSize));if(visible.Height<bounds.Height-2||visible.Width<bounds.Width-2)throw new InvalidOperationException($"Button is clipped in {form.Text}: {control.Text}");}}CheckButtons(control,form);}}
 sealed class SmokeUpdateBackend:Core.IUpdateBackend {
  public bool IsInstalled=>true;
  public string? PendingVersion {get;private set;}
  public int Downloads;
  public Task<string?> CheckAsync()=>Task.FromResult<string?>("1.1.0");
  public Task DownloadAsync(){Downloads++;PendingVersion="1.1.0";return Task.CompletedTask;}
  public void PrepareExit()=>throw new InvalidOperationException("Smoke tests never install updates.");
 }
 sealed class SmokeSession:Core.IGameSession {
  public Core.CharacterSnapshot Snapshot {get;}=new(){Name="Nuclear"};
  public IReadOnlySet<string> Capabilities {get;}=new HashSet<string>();
  public event Action<Core.CharacterSnapshot>? Changed {add{}remove{}}
  public event Action<string>? Interrupted {add{}remove{}}
  public Task ExecuteAsync(Core.GameCommand command,CancellationToken cancellation)=>throw new NotSupportedException("Smoke tests do not send game commands.");
  public Task<Core.CharacterSnapshot> WaitAsync(Func<Core.CharacterSnapshot,bool> predicate,long after,TimeSpan timeout,CancellationToken cancellation)=>throw new NotSupportedException("Smoke tests do not connect to the game.");
 }
}
