using ReturnRight.Api.Domain;

namespace ReturnRight.Api.Features.Cases;

/// <summary>Case title rule, reused everywhere including PDFs: first affected item
/// (+ "and N more"), else purchase merchant, else problem-type label.</summary>
public static class CaseTitle
{
    public static string Build(IssueCase issueCase)
    {
        var itemNames = issueCase.AffectedItems
            .Where(a => a.PurchaseItem is not null)
            .OrderBy(a => a.PurchaseItem!.SortOrder)
            .Select(a => a.PurchaseItem!.ProductName)
            .ToList();

        return itemNames.Count switch
        {
            1 => itemNames[0],
            > 1 => $"{itemNames[0]} and {itemNames.Count - 1} more",
            _ => issueCase.Purchase?.MerchantName ?? CaseLabels.Label(issueCase.ProblemType),
        };
    }
}
