using ReturnRight.Api.Domain;

namespace ReturnRight.Api.Features.Cases;

/// <summary>Pure rules for finishing a case — unit-tested without a database.</summary>
public static class ResolveRules
{
    public static bool IsPositiveOutcome(FinalOutcomeType outcome) =>
        outcome is FinalOutcomeType.FullRefundReceived
            or FinalOutcomeType.PartialRefundReceived
            or FinalOutcomeType.ReplacementReceived
            or FinalOutcomeType.RepairCompleted;

    public static CaseStatus StatusFor(FinalOutcomeType outcome) =>
        IsPositiveOutcome(outcome) ? CaseStatus.Resolved : CaseStatus.Closed;
}
