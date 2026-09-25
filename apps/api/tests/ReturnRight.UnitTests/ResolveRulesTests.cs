using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases;

namespace ReturnRight.UnitTests;

public class ResolveRulesTests
{
    [Theory]
    [InlineData(FinalOutcomeType.FullRefundReceived)]
    [InlineData(FinalOutcomeType.PartialRefundReceived)]
    [InlineData(FinalOutcomeType.ReplacementReceived)]
    [InlineData(FinalOutcomeType.RepairCompleted)]
    public void Positive_outcomes_resolve(FinalOutcomeType outcome)
    {
        Assert.True(ResolveRules.IsPositiveOutcome(outcome));
        Assert.Equal(CaseStatus.Resolved, ResolveRules.StatusFor(outcome));
    }

    [Theory]
    [InlineData(FinalOutcomeType.SellerRejected)]
    [InlineData(FinalOutcomeType.UserAbandoned)]
    [InlineData(FinalOutcomeType.Other)]
    public void Non_positive_outcomes_close(FinalOutcomeType outcome)
    {
        Assert.False(ResolveRules.IsPositiveOutcome(outcome));
        Assert.Equal(CaseStatus.Closed, ResolveRules.StatusFor(outcome));
    }
}
