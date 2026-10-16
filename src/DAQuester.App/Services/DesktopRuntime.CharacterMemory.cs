using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges;

namespace DAQuester.App.Services;

public sealed partial class DesktopRuntime
{
    public CharacterMemory Memory(string name)
    {
        if (name.Length == 0)
            return new();
        try
        {
            return characterMemory.Load(name);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log("Saved character marks could not be read: " + e.Message);
            return new()
            {
                Character = name
            };
        }
    }

    void RememberCharacter(string name, GameSession session, CharacterSnapshot snapshot)
    {
        if (closing)
            return;
        var write = SaveCharacterMemoryAsync(name, session, snapshot);
        characterMemoryWrites.Add(write);
        _ = ForgetWriteAsync(write);
    }

    async Task ForgetWriteAsync(Task write)
    {
        try
        {
            await write;
        }
        finally
        {
            characterMemoryWrites.Remove(write);
        }
    }

    async Task SaveCharacterMemoryAsync(string name, GameSession session, CharacterSnapshot snapshot)
    {
        if (sessions.GetValueOrDefault(name) != session || snapshot.Generation != session.Snapshot.Generation)
            return;
        try
        {
            var before = Memory(name);
            var saved = await characterMemory.ObserveAsync(name, snapshot);
            if (saved != before && sessions.GetValueOrDefault(name) == session && !closing)
                Notify();
        }
        catch (Exception e)
        {
            Log(name + ": character marks could not be saved: " + e.Message);
        }
    }

    async Task ReadOwnLegendAsync(string name, GameSession session)
    {
        if (Preview)
            return;
        try
        {
            var initial = session.Snapshot;
            if (!initial.InventoryKnown && !initial.LegendsKnown)
                await session.WaitAsync(s => s.InventoryKnown || s.LegendsKnown, initial.Revision, TimeSpan.FromSeconds(20), characterMemoryLifetime.Token);
            if (closing || shuttingDown
                || SchedulerReserves(name)
                || sessions.GetValueOrDefault(name) != session
                || !session.Snapshot.Connected
                || session.Snapshot.LegendsKnown
                || controlActions.ContainsKey(name)
                || QueueReserves(name)
                || RunnerBusy(name)
                || IsRunning(name)
                || calibrationTasks.TryGetValue(name, out var capture) && !capture.IsCompleted)
                return;
            await session.ExecuteAsync(new("inspect-self"), characterMemoryLifetime.Token);
        }
        catch (OperationCanceledException) when (characterMemoryLifetime.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            Log(name + ": own legend refresh deferred: " + e.Message);
        }
    }

    public void RememberCharacter(string name)
    {
        if (name.Length == 0 || name.Equals(Settings.LastCharacter, StringComparison.OrdinalIgnoreCase))
            return;
        Settings.LastCharacter = name;
        try
        {
            var persisted = AppSettings.Load();
            persisted.LastCharacter = name;
            persisted.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log("Last selected character not saved: " + e.Message);
        }
    }
}
