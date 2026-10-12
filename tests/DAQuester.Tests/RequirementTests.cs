using Xunit;
using DAQuester.Core;

namespace DAQuester.Tests;

public class RequirementTests
{
    [Theory]
    [InlineData(1, EligibilityKind.Eligible)]
    [InlineData(12, EligibilityKind.Blocked)]
    [InlineData(null, EligibilityKind.Unknown)]
    public void Generic_recipe_level_requirement(int? level, EligibilityKind expected)
    {
        Assert.Equal(expected, RequirementEvaluator.Evaluate(FoundationTests.Ready(level), new()
        {
            Kind = "level",
            Minimum = 1,
            Maximum = 11
        }).Kind);
    }

    [Fact]
    public void Unsupported_requirement_does_not_assume_eligibility()
    {
        Assert.Equal(EligibilityKind.Unknown, RequirementEvaluator.Evaluate(FoundationTests.Ready(), new()
        {
            Kind = "party-credit"
        }).Kind);
    }

    [Fact]
    public void Unknown_quantity_remains_unknown()
    {
        Assert.Equal(EligibilityKind.Unknown, RequirementEvaluator.Evaluate(FoundationTests.Ready() with
        {
            InventoryKnown = false
        }, new()
        {
            Kind = "inventory",
            Value = "Branch",
            Minimum = 6
        }).Kind);
    }
}
