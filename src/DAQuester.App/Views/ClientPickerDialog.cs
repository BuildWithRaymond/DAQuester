using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace DAQuester.App.Views;

// Reads file names only. Choosing a file neither opens it nor starts a client.
public sealed class ClientPickerDialog : Window
{
 public sealed record FileEntry(string Path, bool IsFolder) { public string Name => System.IO.Path.GetFileName(Path); }
 readonly TextBox location = new() { Name = "clientFolderPath", Watermark = "Folder or full .exe path" };
 readonly ListBox entries = new() { Name = "clientFiles", Classes = { "filePicker" }, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
 readonly TextBlock info = new() { Name = "clientPickerStatus", Classes = { "caption" }, TextWrapping = TextWrapping.Wrap };
 readonly Button choose = new() { Name = "clientChoose", Content = "Use this client", Classes = { "primary" }, IsEnabled = false, MinWidth = 132 };
 readonly Button cancel = new() { Name = "clientCancel", Content = "Cancel", IsCancel = true, IsDefault = true, MinWidth = 95 };
 readonly Button up = new() { Name = "clientUp", Content = "Up", MinWidth = 58 };
 string current = "";
 int loadGeneration;
 public string? SelectedPath { get; private set; }
 public ClientPickerDialog(string initial)
 {
  Title = "DAQuester · Choose Dark Ages"; Width = 760; Height = 590; MinWidth = 620; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner;
  var layout = new Grid { RowDefinitions = new("Auto,Auto,*,Auto"), Margin = new Thickness(24), RowSpacing = 18 };
  var title = new StackPanel { Spacing = 7 }; title.Children.Add(new TextBlock { Text = "Choose your game client", Classes = { "heading" } }); title.Children.Add(new TextBlock { Text = "Choose the Dark Ages .exe used by Launch. Your selection does not start it.", Classes = { "caption" }, TextWrapping = TextWrapping.Wrap }); layout.Children.Add(title);
  var pathRow = new Grid { ColumnDefinitions = new("Auto,*,Auto"), ColumnSpacing = 8 }; pathRow.Children.Add(up); Grid.SetColumn(location, 1); pathRow.Children.Add(location);
  var open = new Button { Name = "clientOpenFolder", Content = "Open folder" }; Grid.SetColumn(open, 2); pathRow.Children.Add(open); Grid.SetRow(pathRow, 1); layout.Children.Add(pathRow);
  entries.ItemTemplate = new FuncDataTemplate<FileEntry>((entry, _) =>
  {
   if (entry is null) return new TextBlock();
   var row = new Grid { ColumnDefinitions = new("22,*,Auto"), ColumnSpacing = 10, Margin = new Thickness(7, 8) };
   row.Children.Add(new Controls.LineIcon { Width = 17, Height = 17, Classes = { entry.IsFolder ? "folderGlyph" : "fileGlyph" }, Data = Geometry.Parse(entry.IsFolder ? "M1,4 L8,4 L10,7 L19,7 L19,18 L1,18 Z" : "M4,1 L12,1 L17,6 L17,19 L4,19 Z M12,1 L12,6 L17,6 M7,10 L14,10 M7,14 L12,14") });
   var name = new TextBlock { Text = entry.Name, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(name, 1); row.Children.Add(name);
   var kind = new TextBlock { Text = entry.IsFolder ? "Folder" : "Application", Classes = { "caption" } }; Grid.SetColumn(kind, 2); row.Children.Add(kind); return row;
  });
  var card = new Border { Classes = { "card" }, Child = entries }; Grid.SetRow(card, 2); layout.Children.Add(card);
  var footer = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 16 }; footer.Children.Add(info);
  var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, VerticalAlignment = VerticalAlignment.Center }; actions.Children.Add(choose); actions.Children.Add(cancel); Grid.SetColumn(actions, 1); footer.Children.Add(actions); Grid.SetRow(footer, 3); layout.Children.Add(footer); Content = layout;
  up.Click += async (_, _) => { if (current.Length > 0) await LoadDirectoryAsync(Directory.GetParent(current)?.FullName ?? current); };
  open.Click += async (_, _) => await OpenPathAsync(); location.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await OpenPathAsync(); } };
  entries.SelectionChanged += (_, _) =>
  {
   SelectedPath = entries.SelectedItem is FileEntry { IsFolder: false } file ? file.Path : null; choose.IsEnabled = SelectedPath is not null;
   if (SelectedPath is not null) { info.Classes.Remove("error"); info.Text = System.IO.Path.GetFileName(SelectedPath); }
  };
  entries.DoubleTapped += async (_, _) => await OpenSelectionAsync(); entries.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await OpenSelectionAsync(); } };
  choose.Click += (_, _) => Choose(); cancel.Click += (_, _) => Close(null);
  Opened += async (_, _) =>
  {
   MainWindow.ApplyDarkChrome(this); cancel.Focus();
   string directory; try { directory = Directory.Exists(initial) ? initial : System.IO.Path.GetDirectoryName(initial) ?? ""; } catch (ArgumentException) { directory = ""; }
   if (!Directory.Exists(directory)) directory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
   await LoadDirectoryAsync(directory, initial);
  };
 }
 async Task OpenPathAsync()
 {
  string target = location.Text?.Trim() ?? "";
  if (File.Exists(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { string file = System.IO.Path.GetFullPath(target); await LoadDirectoryAsync(System.IO.Path.GetDirectoryName(file)!, file); return; }
  await LoadDirectoryAsync(target);
 }
 async Task OpenSelectionAsync() { if (entries.SelectedItem is FileEntry file) { if (file.IsFolder) await LoadDirectoryAsync(file.Path); else Choose(); } }
 void Choose()
 {
  if (SelectedPath is null || !SelectedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(SelectedPath)) { info.Classes.Add("error"); info.Text = "Choose an existing .exe application."; choose.IsEnabled = false; return; }
  Close(SelectedPath);
 }
 public async Task LoadDirectoryAsync(string folder, string? selectFile = null)
 {
  int generation = ++loadGeneration; choose.IsEnabled = false; SelectedPath = null; info.Classes.Remove("error"); info.Text = "Reading this folder…";
  try
  {
   if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Enter a folder or application path.");
   string directory = System.IO.Path.GetFullPath(folder.Trim());
   var files = await Task.Run(() => Directory.EnumerateDirectories(directory).OrderBy(System.IO.Path.GetFileName, StringComparer.CurrentCultureIgnoreCase).Select(p => new FileEntry(p, true)).Concat(Directory.EnumerateFiles(directory, "*.exe").OrderBy(System.IO.Path.GetFileName, StringComparer.CurrentCultureIgnoreCase).Select(p => new FileEntry(p, false))).ToArray());
   if (generation != loadGeneration) return;
   current = directory; location.Text = current; entries.ItemsSource = files; up.IsEnabled = Directory.GetParent(current) is not null;
   int applications = files.Count(f => !f.IsFolder); info.Text = applications == 0 ? "No .exe files here. Open a folder or paste a full application path." : $"{applications} applications · choose your Dark Ages client";
   if (selectFile is not null) entries.SelectedItem = files.FirstOrDefault(f => !f.IsFolder && f.Path.Equals(selectFile, StringComparison.OrdinalIgnoreCase));
  }
  catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
  {
   if (generation != loadGeneration) return;
   entries.ItemsSource = Array.Empty<FileEntry>(); info.Classes.Add("error"); info.Text = "Can’t open this folder. Check the path or choose another folder.";
  }
 }
 public static Task<string?> ShowAsync(Window owner, string initial) => new ClientPickerDialog(initial).ShowDialog<string?>(owner);
}
