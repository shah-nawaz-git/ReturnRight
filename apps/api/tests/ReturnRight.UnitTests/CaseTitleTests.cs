using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases;

namespace ReturnRight.UnitTests;

public class CaseTitleTests
{
    private static IssueCase NewCase() =>
        new()
        {
            ProblemType = ProblemType.DamagedItem,
            Description = "broken",
            Status = CaseStatus.Open,
            RequestedOutcomeType = RequestedOutcomeType.FullRefund,
        };

    private static CaseAffectedItem Affected(string name, int sort = 0) =>
        new()
        {
            PurchaseItem = new PurchaseItem
            {
                ProductName = name,
                Quantity = 1,
                SortOrder = sort,
                ReturnDeadlineProvenance = FieldProvenance.User(),
                CommercialWarrantyEndProvenance = FieldProvenance.User(),
            },
        };

    [Fact]
    public void Single_affected_item_names_the_item()
    {
        var issueCase = NewCase();
        issueCase.AffectedItems.Add(Affected("Auralis X4 Headphones"));
        Assert.Equal("Auralis X4 Headphones", CaseTitle.Build(issueCase));
    }

    [Fact]
    public void Multiple_affected_items_add_count()
    {
        var issueCase = NewCase();
        issueCase.AffectedItems.Add(Affected("Headphones", 0));
        issueCase.AffectedItems.Add(Affected("Case", 1));
        issueCase.AffectedItems.Add(Affected("Cable", 2));
        Assert.Equal("Headphones and 2 more", CaseTitle.Build(issueCase));
    }

    [Fact]
    public void No_items_falls_back_to_merchant()
    {
        var issueCase = NewCase();
        issueCase.Purchase = new Purchase
        {
            MerchantName = "SoundMarket",
            Currency = "EUR",
            MerchantNameProvenance = FieldProvenance.User(),
            OrderNumberProvenance = FieldProvenance.User(),
            PurchaseDateProvenance = FieldProvenance.User(),
            TotalAmountProvenance = FieldProvenance.User(),
        };
        Assert.Equal("SoundMarket", CaseTitle.Build(issueCase));
    }

    [Fact]
    public void No_items_or_purchase_falls_back_to_problem_label()
    {
        Assert.Equal("Damaged item", CaseTitle.Build(NewCase()));
    }
}
