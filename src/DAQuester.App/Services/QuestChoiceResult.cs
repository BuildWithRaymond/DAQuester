using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed record QuestChoiceResult(
    MundaneBirthOptions? BirthOptions = null,
    EmeraldDawnOptions? EmeraldOptions = null,
    bool AllowUnsafeWalking = false,
    SickChildOptions? SickChildOptions = null,
    bool OpenCharacters = false,
    bool AltarNoMarkAccepted = false,
    bool FirstStepsBriefed = false,
    bool TerrorWindowAccepted = false,
    DanaanOptions? DanaanOptions = null,
    WizardyResearchOptions? ResearchOptions = null);
