namespace DAQuester.App;
internal static class Program {
 [STAThread] static void Main(string[] args) {
  if(args.Contains("--mcp")||args.Contains("--control")) {Environment.ExitCode=ControlMain.RunAsync(args).GetAwaiter().GetResult();return;}
  bool screenshots=args.Contains("--screenshots"),smoke=args.Contains("--smoke")||screenshots;
  if(smoke){
   AppSettings.UseIsolatedData(Path.Combine(Environment.CurrentDirectory,"artifacts","smoke-data-"+Guid.NewGuid().ToString("N")));
   var seed=AppSettings.Load();if(seed.Characters.Length!=0)throw new InvalidOperationException("New profiles must not invent a default character.");seed.Characters=screenshots?["Agent","Elara","Wren"]:["Nuclear"];if(screenshots)seed.ClientPath="";seed.Save();
  }
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
   if(smoke&&!screenshots)ControlVisualChecks.Run();
   using var form=new MainForm();
   if(screenshots){form.Show();Application.DoEvents();ScreenshotCapture.Run(form);return;}
   if(args.Contains("--smoke")) {
    Directory.CreateDirectory("artifacts");File.WriteAllText("artifacts/app-smoke.txt","Running dark UI smoke checks.");
    form.Show();Application.DoEvents();Directory.CreateDirectory("artifacts");
    EligibilityVisualChecks.Run(form);
    QueueVisualChecks.Run(form);
    PlannerVisualChecks.Run(form);
    ((Button)form.Controls.Find("journeyDetails",true).Single()).PerformClick();Application.DoEvents();
    if(form.Controls.Find("workspacePages",true).Length!=1)throw new InvalidOperationException("Left navigation must switch pages inside the main workspace.");
    var selector=(ComboBox)form.Controls.Find("questSelector",true).Single();
    if(selector.Items.Count!=6)throw new InvalidOperationException("All six executable quests must be selectable.");
    selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id==Core.ShinewoodFaerieEvidence.Id);Application.DoEvents();
    var faerieRepeat=(CheckBox)form.Controls.Find("repeatQuest",true).Single();
    if(selector.Text!="Shinewood Faerie"||faerieRepeat.Visible||faerieRepeat.Checked)throw new InvalidOperationException("Faerie quest must be selectable without automatic repeats.");
    Render(form,"shinewood-faerie-journal-smoke");
    var faerieJournal=new Core.FileQuestJournal(Path.Combine(AppSettings.DataPath,"journal"));
    var waitReply=new Core.Dialog(1,2,17,4,"","Sorry, no faeries are willing to bond with you. Come back in one deoch.",true,1173,54,0,[]);
    faerieJournal.SaveAsync(new(){Character="Nuclear",Quest=Core.ShinewoodFaerieEvidence.Id,Step="complete",Status="Completed",CompletedAt=DateTimeOffset.UtcNow,
     FaeriePetWait=waitReply,FaerieUnavailable=[Core.ShinewoodFaerieEvidence.Pet],CompletionText="No Pet Faerie this visit; wait one deoch before trying again; Rucesion Song"}).GetAwaiter().GetResult();
    selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="mundane-birth");selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id==Core.ShinewoodFaerieEvidence.Id);Application.DoEvents();
    if(!form.Controls.Find("questProgressState",true).Single().Text.Contains("one deoch",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Failed Faerie bonding must display its one-deoch wait, including offline.");
    if(!form.Controls.Find("questProgressMessage",true).Single().Text.Contains("No Pet Faerie",StringComparison.Ordinal))throw new InvalidOperationException("Failed pet attempt must never appear as a verified pet reward.");
    Render(form,"shinewood-faerie-pet-wait-smoke");
    faerieJournal.SaveAsync(new(){Character="Nuclear",Quest=Core.ShinewoodFaerieEvidence.Id}).GetAwaiter().GetResult();
    selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="mundane-birth");selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id==Core.ShinewoodFaerieEvidence.Id);Application.DoEvents();
    using(var warning=new ShinewoodFaerieWarningForm("Nuclear")){
     warning.Show();Application.DoEvents();var cancel=(Button)warning.Controls.Find("faerieCancel",true).Single();
     if(warning.AcceptButton!=cancel||warning.CancelButton!=cancel||!cancel.Focused)throw new InvalidOperationException("Faerie warning must default to Cancel and focus it.");
     if(!warning.Controls.Find("faerieContinue",true).Single().Enabled)throw new InvalidOperationException("Confirmed walking route must permit explicit acknowledgment.");
     Render(warning,"shinewood-faerie-warning-smoke");cancel.PerformClick();Application.DoEvents();
     if(warning.DialogResult!=DialogResult.Cancel)throw new InvalidOperationException("Cancel must reject walking departure.");warning.Close();
    }
    using(var warning=new ShinewoodFaerieWarningForm("Nuclear","Missing route")){
     warning.Show();Application.DoEvents();if(warning.Controls.Find("faerieContinue",true).Single().Enabled)throw new InvalidOperationException("Unverified walking return must not permit departure.");
     Render(warning,"shinewood-faerie-route-unavailable-smoke");warning.Close();
    }
    selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="emerald-dawn");Application.DoEvents();
    var emeraldRepeat=(CheckBox)form.Controls.Find("repeatQuest",true).Single();
    if(selector.Text!="Emerald Dawn"||emeraldRepeat.Visible||emeraldRepeat.Checked)throw new InvalidOperationException("Emerald Dawn must be selectable and must not repeat.");
    Render(form,"emerald-dawn-journal-smoke");
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
    var fast=(CheckBox?)form.Controls.Find("fastMode",true).SingleOrDefault();
    if(fast is null||fast.Checked||!fast.Visible||!fast.Enabled)throw new InvalidOperationException("Fast mode must be available in player mode and off by default.");
    fast.Checked=true;Application.DoEvents();
    if(fast.Text!="Fast mode · ready when connected")throw new InvalidOperationException("Offline fast mode must explain when its pace applies.");
    using(var saved=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(AppSettings.Load(),AppSettings.Json)))
     if(!saved.RootElement.GetProperty("FastModeCharacters").EnumerateArray().Any(c=>c.GetString()==grid.Rows[0].Cells[0].Value?.ToString()))throw new InvalidOperationException("Fast mode must persist for the selected character.");
    var preference=AppSettings.Load();if(!preference.FastModeEnabled(grid.Rows[0].Cells[0].Value!.ToString()!.ToUpperInvariant()))throw new InvalidOperationException("Fast mode character matching must ignore name casing.");
    using(var restored=new MainForm()){restored.ToggleJourneyDetails();if(!((CheckBox)restored.Controls.Find("fastMode",true).Single()).Checked)throw new InvalidOperationException("Fast mode must restore when the journal is reopened.");}
    Render(form,"fast-mode-smoke");
    grid.Rows.Add("OtherAisling","Disconnected","—","—","—","—","Idle","preflight","Ready","","Mundane Birth");
    grid.CurrentCell=grid.Rows[1].Cells[0];Application.DoEvents();if(fast.Checked)throw new InvalidOperationException("Fast mode must not carry over to another character.");
    grid.CurrentCell=grid.Rows[0].Cells[0];Application.DoEvents();if(!fast.Checked)throw new InvalidOperationException("Returning to a character must restore its fast mode choice.");
    fast.Checked=false;Application.DoEvents();
    using(var saved=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(AppSettings.Load(),AppSettings.Json)))
     if(saved.RootElement.GetProperty("FastModeCharacters").GetArrayLength()!=0)throw new InvalidOperationException("Unchecking fast mode must persist normal walking.");
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
    form.MinimumSize=new(1024,768);form.Size=form.MinimumSize;Application.DoEvents();
    Render(form,"small-screen-smoke");
    CheckNavigation(form,selector);
    var smokeBackend=new SmokeUpdateBackend();var smokeUpdates=new Core.UpdateCoordinator(smokeBackend);
    using(var settings=PreviewPage(new PlayerSettingsPage(AppSettings.Load(),smokeUpdates),new(720,840),"Settings")){
     settings.Show();Application.DoEvents();var automatic=(CheckBox)settings.Controls.Find("automaticUpdateChecks",true).Single();
     var check=(Button)settings.Controls.Find("checkForUpdates",true).Single();var download=(Button)settings.Controls.Find("downloadUpdate",true).Single();var install=(Button)settings.Controls.Find("installUpdate",true).Single();
     if(automatic.Checked||!check.Enabled||download.Enabled||install.Enabled)throw new InvalidOperationException("Updates must default off with explicit check, download and install actions.");
     Render(settings,"settings-smoke");automatic.Checked=true;if(!AppSettings.Load().AutomaticUpdateChecks)throw new InvalidOperationException("Optional update checks must persist.");
     smokeUpdates.StartAsync().GetAwaiter().GetResult();Application.DoEvents();if(smokeBackend.Downloads!=0||!download.Enabled||install.Enabled)throw new InvalidOperationException("Automatic checks must not download updates.");Render(settings,"update-available-smoke");
     download.PerformClick();Application.DoEvents();if(smokeBackend.Downloads!=1||download.Enabled||!install.Enabled)throw new InvalidOperationException("Downloading must expose a separate install action.");Render(settings,"update-ready-smoke");settings.Close();
    }
    using(var login=PreviewPage(new AccountManagerPage(AppSettings.Load(),["Nuclear"],preview:true),new(1000,600),"Characters")){login.Show();Application.DoEvents();var save=login.Controls.Find("saveAccounts",true).Single();var point=login.PointToClient(save.PointToScreen(Point.Empty));if(point.Y+save.Height>login.ClientSize.Height-login.Padding.Bottom)throw new InvalidOperationException("Saved login actions must remain fully visible.");Render(login,"saved-login-smoke");login.Close();}
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
    CheckEmeraldChoices();
    File.WriteAllText("artifacts/app-smoke.txt","Fully dark window chrome and rendered surfaces; complete rounded control painting and clipped partial repaints; button hover/press/disabled states, capture/focus cancellation, Space/Enter/Escape/mnemonics; repeated repaints and 100/125/150/200% scaled control geometry; visible labels and unclipped actions; player/developer visibility; per-character fast mode defaults, persistence and selection; six quests with existing policies preserved; Shinewood Faerie selection, library, Cancel-default safety warning and unavailable-return gate; Emerald Dawn outcome gating, Gans choices and restored preferences; Run again/cooldown/live ownership controls; in-place pages and retained filters; bulk editing 20 accounts, duplicate validation, immediate journal changes, empty state; isolated encrypted password batch save, blank preservation and rename; client settings and separate update actions; birth choices; standard/compact/1024 px rendering passed.");return;
   }
   Application.Run(form);
  }catch(Exception e){if(smoke){Directory.CreateDirectory("artifacts");File.WriteAllText(screenshots?"artifacts/screenshots.txt":"artifacts/app-smoke.txt",e.ToString());Environment.ExitCode=1;}else Theme.Error(null,e.Message,"DAQuester startup");}
 }
 static ThemedForm PreviewPage(Control page,Size size,string title){var form=new ThemedForm{Text="DAQuester · "+title,ClientSize=size,StartPosition=FormStartPosition.CenterScreen};form.Controls.Add(page);Theme.Apply(form);return form;}
 static void CheckEmeraldChoices(){
  foreach(string id in Core.EmeraldDawnOptions.Outcomes){
   using var form=new EmeraldDawnOptionsForm("SyntheticCharacter");form.Show();Application.DoEvents();
   var outcome=(ComboBox)form.Controls.Find("emeraldOutcome",true).Single();var druid=(CheckBox)form.Controls.Find("emeraldVisitDruid",true).Single();var confirm=(Button)form.Controls.Find("emeraldStart",true).Single();
   if(outcome.Items.Count!=3||outcome.SelectedIndex!=-1||confirm.Enabled||druid.Checked)throw new InvalidOperationException("Emerald Dawn must require an explicit outcome; Gans visit defaults off.");
   outcome.SelectedIndex=Array.IndexOf(Core.EmeraldDawnOptions.Outcomes,id);druid.Checked=true;Application.DoEvents();
   if(!confirm.Enabled)throw new InvalidOperationException("An explicit Emerald Dawn outcome must enable confirmation.");
   Render(form,"emerald-options-"+id+"-smoke");
   confirm.PerformClick();if(form.Result!=new Core.EmeraldDawnOptions(id,true))throw new InvalidOperationException("Emerald Dawn must return the exact chosen outcome and Gans visit.");
   if(form.BackColor.GetBrightness()>.15f)throw new InvalidOperationException("Emerald Dawn choices must use the shared dark theme.");
   using var restored=new EmeraldDawnOptionsForm("SyntheticCharacter",form.Result);restored.Show();Application.DoEvents();
   if(((ComboBox)restored.Controls.Find("emeraldOutcome",true).Single()).SelectedIndex!=Array.IndexOf(Core.EmeraldDawnOptions.Outcomes,id)||!((CheckBox)restored.Controls.Find("emeraldVisitDruid",true).Single()).Checked)throw new InvalidOperationException("Saved Emerald Dawn choices must be restored exactly.");restored.Close();
  }
 }
 static void CheckNavigation(MainForm form,ComboBox selector){
  var pages=form.Controls.Find("workspacePages",true).Single();
  Button Nav(string id)=>(Button)form.Controls.Find("navigate"+id,true).Single();
  void Open(string id){
   Nav(id).PerformClick();Application.DoEvents();
   if(form.OwnedForms.Length!=0)throw new InvalidOperationException("Navigation must not open a separate window.");
   if(pages.Controls.Cast<Control>().Count(p=>p.Visible)!=1||!pages.Controls.Cast<Control>().Any(p=>p.Visible&&p.Name==id+"Page"))throw new InvalidOperationException("Navigation must show exactly the selected main page: "+id);
   if(!((JournalButton)Nav(id)).Active)throw new InvalidOperationException("Current navigation must be highlighted.");
   Render(form,"page-"+id+(form.Width<=1024?"-small-screen":"-standard"));
  }
  Open("journal");
  var search=(TextBox)pages.Controls.Find("plannerSearch",true).Single();var library=pages.Controls.Find("plannerQuestList",true).Single();
  search.Text="Spare";Application.DoEvents();if(library.Controls.Count!=1)throw new InvalidOperationException("Planner search must filter actual quest rows.");
  Open("settings");Open("login");
  if(pages.Controls.Find("settingsCharacters",true).Length!=0)throw new InvalidOperationException("Characters belong only in Account manager.");
  var accounts=pages.Controls.Find("accountLoginsTable",true).SingleOrDefault() as DataGridView??throw new InvalidOperationException("Account manager must show all editable names and passwords together.");
  if(accounts.Rows.Cast<DataGridViewRow>().Any(r=>r.Cells[2].InheritedStyle.WrapMode!=DataGridViewTriState.True||r.Height<r.GetPreferredHeight(0,DataGridViewAutoSizeRowMode.AllCells,true)))throw new InvalidOperationException("Character status must wrap connection and password details into readable lines after theming.");
  if(accounts.ReadOnly||accounts.Columns[0].ReadOnly||accounts.Columns[1].ReadOnly||accounts.Rows.Count!=1||accounts.Rows[0].Cells[0].Value?.ToString()!="Nuclear")throw new InvalidOperationException("Account manager must use real saved names without a fake default.");
  var add=(Button)pages.Controls.Find("addAccount",true).Single();var save=(Button)pages.Controls.Find("saveAccounts",true).Single();var remove=(Button)pages.Controls.Find("removeAccount",true).Single();
  add.PerformClick();accounts.EndEdit();accounts.Rows[1].Cells[0].Value="Clock";save.PerformClick();Application.DoEvents();
  if(!AppSettings.Load().Characters.SequenceEqual(new[]{"Nuclear","Clock"}))throw new InvalidOperationException("Bulk account edits must persist the complete character list.");
  var roster=(DataGridView)form.Controls.Find("characterRoster",true).Single();if(roster.Rows.Count!=2)throw new InvalidOperationException("Saving accounts must update the journal immediately.");
  accounts.Rows[1].Cells[0].Value="nuclear";save.PerformClick();Application.DoEvents();
  if(AppSettings.Load().Characters.Length!=2||pages.Controls.Find("accountSaveStatus",true).Single().ForeColor!=Theme.Rose)throw new InvalidOperationException("Duplicate names must block the batch before saving.");Render(form,"accounts-validation-smoke");
  accounts.Rows[1].Cells[0].Value="Clock";
  for(int i=0;i<18;i++){add.PerformClick();accounts.EndEdit();accounts.Rows[^1].Cells[0].Value="Sample"+(char)('a'+i);}
  save.PerformClick();Application.DoEvents();if(AppSettings.Load().Characters.Length!=20)throw new InvalidOperationException("Account manager must support editing many characters in one batch.");Render(form,"accounts-bulk-small-screen");
  Open("journal");if(search.Text!="Spare"||library.Controls.Count!=1)throw new InvalidOperationException("Library filters must survive page navigation.");
  Open("login");if(accounts.Rows.Count!=20)throw new InvalidOperationException("Account list must survive page navigation.");
  while(accounts.Rows.Count>0){accounts.ClearSelection();accounts.CurrentCell=accounts.Rows[0].Cells[0];accounts.Rows[0].Selected=true;remove.PerformClick();}
  save.PerformClick();Application.DoEvents();if(AppSettings.Load().Characters.Length!=0||roster.Rows.Count!=0)throw new InvalidOperationException("Removing accounts must allow an empty journal.");
  Open("journal");Render(form,"empty-journal-smoke");Open("login");if(save.Enabled)throw new InvalidOperationException("An empty account page must invite adding a real character.");
  foreach(string name in new[]{"Nuclear","Clock"}){add.PerformClick();accounts.EndEdit();accounts.Rows[^1].Cells[0].Value=name;}save.PerformClick();Application.DoEvents();
  Open("settings");var client=(TextBox)pages.Controls.Find("settingsClientPath",true).Single();client.Focus();client.SelectAll();client.SelectedText="";((Button)pages.Controls.Find("saveSettings",true).Single()).PerformClick();
  if(AppSettings.Load().ClientPath!=""||AppSettings.Load().Characters.Length!=2)throw new InvalidOperationException("Client settings must save independently of account names.");
  Open("journal");search.Text="";Application.DoEvents();
  if(library.Controls.OfType<PlannerQuestRow>().Count(r=>r.Entry.RecipeId is not null)!=6)throw new InvalidOperationException("Planner must expose all six automatic recipes.");
  form.Size=new(1450,930);Application.DoEvents();foreach(string page in new[]{"history","settings","login","journal"})Open(page);
  CheckQuestActions(form,selector);CheckOrdinaryCompletion(form,selector);CheckPasswordBatch();
 }
 static void CheckOrdinaryCompletion(MainForm form,ComboBox selector){
  var completed=DateTimeOffset.UtcNow.AddMinutes(-1);var next=completed.AddHours(8);
  var journal=new Core.FileQuestJournal(Path.Combine(AppSettings.DataPath,"journal"));
  journal.SaveAsync(new(){Character="Nuclear",Step="complete",Status="Completed",CompletedAt=completed,NextRun=next,Rewards=new(){{"experience",3500}}}).GetAwaiter().GetResult();
  selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="mundane-birth");
  selector.SelectedItem=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="spare-a-stick");Application.DoEvents();
  var roster=(DataGridView)form.Controls.Find("characterRoster",true).Single();
  var row=roster.Rows.Cast<DataGridViewRow>().Single(r=>r.Cells[0].Value?.ToString()=="Nuclear");roster.CurrentCell=row.Cells[0];row.Selected=true;Application.DoEvents();
  if(row.Cells[6].Value?.ToString()!="Completed for now"||row.Cells[9].Value?.ToString()!=next.ToLocalTime().ToString("g"))throw new InvalidOperationException("An offline ordinary attempt must show completion and its next deadline.");
  var message=form.Controls.Find("questProgressMessage",true).Single().Text;
  if(!message.Contains(next.ToLocalTime().ToString("MMM d, h:mm tt"))||!message.Contains("completed",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Saved cooldown must remain visible while offline.");
  Render(form,"ordinary-completion-smoke");form.Size=new(1024,768);Application.DoEvents();Render(form,"ordinary-completion-small-screen");
 }
 static void CheckPasswordBatch(){
  using var host=new ThemedForm{Text="Isolated account save check",ClientSize=new(1000,600)};
  var settings=AppSettings.Load();var page=new AccountManagerPage(settings,["SyntheticCredential","SyntheticOther"]);host.Controls.Add(page);Theme.Apply(host);host.Show();Application.DoEvents();
  var accounts=(DataGridView)page.Controls.Find("accountLoginsTable",true).Single();var save=(Button)page.Controls.Find("saveAccounts",true).Single();
  accounts.CurrentCell=accounts.Rows[0].Cells[1];accounts.BeginEdit(false);Application.DoEvents();if(accounts.EditingControl is not TextBox password)throw new InvalidOperationException("Password cell must open a text editor.");
  if(!password.UseSystemPasswordChar||password.ReadOnly||password.Text.Length!=0)throw new InvalidOperationException("Account passwords must be editable, masked and free of placeholder text.");
  password.Text="isolated-credential-fixture";accounts.EndEdit();accounts.Rows[1].Cells[1].Value="second-isolated-fixture";
  save.PerformClick();Application.DoEvents();
  if(CredentialVault.Load("SyntheticCredential")!="isolated-credential-fixture"||CredentialVault.Load("SyntheticOther")!="second-isolated-fixture")throw new InvalidOperationException("Bulk password save must round-trip through the Windows account vault.");
  if(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(AppSettings.DataPath,"logins","SYNTHETICCREDENTIAL.bin"))).Contains("isolated-credential-fixture",StringComparison.Ordinal))throw new InvalidOperationException("Stored passwords must be encrypted.");
  if(accounts.Rows.Cast<DataGridViewRow>().Any(r=>r.Cells[1].Value?.ToString() is {Length:>0}))throw new InvalidOperationException("Saved passwords must clear out of editable cells.");
  accounts.Rows[0].Cells[1].Value="blocked-batch-fixture";accounts.Rows[1].Cells[0].Value="SyntheticCredential";save.PerformClick();Application.DoEvents();
  if(CredentialVault.Load("SyntheticCredential")!="isolated-credential-fixture")throw new InvalidOperationException("Invalid account batches must not write passwords.");accounts.Rows[0].Cells[1].Value="";accounts.Rows[1].Cells[0].Value="SyntheticOther";
  accounts.Rows[0].Cells[0].Value="";save.PerformClick();Application.DoEvents();if(AppSettings.Load().Characters.Length!=2||page.Controls.Find("accountSaveStatus",true).Single().ForeColor!=Theme.Rose)throw new InvalidOperationException("Clearing an existing name must require correction or explicit removal.");accounts.Rows[0].Cells[0].Value="SyntheticCredential";
  save.PerformClick();Application.DoEvents();if(CredentialVault.Load("SyntheticCredential")!="isolated-credential-fixture")throw new InvalidOperationException("Blank password cells must preserve saved credentials.");
  accounts.CurrentCell=accounts.Rows[0].Cells[1];accounts.BeginEdit(false);Application.DoEvents();if(accounts.EditingControl is not TextBox{Text.Length:0,UseSystemPasswordChar:true})throw new InvalidOperationException("Stored password hints must never become an editable password.");accounts.EndEdit();
  accounts.Rows[0].Cells[0].Value="SyntheticOther";accounts.Rows[1].Cells[0].Value="SyntheticCredential";save.PerformClick();Application.DoEvents();
  if(CredentialVault.Load("SyntheticOther")!="isolated-credential-fixture"||CredentialVault.Load("SyntheticCredential")!="second-isolated-fixture")throw new InvalidOperationException("Swapping account names must preserve both original passwords.");
  accounts.Rows[0].Cells[0].Value="SyntheticCredential";accounts.Rows[1].Cells[0].Value="SyntheticOther";save.PerformClick();Application.DoEvents();
  accounts.Rows[0].Cells[0].Value="SyntheticRenamed";save.PerformClick();Application.DoEvents();if(CredentialVault.Load("SyntheticRenamed")!="isolated-credential-fixture")throw new InvalidOperationException("Renaming an account must preserve its saved password.");
  Render(host,"accounts-passwords-saved-smoke");host.Close();
 }
 static void CheckQuestActions(MainForm form,ComboBox selector){
  form.Size=new(1024,768);Application.DoEvents();
  var quest=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="spare-a-stick");selector.SelectedItem=quest;
  Button Action(string name)=>(Button)form.Controls.Find(name+"Quest",true).Single();
  var completed=new Core.QuestJob{Character="Nuclear",Status="Completed",Step="complete",CompletedAt=DateTimeOffset.UtcNow.AddHours(-9)};
  form.UpdateQuestActions(quest,completed,true,false,false,false);
  if(!Action("start").Enabled||Action("start").Text!="Run again"||Action("resume").Enabled||Action("pause").Enabled||Action("stop").Enabled)throw new InvalidOperationException("A completed single attempt must offer Run again without locking all buttons.");
  Action("start").Focus();Application.DoEvents();Control? viewport=Action("start").Parent;while(viewport is not null&&viewport is not DarkScrollPanel)viewport=viewport.Parent;
  if(viewport is null||!viewport.ClientRectangle.Contains(new Rectangle(viewport.PointToClient(Action("start").PointToScreen(Point.Empty)),Action("start").Size)))throw new InvalidOperationException("Keyboard focus must reveal Run again in the compact journal.");Render(form,"repeatable-actions-smoke");
  var cooldown=completed with{CompletedAt=DateTimeOffset.UtcNow.AddHours(-1)};form.UpdateQuestActions(quest,cooldown,true,false,false,false);if(Action("start").Enabled)throw new InvalidOperationException("Run again must respect cooldown.");
  form.UpdateQuestActions(quest,cooldown,true,false,false,true);if(!Action("start").Enabled||Action("start").Text!="Schedule")throw new InvalidOperationException("Repeat must offer scheduling during cooldown.");
  var restored=new Core.QuestJob{Character="Nuclear",Status="Running",Step="gather",Reason="Saved checkpoint"};form.UpdateQuestActions(quest,restored,true,false,false,false);
  if(!Action("start").Enabled||!Action("resume").Enabled||Action("pause").Enabled)throw new InvalidOperationException("Saved Running status must not lock controls without a live owner.");
  form.UpdateQuestActions(quest,restored,true,true,false,false);if(Action("start").Enabled||Action("resume").Enabled||!Action("pause").Enabled||!Action("stop").Enabled)throw new InvalidOperationException("A live runner must offer Pause and Stop.");
  var birth=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="mundane-birth");form.UpdateQuestActions(birth,completed with{Quest=birth.Id},true,false,false,false);if(Action("start").Enabled||Action("resume").Enabled)throw new InvalidOperationException("One-time completion must remain final.");
  var hair=selector.Items.Cast<Core.QuestDefinition>().Single(q=>q.Id=="perfect-hairstyle");form.UpdateQuestActions(hair,completed with{Quest=hair.Id,LegendReward=true},true,false,false,true);if(Action("start").Enabled||Action("resume").Enabled)throw new InvalidOperationException("The verified Suomi legend must end weekly attempts.");
  var refusal=new Core.Dialog(1,1,99,1,"Callough","You remind me of my child",true,177,1,0,[]);form.UpdateQuestActions(quest,completed with{PermanentCompletion=new(DateTimeOffset.UtcNow,refusal)},true,false,false,true);if(Action("start").Enabled||Action("resume").Enabled)throw new InvalidOperationException("Permanent server completion must disable further attempts.");
  form.UpdateQuestActions(quest,completed,false,false,false,false);if(Action("start").Enabled)throw new InvalidOperationException("A disconnected character must not start quest actions.");
 }
 static void Render(Form form,string name){
  CheckButtons(form,form);CheckActionLabels(form);
  using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(0,0,bitmap.Width,bitmap.Height));bitmap.Save("artifacts/"+name+".png");
  for(int y=0;y<bitmap.Height-8;y+=4)for(int x=0;x<bitmap.Width-8;x+=4){bool light=true;for(int dy=0;dy<8&&light;dy++)for(int dx=0;dx<8;dx++){var c=bitmap.GetPixel(x+dx,y+dy);if(c.R<185||c.G<185||c.B<185){light=false;break;}}if(light)throw new InvalidOperationException($"Light native surface found in {name} at {x},{y}.");}
 }
 static void CheckButtons(Control root,Form form){foreach(Control control in root.Controls){if(control is Button&&control.Visible){var ancestors=new List<Control>();for(Control? p=control.Parent;p is not null&&p!=form;p=p.Parent)ancestors.Add(p);if(!ancestors.Any(p=>p is DarkScrollPanel)){var bounds=new Rectangle(form.PointToClient(control.PointToScreen(Point.Empty)),control.Size);var visible=bounds;foreach(var p in ancestors)visible=Rectangle.Intersect(visible,new(form.PointToClient(p.PointToScreen(Point.Empty)),p.ClientSize));if(visible.Height<bounds.Height-2||visible.Width<bounds.Width-2)throw new InvalidOperationException($"Button is clipped in {form.Text}: {control.Text}");}}CheckButtons(control,form);}}
 static void CheckActionLabels(Control root){foreach(Control control in root.Controls){if(control is JournalButton{Visible:true,Quiet:false} button){int padding=(int)Math.Round((button.Compact?6:12)*button.DeviceDpi/96f);int required=TextRenderer.MeasureText(button.Text,button.Font,Size.Empty,TextFormatFlags.SingleLine).Width;if(required>button.Width-padding*2)throw new InvalidOperationException($"Action label is truncated: {button.Text} ({button.Width}px button).");}CheckActionLabels(control);}}
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
