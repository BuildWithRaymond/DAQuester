namespace DAQuester.App.ViewModels;

public sealed record AccountEdit(string OriginalName, string Name, string Password);

public static class AccountPersistence
{
 public static string[] Save(IEnumerable<AccountEdit> edits, IReadOnlyCollection<string> currentNames,
  Func<string, string?> readPassword, Action<string, string> writePassword, Action<string[]> commitNames,
  Func<string, bool> mayRemove)
 {
  var rows = edits.Select(e => e with { Name = e.Name.Trim() })
   .Where(e => e.OriginalName.Length > 0 || e.Name.Length > 0 || e.Password.Length > 0).ToArray();
  if (rows.Any(e => e.Name.Length == 0 || !e.Name.All(char.IsAsciiLetter)))
   throw new InvalidOperationException("Enter a character name using letters only for every account.");
  if (rows.Select(e => e.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Length)
   throw new InvalidOperationException("Each character name must appear only once. Rename or remove duplicate rows.");
  foreach (string existing in currentNames)
   if (!rows.Any(e => e.Name.Equals(existing, StringComparison.OrdinalIgnoreCase)) && !mayRemove(existing))
    throw new InvalidOperationException("Stop " + existing + "'s active operation before removing or renaming that character.");
  // Capture every original credential before any renamed destination is written.
  var passwords = rows.Select(e => (e.Name, Password: e.Password.Length > 0 ? e.Password :
   e.OriginalName.Length > 0 && !e.OriginalName.Equals(e.Name, StringComparison.OrdinalIgnoreCase) ? readPassword(e.OriginalName) : null))
   .Where(e => e.Password is not null).ToArray();
  foreach (var credential in passwords) writePassword(credential.Name, credential.Password!);
  string[] names = rows.Select(e => e.Name).ToArray(); commitNames(names); return names;
 }
}
