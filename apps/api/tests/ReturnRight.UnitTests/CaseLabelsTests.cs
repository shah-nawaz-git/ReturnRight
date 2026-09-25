using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases;

namespace ReturnRight.UnitTests;

public class CaseLabelsTests
{
    [Fact]
    public void Every_problem_type_has_a_label() =>
        Assert.All(
            Enum.GetValues<ProblemType>(),
            value => Assert.False(string.IsNullOrWhiteSpace(CaseLabels.Label(value))));

    [Fact]
    public void Every_requested_outcome_has_a_label() =>
        Assert.All(
            Enum.GetValues<RequestedOutcomeType>(),
            value => Assert.False(string.IsNullOrWhiteSpace(CaseLabels.Label(value))));

    [Fact]
    public void Every_status_has_a_label() =>
        Assert.All(
            Enum.GetValues<CaseStatus>(),
            value => Assert.False(string.IsNullOrWhiteSpace(CaseLabels.Label(value))));

    [Fact]
    public void Every_final_outcome_has_a_label() =>
        Assert.All(
            Enum.GetValues<FinalOutcomeType>(),
            value => Assert.False(string.IsNullOrWhiteSpace(CaseLabels.Label(value))));

    [Fact]
    public void Every_evidence_type_has_a_label() =>
        Assert.All(
            Enum.GetValues<EvidenceType>(),
            value => Assert.False(string.IsNullOrWhiteSpace(CaseLabels.Label(value))));

    [Fact]
    public void Every_interaction_type_has_a_label() =>
        Assert.All(
            Enum.GetValues<InteractionType>(),
            value => Assert.False(string.IsNullOrWhiteSpace(CaseLabels.Label(value))));

    [Fact]
    public void Labels_are_consumer_friendly()
    {
        Assert.Equal("Damaged item", CaseLabels.Label(ProblemType.DamagedItem));
        Assert.Equal("Full refund", CaseLabels.Label(RequestedOutcomeType.FullRefund));
        Assert.Equal("Waiting for seller", CaseLabels.Label(CaseStatus.WaitingForSeller));
        Assert.Equal("Full refund received", CaseLabels.Label(FinalOutcomeType.FullRefundReceived));
    }
}
