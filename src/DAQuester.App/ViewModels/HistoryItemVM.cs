using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

// Equal history entries stay in place while their portrait bindings refresh.
public sealed class HistoryItemVM(string character, string quest, string outcome, DateTimeOffset at, string summary, DesktopRuntime? runtime = null) : ObservableObject, IEquatable<HistoryItemVM>
{
    public string Character { get; } = character;
    public string Quest { get; } = quest;
    public string Outcome { get; } = outcome;
    public DateTimeOffset At { get; } = at;
    public string Summary { get; } = summary;
    public string When => At.ToLocalTime().ToString("MMM d, h:mm tt");
    internal DesktopRuntime? Runtime => runtime;
    public CharacterSnapshot? PortraitCharacter => Runtime?.Snapshot(Character);
    public CharacterAppearance? RememberedAppearance => Runtime?.Memory(Character).Appearance;
    public string PortraitGameDirectory => Path.GetDirectoryName(Runtime?.Settings.ClientPath ?? "") ?? "";

    public bool Equals(HistoryItemVM? other) => other is not null && ReferenceEquals(Runtime, other.Runtime)
        && Character == other.Character && Quest == other.Quest && Outcome == other.Outcome
        && At == other.At && Summary == other.Summary;

    public override bool Equals(object? other) => other is HistoryItemVM entry && Equals(entry);
    public override int GetHashCode() => HashCode.Combine(Runtime, Character, Quest, Outcome, At, Summary);
}
