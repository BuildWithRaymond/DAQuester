using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class ResearchPotionEvidenceTests
{
    [Theory]
    [InlineData("failure")]
    [InlineData("sorcery")]
    public void Research_status_survives_a_healing_message_before_server_popup_closure(string result)
    {
        var (job, after) = Captured(result);
        var observed = WizardyResearchEvidence.ObserveStatus(job, after with { Dialog = job.Research!.Source });
        Assert.NotNull(observed.Pending);
        Assert.Empty(observed.Research!.Attempts);
        observed = JsonSerializer.Deserialize<QuestJob>(JsonSerializer.Serialize(observed))!;

        var settled = WizardyResearchEvidence.ObserveStatus(observed, WithPotionMessage(after));

        Assert.Null(settled.Pending);
        Assert.Equal(after.ActiveStatus, Assert.Single(settled.Research!.Attempts).Status);
        Assert.Equal(result == "failure" ? 0 : 3, settled.Rewards.GetValueOrDefault("item:Mileth Song"));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("generation")]
    [InlineData("status-generation")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("before-submission")]
    [InlineData("wrong-type")]
    [InlineData("unknown-text")]
    [InlineData("source-identity")]
    [InlineData("source-revision")]
    [InlineData("other-quest")]
    public void Unbound_status_is_not_retained_for_later_completion(string gap)
    {
        var (job, after) = Captured("sorcery");
        var status = after.ActiveStatus!;
        var observation = after with { Dialog = job.Research!.Source };
        observation = gap switch
        {
            "actor" => observation with { Name = "Someoneelse" },
            "generation" => observation with { Generation = after.Generation + 1 },
            "status-generation" => observation with { ActiveStatus = status with { Generation = after.Generation + 1 } },
            "stale" => observation with { ActiveStatus = status with { Revision = job.Research!.Source!.Revision } },
            "future" => observation with { ActiveStatus = status with { Revision = after.Revision + 1 } },
            "before-submission" => observation with { ActiveStatus = status with { ObservedAt = job.Pending!.Started.AddSeconds(-1) } },
            "wrong-type" => observation with { ActiveStatus = status with { Type = 1 } },
            "unknown-text" => observation with { ActiveStatus = status with { Text = "Synthetic unknown research result" } },
            _ => observation
        };
        job = gap switch
        {
            "source-identity" => job with { Research = job.Research! with { Source = job.Research.Source! with { EntityId = 88 } } },
            "source-revision" => job with { Research = job.Research! with { Source = job.Research.Source! with { Revision = job.Pending!.Revision } } },
            "other-quest" => job with { Quest = "other-quest" },
            _ => job
        };

        var observed = WizardyResearchEvidence.ObserveStatus(job, observation);
        var settled = WizardyResearchEvidence.ObserveStatus(observed, WithPotionMessage(after));

        Assert.NotNull(settled.Pending);
        Assert.Empty(settled.Research!.Attempts);
        Assert.Empty(settled.Rewards);
    }

    [Theory]
    [InlineData("popup-open")]
    [InlineData("parchment-left")]
    [InlineData("no-song")]
    [InlineData("unknown-bag")]
    [InlineData("gold")]
    [InlineData("generation")]
    [InlineData("actor")]
    public void Retained_status_still_requires_the_original_resource_and_dialog_receipts(string gap)
    {
        var (job, after) = Captured("sorcery");
        var observed = WizardyResearchEvidence.ObserveStatus(job, after with { Dialog = job.Research!.Source });
        var state = WithPotionMessage(after);
        state = gap switch
        {
            "popup-open" => state with { Dialog = job.Research!.Source },
            "parchment-left" => state with { Inventory = state.Inventory.Add(1, new(1, "Parchment Scroll", 1)) },
            "no-song" => state with { Inventory = ImmutableDictionary<byte, Item>.Empty },
            "unknown-bag" => state with { InventoryKnown = false },
            "gold" => state with { Gold = state.Gold - 500 },
            "generation" => state with { Generation = state.Generation + 1 },
            "actor" => state with { Name = "Someoneelse" },
            _ => state
        };

        var settled = WizardyResearchEvidence.ObserveStatus(observed, state);

        Assert.NotNull(settled.Pending);
        Assert.Empty(settled.Research!.Attempts);
    }

    [Fact]
    public void Completing_an_attempt_clears_its_retained_status_and_preserves_the_health_obligation()
    {
        var (job, after) = Captured("sorcery");
        var json = JsonSerializer.SerializeToElement(job.Research!);
        var properties = json.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        properties["HealthCheckRequired"] = JsonSerializer.SerializeToElement(true);
        job = job with { Research = JsonSerializer.Deserialize<WizardyResearchProgress>(JsonSerializer.Serialize(properties))! };
        var observed = WizardyResearchEvidence.ObserveStatus(job, after with { Dialog = job.Research!.Source });

        var settled = WizardyResearchEvidence.ObserveStatus(observed, WithPotionMessage(after));
        var progress = JsonSerializer.SerializeToElement(settled.Research);

        Assert.Null(settled.Pending);
        Assert.Equal(0, settled.Research!.Stage);
        Assert.True(progress.TryGetProperty("ResultStatus", out var retained));
        Assert.Equal(JsonValueKind.Null, retained.ValueKind);
        Assert.True(progress.TryGetProperty("HealthCheckRequired", out var obligation));
        Assert.True(obligation.GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_healing_receipt_clears_only_the_dose_and_keeps_a_current_health_check(bool wrongGeneration)
    {
        var job = new QuestJob { Character = "ResearchActor", Quest = WizardyResearchEvidence.Id,
            Research = new() { Healing = new(1, 10, 30, 30, 2), HealthCheckRequired = true } };
        var after = new CharacterSnapshot { Name = job.Character, Connected = true, Generation = wrongGeneration ? 2 : 1,
            Revision = 12, InventoryKnown = true, Inventory = ImmutableDictionary<byte, Item>.Empty.Add(2, new(2, "Exkuranum", 29)),
            Hp = 3950, MaxHp = 3950 };

        var observed = WizardyResearchEvidence.ObserveStatus(job, after);

        Assert.Equal(wrongGeneration, observed.Research!.Healing is not null);
        Assert.True(observed.Research.HealthCheckRequired);
        Assert.Empty(observed.Research.Attempts);
    }

    [Fact]
    public void Archived_terminal_result_clears_an_already_submitted_result_Next()
    {
        var (job, after) = Captured("sorcery");
        job = job with { Research = job.Research! with { SubmittedNext = job.Research.Source } };

        var observed = WizardyResearchEvidence.ObserveStatus(job, after);

        Assert.Null(observed.Pending);
        Assert.Null(observed.Research!.SubmittedNext);
        Assert.Single(observed.Research.Attempts);
    }

    static CharacterSnapshot WithPotionMessage(CharacterSnapshot after) => after with
    {
        Revision = after.Revision + 2,
        ActiveStatus = new(after.Generation, after.Revision + 1, 3, "Synthetic Exkuranum healing reply", after.ActiveStatus!.ObservedAt.AddSeconds(1))
    };

    static (QuestJob Job, CharacterSnapshot After) Captured(string result)
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../content/evidence/datester-7.41/wizardry-research-" + result + "-result.json"));
        using var proof = JsonDocument.Parse(File.ReadAllText(path));
        var root = proof.RootElement;
        var source = root.GetProperty("Source").Deserialize<Dialog>()!;
        var after = root.GetProperty("After").Deserialize<CharacterSnapshot>()!;
        var pending = new Transaction("research-submit", source.Generation, source.Revision - 1,
            root.GetProperty("Gold").GetUInt32(), 0, 0, root.GetProperty("SubmittedAt").GetDateTimeOffset())
        {
            EntityId = source.EntityId,
            EntityType = source.EntityType,
            InventoryBaselineKnown = true,
            InventoryCounts = root.GetProperty("SongsBefore").Deserialize<Dictionary<string, int>>()!
        };
        pending.InventoryCounts[WizardyResearchEvidence.Parchment] = root.GetProperty("ParchmentBefore").GetInt32();
        return (new() { Character = after.Name!, Quest = WizardyResearchEvidence.Id,
            Research = new() { Stage = 4, Source = source }, Pending = pending }, after);
    }
}
