using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.Views;

public sealed class QuestChoiceDialog : Window
{
 readonly StackPanel body = new() { Spacing = 14 };
 readonly Button confirm = new() { Name = "choiceConfirm", Classes = { "primary" }, MinWidth = 180, IsEnabled = false };
 readonly Button cancel = new() { Name = "choiceCancel", Content = "Cancel", MinWidth = 95, IsCancel = true };
 readonly TextBlock validation = new() { Name = "choiceValidation", TextWrapping = TextWrapping.Wrap, Classes = { "error" } };
 public QuestChoiceDialog(string character, QuestDefinition quest, QuestJob job, string? routeError)
 {
  Title = quest.Name + " · " + character; Width = 600; Height = 550; MinWidth = 480; MinHeight = 410; CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
  var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(26) };
  var heading = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 22) };
  heading.Children.Add(new TextBlock { Text = character.ToUpperInvariant(), Classes = { "eyebrow" } });
  heading.Children.Add(new TextBlock { Text = quest.Name, Classes = { "heading" }, TextWrapping = TextWrapping.Wrap }); layout.Children.Add(heading);
  var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }; Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
  var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
  actions.Children.Add(confirm); actions.Children.Add(cancel); Grid.SetRow(actions, 2); layout.Children.Add(actions);
  cancel.Click += (_, _) => Close(null);
  KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(null); e.Handled = true; } };
  Opened += (_, _) => { MainWindow.ApplyDarkChrome(this); cancel.Focus(); };
  switch (quest.Id) { case "mundane-birth": BuildBirth(job); break; case "emerald-dawn": BuildEmerald(job); break; case "shinewood-faerie": BuildFaerie(routeError); break; default: confirm.IsVisible = false; body.Children.Add(Copy("This quest has no additional choices.")); break; }
  body.Children.Add(validation);
  // Attach only after quest-specific names are final; styling locks Control.Name.
  Content = layout;
 }
 static TextBlock Copy(string value) => new() { Text = value, Classes = { "muted" }, TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
 void Field(string label, Control control) { var field = new StackPanel { Spacing = 7 }; field.Children.Add(new TextBlock { Text = label, Classes = { "caption" } }); field.Children.Add(control); body.Children.Add(field); }
 void BuildBirth(QuestJob job)
 {
  body.Children.Add(Copy("Choose what Riona will record in this character’s legend. These choices are saved before the journey begins."));
  var season = new ComboBox { Name = "birthSeason", ItemsSource = MundaneBirthOptions.Seasons, PlaceholderText = "Choose a season", HorizontalAlignment = HorizontalAlignment.Stretch };
  var town = new ComboBox { Name = "birthTown", ItemsSource = MundaneBirthOptions.Towns, PlaceholderText = "Choose a town", HorizontalAlignment = HorizontalAlignment.Stretch };
  var age = new NumericUpDown { Name = "birthAge", Minimum = 12, Maximum = 40, Increment = 1, Value = 12, FormatString = "0", HorizontalAlignment = HorizontalAlignment.Stretch };
  Field("Season", season); Field("Age · 12–40", age); Field("Town", town);
  body.Children.Add(Copy("Birth is a lasting legend choice. Changing an existing birth can cost up to 100,000 XP. DAQuester stops if Riona says your birth is already recorded."));
  if (job.BirthOptions is { } saved) { season.SelectedItem = saved.Season; town.SelectedItem = saved.Town; age.Value = saved.Age; }
  bool locked = job.Pending is not null || job.CompletedAt is not null;
  if (locked) { season.IsEnabled = false; town.IsEnabled = false; age.IsEnabled = false; }
  void Validate() { confirm.IsEnabled = season.SelectedItem is string && town.SelectedItem is string && age.Value is >= 12 and <= 40 && decimal.Truncate(age.Value.Value) == age.Value; }
  season.SelectionChanged += (_, _) => Validate(); town.SelectionChanged += (_, _) => Validate(); age.ValueChanged += (_, _) => Validate();
  confirm.Content = "Confirm birth choices"; confirm.IsDefault = true; Validate();
  confirm.Click += (_, _) =>
  {
   try { var value = new MundaneBirthOptions((string)season.SelectedItem!, decimal.ToInt32(age.Value!.Value), (string)town.SelectedItem!); value.Validate(); if (locked && value != job.BirthOptions) throw new QuestBlockedException("Saved birth choices cannot change. Resume to check the existing submission."); Close(new QuestChoiceResult(BirthOptions: value)); }
   catch (Exception error) { validation.Text = error.Message; }
  };
 }
 void BuildEmerald(QuestJob job)
 {
  body.Children.Add(Copy("Who receives the Emerald Knight Insignia? Choose this character’s lasting outcome before starting."));
  var outcomes = new[] { "Aid the Emerald Knights", "Aid the Necromancer Order", "Sell to Skan" };
  var outcome = new ComboBox { Name = "emeraldOutcome", ItemsSource = outcomes, PlaceholderText = "Choose a recipient", HorizontalAlignment = HorizontalAlignment.Stretch };
  var consequence = Copy("Each final outcome gives away the insignia."); consequence.Name = "emeraldConsequence";
  var visit = new CheckBox { Name = "emeraldVisitDruid", Content = "Visit Gans first · keep the insignia" }; Field("Recipient", outcome); body.Children.Add(consequence); body.Children.Add(visit);
  body.Children.Add(Copy("Gans offers a conversation with no reward. Your chosen recipient still receives the insignia afterward. Choices stay with this character once the journey begins."));
  bool locked = job.Pending is not null || job.CompletedAt is not null || job.EmeraldMioasSeen || job.EmeraldBergBriefed || job.EmeraldDruidSeen || job.EmeraldRecovery is not null;
  if (job.EmeraldOptions is { } saved) { outcome.SelectedIndex = Array.IndexOf(EmeraldDawnOptions.Outcomes, saved.Outcome); visit.IsChecked = saved.VisitDruid; }
  outcome.IsEnabled = visit.IsEnabled = !locked;
  void Validate() { confirm.IsEnabled = outcome.SelectedIndex >= 0; consequence.Text = outcome.SelectedIndex switch { 0 => "Dislaidir receives the insignia. Goal: Aided the Emerald Knights legend.", 1 => "Sennaan receives the insignia. Goal: Aided the Necromancer Order legend.", 2 => "Skan buys the insignia for 1,000,000 gold. This sale gives no second legend.", _ => "Choose a recipient. Each final outcome gives away the insignia." }; }
  outcome.SelectionChanged += (_, _) => Validate(); confirm.Content = "Confirm outcome"; confirm.IsDefault = true; Validate();
  confirm.Click += (_, _) =>
  {
   try { var value = new EmeraldDawnOptions(EmeraldDawnOptions.Outcomes[outcome.SelectedIndex], visit.IsChecked == true); value.Validate(); if (locked && value != job.EmeraldOptions) throw new QuestBlockedException("Saved Emerald Dawn choices cannot change. Resume to check the saved outcome."); Close(new QuestChoiceResult(EmeraldOptions: value)); }
   catch (Exception error) { validation.Text = error.Message; }
  };
 }
 void BuildFaerie(string? routeError)
 {
  Height = 440; body.Children.Add(new TextBlock { Text = "Prepare a quick way home", Classes = { "section", "gold" } });
  var warning = Copy("Shinewood can be dangerous, especially at lower levels. You have no usable town song or scroll, and no ready dachaidh.\n\nBring an escape item or prepare dachaidh for a quick return. If you continue without one, DAQuester will walk you back out through the forest."); warning.Name = "faerieSafetyWarning"; body.Children.Add(warning);
  var route = Copy(routeError is null ? "Continue only if you are prepared for the walk back." : "Walking return cannot be verified. Prepare a town song or dachaidh before starting."); route.Name = "faerieWalkingRoute"; body.Children.Add(route);
  confirm.Name = "faerieContinue"; confirm.Content = "Continue and walk back"; confirm.IsEnabled = routeError is null; confirm.Classes.Remove("primary"); cancel.Name = "faerieCancel"; cancel.IsDefault = true;
  confirm.Click += (_, _) => { if (routeError is null) Close(new QuestChoiceResult(AllowUnsafeWalking: true)); };
 }
 public static Task<QuestChoiceResult?> ShowChoiceAsync(Window owner, string character, QuestDefinition quest, QuestJob job, string? routeError) => new QuestChoiceDialog(character, quest, job, routeError).ShowDialog<QuestChoiceResult?>(owner);
 public static async Task ShowErrorAsync(Window owner, string message, string title = "DAQuester")
 {
  var close = new Button { Content = "Got it", Classes = { "primary" }, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
  var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Margin = new Thickness(26) };
  layout.Children.Add(new TextBlock { Text = title, Classes = { "heading" }, Margin = new Thickness(0, 0, 0, 18) });
  var scroll = new ScrollViewer { Content = Copy(message), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }; Grid.SetRow(scroll, 1); layout.Children.Add(scroll); Grid.SetRow(close, 2); layout.Children.Add(close);
  var dialog = new Window { Title = title, Width = 540, Height = 320, MinWidth = 420, MinHeight = 260, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = layout };
  close.Click += (_, _) => dialog.Close(); dialog.Opened += (_, _) => { MainWindow.ApplyDarkChrome(dialog); close.Focus(); }; await dialog.ShowDialog(owner);
 }
}
