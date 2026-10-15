using System.Text.Json;
using DAQuester.App.Controls;
using DAQuester.Core;

namespace DAQuester.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    string artworkDirectory = "";
    readonly HashSet<CharacterAppearance> warmedPortraits = [];

    void WarmArtwork()
    {
        // Headless synthetic runtimes do not preload the installed client's files.
        if (Runtime.Preview && !Program.IsScreenshots)
            return;
        string directory = JourneyGameDirectory;
        if (!directory.Equals(artworkDirectory, StringComparison.OrdinalIgnoreCase))
        {
            artworkDirectory = directory;
            warmedPortraits.Clear();
            WarmQuestArtworkAsync(directory);
        }
        if (directory.Length == 0)
            return;
        ArtworkCache.ForUserData.ReportError = error => Runtime.Log("Artwork cache: " + error);
        foreach (var character in Characters)
        {
            var look = CharacterPreviewAppearance.From(character.PortraitCharacter, character.RememberedAppearance).Look;
            if (warmedPortraits.Add(look))
                WarmPortraitAsync(look, directory);
        }
    }

    async void WarmQuestArtworkAsync(string directory)
    {
        try { await QuestArtworkProvider.PreloadAsync(directory); }
        catch (Exception error) when (ArtworkError(error)) { Runtime.Log("Quest artwork preload: " + error); }
    }

    async void WarmPortraitAsync(CharacterAppearance look, string directory)
    {
        try { await CharacterArtworkProvider.LoadPortraitAsync(look, directory); }
        catch (Exception error) when (ArtworkError(error)) { Runtime.Log("Character artwork preload: " + error); }
    }

    static bool ArtworkError(Exception error) => error is IOException or UnauthorizedAccessException
        or InvalidDataException or OverflowException or ArgumentException or JsonException;
}
