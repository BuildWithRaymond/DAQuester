namespace DAQuester.App.Controls;

public static class QueueReorder
{
 public static bool CanMove(string owner, string currentCharacter, bool locked, int sourceIndex, int count) =>
  !locked && !string.IsNullOrWhiteSpace(owner) && owner.Equals(currentCharacter, StringComparison.OrdinalIgnoreCase) && sourceIndex >= 0 && sourceIndex < count;
 // A drop boundary is measured before removing the dragged item.
 public static int InsertionTarget(int sourceIndex, int boundary, int count)
 {
  if (count < 1 || sourceIndex < 0 || sourceIndex >= count) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
  if (boundary < 0 || boundary > count) throw new ArgumentOutOfRangeException(nameof(boundary));
  return boundary > sourceIndex ? boundary - 1 : boundary;
 }
}
