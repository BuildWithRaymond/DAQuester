using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed class CharacterItemVM : ObservableObject
{
    readonly MainWindowViewModel owner;
    internal DesktopRuntime Runtime => owner.Runtime;
    string name, password = "";
    bool expanded;
    string[] legendMarks = [];
    public bool IsExpanded
    {
        get => expanded; set => Set(ref expanded, value);
    }
    public JourneyOverviewVM JourneyOverview => owner.OverviewFor(OriginalName);
    public string OriginalName
    {
        get; private set;
    }
    public string DisplayName => OriginalName.Length > 0 ? OriginalName : Name.Length > 0 ? Name : "New character";

    public string Name
    {
        get => name;
        set
        {
            if (Set(ref name, value))
            {
                RaiseAll();
                NotifyCommands();
                owner.AccountEdited();
            }
        }
    }

    public string Password
    {
        get => password;
        set
        {
            if (Set(ref password, value))
            {
                RaiseAll();
                NotifyCommands();
                owner.AccountEdited();
            }
        }
    }

    public string Initial => DisplayName[..1].ToUpperInvariant();
    public bool IsSelected => owner.SelectedCharacter == this;
    public bool Saved => OriginalName.Length > 0 && Name.Equals(OriginalName, StringComparison.OrdinalIgnoreCase) && Password.Length == 0;
    public string Connection => owner.Runtime.Snapshot(OriginalName).Connected ? "Connected" : "Offline";
    public CharacterSnapshot PortraitCharacter => owner.Runtime.Snapshot(OriginalName);
    public CharacterAppearance? RememberedAppearance => owner.Runtime.Memory(OriginalName).Appearance;
    public string PortraitGameDirectory => Path.GetDirectoryName(owner.Runtime.Settings.ClientPath) ?? "";

    public string LevelText
    {
        get
        {
            var snapshot = owner.Runtime.Snapshot(OriginalName);
            var memory = owner.Runtime.Memory(OriginalName);
            return snapshot.Connected && snapshot.Id is not null && snapshot.Level is { } current ? $"Level {current} · confirmed" : memory.Level is { } last ? $"Level {last} · last observed" : "Level not observed";
        }
    }

    bool HasCurrentLegends => PortraitCharacter.Connected && PortraitCharacter.Id is not null && PortraitCharacter.LegendsKnown;
    public IReadOnlyList<string> LegendMarks => legendMarks;
    public bool HasLegendMarks => legendMarks.Length > 0;
    public string LegendSummary => HasCurrentLegends ? $"{legendMarks.Length} · observed"
        : owner.Runtime.Memory(OriginalName).LegendsObservedAt is not null ? $"{legendMarks.Length} · last observed" : "Not observed";
    public string LegendEmptyText => HasCurrentLegends || owner.Runtime.Memory(OriginalName).LegendsObservedAt is not null
        ? "No legend marks observed" : "Connect this character to observe legend marks.";
    public string LegendText => HasLegendMarks ? string.Join("\n", legendMarks) : LegendEmptyText;

    void RefreshLegends()
    {
        var marks = HasCurrentLegends ? PortraitCharacter.Legends.ToArray() : owner.Runtime.Memory(OriginalName).Legends;
        if (!legendMarks.SequenceEqual(marks))
            legendMarks = marks.ToArray();
    }

    public string LoginText => Password.Length > 0 ? "New password · unsaved" : Name != OriginalName ? "Unsaved character edits" : OriginalName.Length > 0 && CredentialVault.Exists(OriginalName) ? "Password saved securely" : "Manual sign-in";
    public string? LaunchBlocker => Saved ? owner.Runtime.LaunchBlocker(OriginalName) : "Save this character’s edits before launching.";
    public string LaunchTooltip => LaunchBlocker ?? owner.Runtime.LaunchHint(OriginalName) ?? "Launch Dark Ages";
    public string LaunchStatus => LaunchBlocker ?? owner.Runtime.LaunchState(OriginalName);
    public AsyncCommand LaunchCommand
    {
        get;
    }
    public RelayCommand SelectCommand
    {
        get;
    }
    public RelayCommand RemoveCommand
    {
        get;
    }

    public CharacterItemVM(MainWindowViewModel owner, string original)
    {
        this.owner = owner;
        OriginalName = name = original;
        expanded = original.Length == 0;
        LaunchCommand = owner.Command(_ => owner.Runtime.LaunchAsync(OriginalName), _ => LaunchBlocker is null);
        SelectCommand = new(_ =>
        {
            owner.SelectedCharacter = this;
            owner.Page = "Planner";
        }, _ => OriginalName.Length > 0);
        RemoveCommand = new(_ => owner.RemoveCharacter(this));
        RefreshLegends();
    }

    public void Accept(string saved)
    {
        OriginalName = name = saved;
        password = "";
        RefreshLegends();
        RaiseAll();
        NotifyCommands();
    }

    public void Refresh()
    {
        RefreshLegends();
        RaiseAll();
        NotifyCommands();
    }

    void NotifyCommands()
    {
        LaunchCommand.Notify();
        SelectCommand.Notify();
    }
}
