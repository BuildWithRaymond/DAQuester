using System.Text.Json;
using DAQuester.App;
using DAQuester.App.ViewModels;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class LegendMarksTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clicked_character_rows_keep_order_icons_and_complete_text_without_using_selected_character(bool remembered)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            var snapshot = Snapshot(["First - Deoch 223, Summer", "Repeated", "Repeated"],
                [("First - Deoch 223, Summer", 3), ("Repeated", 6), ("Repeated", 8)]);
            if (remembered)
                await new FileCharacterMemory(Path.Combine(AppSettings.DataPath, "characters"), runtime.Settings.ServerProfile).ObserveAsync("Elara", snapshot);
            else runtime.SetPreviewSnapshot(snapshot);
            var vm = new MainWindowViewModel(runtime);
            var row = vm.Characters.Single(c => c.OriginalName == "Elara");
            vm.SelectedCharacter = vm.Characters.Single(c => c.OriginalName == "Wren");
            var rows = Rows(row);
            Assert.Equal(snapshot.Legends, rows.Select(r => r.GetProperty("Text").GetString()));
            Assert.Equal(new[] { 3, 6, 8 }, rows.Select(r => r.GetProperty("Icon").GetInt32()));
            Assert.False(rows[^1].GetProperty("HasIcon").GetBoolean());
            Assert.Contains(remembered ? "last observed" : "observed", row.LegendSummary);
            runtime.SetPreviewSnapshot(snapshot with { Revision = 4, LegendsRevision = 4, Legends = [] });
            vm.Refresh();
            Assert.Empty(Rows(row));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Text_only_or_mismatched_metadata_uses_neutral_icons_without_guessing_from_words(bool mismatched)
    {
        var runtime = DashboardTests.Workspace();
        try
        {
            runtime.SetPreviewSnapshot(Snapshot(["Wizard mark", "Loved someone"],
                mismatched ? [("Loved someone", 6), ("Wizard mark", 3)] : []));
            var vm = new MainWindowViewModel(runtime);
            var rows = Rows(vm.Characters.Single(c => c.Name == "Elara"));
            Assert.All(rows, r => Assert.Equal(-1, r.GetProperty("Icon").GetInt32()));
        }
        finally { await runtime.ShutdownAsync(); }
    }

    static JsonElement[] Rows(CharacterItemVM character)
    {
        var property = typeof(CharacterItemVM).GetProperty("LegendRows");
        Assert.NotNull(property);
        return JsonSerializer.SerializeToElement(property.GetValue(character)).EnumerateArray().ToArray();
    }

    static CharacterSnapshot Snapshot(string[] marks, (string Text, int Icon)[] entries) => JsonSerializer.Deserialize<CharacterSnapshot>(
        JsonSerializer.Serialize(new { Name = "Elara", Id = 42, Connected = true, Generation = 7, Revision = 2, LegendsKnown = true,
            LegendsRevision = 2, Legends = marks, LegendEntries = entries.Select(e => new { e.Text, e.Icon }) }))!;
}
