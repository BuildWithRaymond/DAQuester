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
        var feedback = this.FindControl<CharacterFeedbackLayer>("dashboardJourneyFeedback")!;
        feedback.AnchorFromRight = sprite.Width / 2;
        feedback.Height = shortWindow ? 96 : 154;
        AdaptCards();
    }
    void AdaptCards()
    {
        var scroll = this.FindControl<ScrollViewer>("dashboardCardsScroll")!;
        double available = scroll.Viewport.Width > 0 ? scroll.Viewport.Width : scroll.Bounds.Width;
        if (available <= 0)
            return;
        int columns = Math.Clamp((int)(available / 280), 1, 4);
        double width = Math.Floor(available / columns) - 12;
        foreach (var card in this.FindControl<ItemsControl>("dashboardCards")!.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("dashboardCharacter")))
            if (Math.Abs(card.Width - width) > 0.5)
                card.Width = width;
    }
}
