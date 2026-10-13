using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace DAQuester.App.Controls;

public sealed class QuestRewards : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);
    public static readonly StyledProperty<string> RewardTextProperty = AvaloniaProperty.Register<QuestRewards, string>(nameof(RewardText), "");
    public static readonly StyledProperty<string> LegendEvidenceProperty = AvaloniaProperty.Register<QuestRewards, string>(nameof(LegendEvidence), "");
    public static readonly StyledProperty<string> GameDirectoryProperty = AvaloniaProperty.Register<QuestRewards, string>(nameof(GameDirectory), "");
    public string RewardText { get => GetValue(RewardTextProperty); set => SetValue(RewardTextProperty, value); }
    public string LegendEvidence { get => GetValue(LegendEvidenceProperty); set => SetValue(LegendEvidenceProperty, value); }
    public string GameDirectory { get => GetValue(GameDirectoryProperty); set => SetValue(GameDirectoryProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != RewardTextProperty && change.Property != LegendEvidenceProperty && change.Property != GameDirectoryProperty)
            return;
        Inlines ??= new InlineCollection();
        Inlines.Clear();
        foreach (var part in LegendRewardParts.Split(RewardText, LegendEvidence))
        {
            if (part.Icon is { } icon)
                Inlines.Add(new InlineUIContainer { BaselineAlignment = BaselineAlignment.Center, Child = new LegendMarkGlyph { Icon = icon, GameDirectory = GameDirectory, Margin = new Thickness(0, 0, 3, 0) } });
            Inlines.Add(new Run(part.Text));
        }
    }
}
