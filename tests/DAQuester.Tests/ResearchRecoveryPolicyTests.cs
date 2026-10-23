using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class ResearchRecoveryPolicyTests
{
    static QuestDefinition Recipe => new(WizardyResearchEvidence.Id, 1, "Wizardy research", "Synthetic recovery policy", [], [], 12);

    [Theory]
    [InlineData("healing")]
    [InlineData("health")]
    [InlineData("purchase")]
    public void Saved_recovery_remains_runnable_before_a_research_rest_deadline(string recovery)
    {
        var job = new QuestJob { Quest = WizardyResearchEvidence.Id, NextRun = DateTimeOffset.UtcNow.AddHours(12),
            Research = SavedProgress(recovery) };

        Assert.Null(QuestRunPolicy.NextAttempt(Recipe, job));
    }

    [Theory]
    [InlineData("healing", true)]
    [InlineData("health", true)]
    [InlineData("purchase", true)]
    [InlineData("healing", false)]
    [InlineData("health", false)]
    [InlineData("purchase", false)]
    public void Saved_recovery_prevents_a_scheduled_new_attempt_from_resetting_its_checkpoint(string recovery, bool resume)
    {
        var due = DateTimeOffset.UtcNow;
        var job = new QuestJob { Quest = WizardyResearchEvidence.Id, Step = "complete", CompletedAt = due.AddDays(-1),
            Research = SavedProgress(recovery) };

        Assert.False(QuestRunPolicy.StartNewScheduledAttempt(Recipe, job, new() { Resume = resume, Due = due }));
    }

    static WizardyResearchProgress SavedProgress(string recovery) => JsonSerializer.Deserialize<WizardyResearchProgress>(recovery switch
    {
        "healing" => """{"Healing":{"Generation":1,"Revision":2,"BeforeCount":30,"BeforeHp":30,"Slot":2}}""",
        "health" => """{"HealthCheckRequired":true}""",
        _ => """{"PotionPurchase":{"Generation":1,"Revision":2,"BeforeCount":0,"BeforeGold":5000,"Quantity":30,"QuotedGold":3000,"Stock":{"Generation":1,"Revision":2,"EntityId":55,"EntityType":1,"Name":"Potion seller","Text":"Synthetic stock","IsPursuit":false,"Pursuit":64,"Step":0,"Type":4,"Choices":[]}}}"""
    })!;
}
