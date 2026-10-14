using System.Collections.ObjectModel;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    readonly List<DashboardCharacterVM> dashboardRows = [];
    string dashboardSearch = "", dashboardFilter = "All";
    bool dashboardList;
    DashboardCharacterVM? selectedDashboardCharacter;
    public ObservableCollection<DashboardCharacterVM> DashboardCharacters { get; } = [];
    public bool IsDashboard => Page == "Dashboard";
    public string DashboardSearch { get => dashboardSearch; set { if (Set(ref dashboardSearch, value)) Refresh(); } }
    public string DashboardFilter { get => dashboardFilter; set { if (Set(ref dashboardFilter, value)) Refresh(); } }
    public bool DashboardListView { get => dashboardList; set { if (Set(ref dashboardList, value)) RaiseAll(); } }
    public bool DashboardCardsView => !DashboardListView;
    public bool DashboardShowList => DashboardListView && !DashboardEmpty;
    public bool DashboardAll => DashboardFilter == "All";
    public bool DashboardRunning => DashboardFilter == "Running";
    public bool DashboardAttention => DashboardFilter == "Attention";
    public bool DashboardIdle => DashboardFilter == "Idle";
    public bool DashboardOffline => DashboardFilter == "Offline";
    public int DashboardCount => dashboardRows.Count;
    public int DashboardRunningCount => dashboardRows.Count(c => c.FilterState == "Running");
    public int DashboardAttentionCount => dashboardRows.Count(c => c.FilterState == "Attention");
    public int DashboardIdleCount => dashboardRows.Count(c => c.FilterState == "Idle");
    public int DashboardOfflineCount => dashboardRows.Count(c => c.FilterState == "Offline");
    public bool DashboardEmpty => DashboardCharacters.Count == 0;
    public string DashboardEmptyTitle => DashboardCount == 0 ? "Your characters start here" : "No matching characters";
    public string DashboardEmptyMessage => DashboardCount == 0 ? "Add a character in Characters. Their queue and progress stay with them." : "Clear your search or choose All to see more characters.";
    public IReadOnlyList<HistoryItemVM> DashboardActivity => History.Take(5).ToArray();
    public bool DashboardHasActivity => History.Count > 0;
    public DashboardCharacterVM? SelectedDashboardCharacter
    {
        get => selectedDashboardCharacter;
        set
        {
            if (Set(ref selectedDashboardCharacter, value))
                foreach (var row in dashboardRows) row.Refresh();
        }
    }
    public RelayCommand DashboardFilterCommand { get; private set; } = null!;
    public RelayCommand DashboardViewCommand { get; private set; } = null!;
    void InitializeDashboard()
    {
        DashboardFilterCommand = new(p => DashboardFilter = p as string ?? "All");
        DashboardViewCommand = new(p => DashboardListView = p as string == "List");
    }
    void RefreshDashboard()
    {
        var names = Characters.Where(c => c.OriginalName.Length > 0).Select(c => c.OriginalName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        dashboardRows.RemoveAll(row => !names.Contains(row.Name, StringComparer.OrdinalIgnoreCase));
        foreach (string name in names)
            if (!dashboardRows.Any(row => row.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                dashboardRows.Add(new(this, name));
        foreach (var row in dashboardRows) row.Refresh();
        var visible = dashboardRows.Where(row => (DashboardFilter == "All" || row.FilterState == DashboardFilter)
            && (DashboardSearch.Trim().Length == 0 || string.Join(' ', row.Name, row.JourneyTitle, row.Message, row.Location, row.QueueSummary).Contains(DashboardSearch.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();
        Sync(DashboardCharacters, visible);
        if (selectedDashboardCharacter is null || !visible.Contains(selectedDashboardCharacter))
            SelectedDashboardCharacter = visible.FirstOrDefault();
    }
}
