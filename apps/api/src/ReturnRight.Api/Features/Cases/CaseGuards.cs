using ReturnRight.Api.Domain;
using ReturnRight.Api.Security;

namespace ReturnRight.Api.Features.Cases;

public static class CaseGuards
{
    /// <summary>Mutating case-graph operations are blocked once a case is finished.</summary>
    public static IResult? RejectFinished(IssueCase issueCase) =>
        issueCase.Status is CaseStatus.Resolved or CaseStatus.Closed
            ? ProblemResults.Create(
                StatusCodes.Status409Conflict,
                "This case is resolved. Reopen it to add updates.")
            : null;
}
