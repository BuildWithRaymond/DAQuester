using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using DAQuester.App.Services;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    internal void AccountEdited() => AccountStatus = "Unsaved changes. Save accounts to apply them.";
    internal void RemoveCharacter(CharacterItemVM item)
    {
        if (item.OriginalName.Length > 0)
            removedAccounts.Add(item.OriginalName);
        Characters.Remove(item);
        if (SelectedCharacter == item)
            selectedCharacter = Characters.FirstOrDefault();
        AccountEdited();
        RaiseAll();
    }

    void SaveAccounts()
    {
        var settings = Runtime.Settings;
        var previousNames = Characters.Select(c => c.OriginalName).Where(n => n.Length > 0).ToArray();
        var names = AccountPersistence.Save(Characters.Select(c => new AccountEdit(c.OriginalName, c.Name, c.Password)), settings.Characters, CredentialVault.Load, CredentialVault.Save, n =>
        {
            var value = AppSettings.Load();
            value.Characters = n;
            value.Save();
            settings.Characters = n;
        }, n => !Runtime.SchedulerReserves(n) && !Runtime.QueueReserves(n) && !Runtime.IsRunning(n) && !Runtime.RunnerBusy(n) && !Runtime.LaunchPending(n) && !Runtime.ControlActions.Contains(n, StringComparer.OrdinalIgnoreCase));
        foreach (string previous in previousNames.Where(n => !names.Contains(n, StringComparer.OrdinalIgnoreCase)))
            removedAccounts.Add(previous);
        foreach (string saved in names)
            removedAccounts.Remove(saved);
        int index = 0;
        foreach (var item in Characters.Where(c => c.OriginalName.Length > 0 || c.Name.Length > 0 || c.Password.Length > 0).ToArray())
            item.Accept(names[index++]);
        foreach (var blank in Characters.Where(c => c.OriginalName.Length == 0).ToArray())
            Characters.Remove(blank);
        AccountStatus = $"Saved {names.Length} accounts. Passwords stay encrypted by Windows.";
        Refresh();
    }
}
