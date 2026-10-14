using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DAQuester.App.Controls;

namespace DAQuester.App.Views;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Adapt();
        this.FindControl<ItemsControl>("dashboardCards")!.LayoutUpdated += (_, _) => AdaptCards();
    }
    void Adapt()
    {
        var layout = this.FindControl<Grid>("dashboardListLayout")!;
        bool compact = Bounds.Width < 880;
        layout.ColumnDefinitions[1].Width = new GridLength(compact ? 300 : 390);
        bool shortWindow = Bounds.Height < 520;
        Classes.Set("short", shortWindow);
        var sprite = this.FindControl<CharacterSpritePreview>("dashboardJourneySprite")!;
        sprite.Width = shortWindow ? 80 : 96;
        sprite.Height = shortWindow ? 80 : 112;
        AdaptCards();
    }
    void AdaptCards()
    {
        double available = this.FindControl<ScrollViewer>("dashboardCardsScroll")!.Bounds.Width - 16;
        if (available <= 0) return;
        int columns = Math.Max(1, (int)((available + 12) / 332));
        double width = Math.Floor((available + 12) / columns) - 12;
        foreach (var card in this.FindControl<ItemsControl>("dashboardCards")!.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("dashboardCharacter")))
            if (Math.Abs(card.Width - width) > 0.5) card.Width = width;
    }
}
