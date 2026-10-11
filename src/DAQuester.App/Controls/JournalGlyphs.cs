using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DAQuester.App.Controls;

// A single stroke family keeps small controls crisp at Windows display scaling.
public class LineIcon : Control
{
 public static readonly StyledProperty<Geometry?> DataProperty = AvaloniaProperty.Register<LineIcon, Geometry?>(nameof(Data));
 public static readonly StyledProperty<IBrush?> ForegroundProperty = TextBlock.ForegroundProperty.AddOwner<LineIcon>();
 public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
 public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
 static LineIcon() { AffectsRender<LineIcon>(DataProperty, ForegroundProperty); }
 public override void Render(DrawingContext context)
 {
  base.Render(context); if (Data is null || Bounds.Width <= 2 || Bounds.Height <= 2) return;
  var box = Data.Bounds; double scale = Math.Min((Bounds.Width - 2) / Math.Max(1, box.Width), (Bounds.Height - 2) / Math.Max(1, box.Height));
  var transform = Matrix.CreateTranslation(-box.X, -box.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation((Bounds.Width - box.Width * scale) / 2, (Bounds.Height - box.Height * scale) / 2);
  using (context.PushTransform(transform)) context.DrawGeometry(null, new Pen(Foreground, 1.4 / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), Data);
 }
}

public sealed class QuestGlyph : LineIcon
{
 public static readonly StyledProperty<string?> QuestIdProperty = AvaloniaProperty.Register<QuestGlyph, string?>(nameof(QuestId));
 public string? QuestId { get => GetValue(QuestIdProperty); set => SetValue(QuestIdProperty, value); }
 protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); if (change.Property == QuestIdProperty) Data = Geometry.Parse(PathFor(QuestId ?? "")); }
 static string PathFor(string id) => id switch
 {
  var s when s.Contains("faerie", StringComparison.OrdinalIgnoreCase) => "M10,1 L12,8 L19,10 L12,12 L10,19 L8,12 L1,10 L8,8 Z",
  var s when s.Contains("stick", StringComparison.OrdinalIgnoreCase) => "M5,19 L12,1 M9,10 L18,6 M10,8 L4,3",
  var s when s.Contains("hair", StringComparison.OrdinalIgnoreCase) => "M4,12 A3,3 0 1 1 3.99,12 M16,12 A3,3 0 1 1 15.99,12 M5,13 L18,1 M15,13 L2,1",
  var s when s.Contains("emerald", StringComparison.OrdinalIgnoreCase) => "M5,1 L15,1 L19,7 L10,19 L1,7 Z M1,7 L19,7 M5,1 L10,19 L15,1",
  var s when s.Contains("birth", StringComparison.OrdinalIgnoreCase) => "M1,2 L10,2 L10,18 L1,18 Z M10,2 L19,2 L19,18 L10,18 M4,6 L7,6 M13,6 L16,6 M4,10 L7,10 M13,10 L16,10",
  var s when s.Contains("muisir", StringComparison.OrdinalIgnoreCase) => "M1,10 Q2,2 10,2 Q18,2 19,10 Z M7,10 L7,18 L13,18 L13,10 M5,7 L6,7 M13,5 L14,5",
  _ => "M4,1 L16,1 L16,19 L4,19 Z M7,5 L13,5 M7,9 L13,9 M7,13 L11,13"
 };
}

public sealed class GripGlyph : LineIcon
{
 public GripGlyph() { Data = Geometry.Parse("M4,2 L4,3 M11,2 L11,3 M4,8 L4,9 M11,8 L11,9 M4,14 L4,15 M11,14 L11,15"); }
}
