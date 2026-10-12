using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DAQuester.App.Controls;
public sealed class QuestGlyph : LineIcon
{
    public static readonly StyledProperty<string?> QuestIdProperty = AvaloniaProperty.Register<QuestGlyph, string?>(nameof(QuestId));
    public string? QuestId
    {
        get => GetValue(QuestIdProperty);
        set => SetValue(QuestIdProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QuestIdProperty)
            Icon = IconFor(QuestId ?? "");
    }

    static string IconFor(string id) => id switch
    {
        var s when s.Contains("faerie", StringComparison.OrdinalIgnoreCase) => "Sparkles",
        var s when s.Contains("stick", StringComparison.OrdinalIgnoreCase) => "GitBranch",
        var s when s.Contains("hair", StringComparison.OrdinalIgnoreCase) => "Scissors",
        var s when s.Contains("emerald", StringComparison.OrdinalIgnoreCase) => "Gem",
        var s when s.Contains("birth", StringComparison.OrdinalIgnoreCase) => "BookOpen",
        var s when s.Contains("muisir", StringComparison.OrdinalIgnoreCase) => "Sprout",
        _ => "ScrollText"
    };
}
