namespace ReturnRight.Api.Domain;

public class Purchase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string MerchantName { get; set; }
    public string? OrderNumber { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public required string Currency { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Notes { get; set; }
    public required FieldProvenance MerchantNameProvenance { get; set; }
    public required FieldProvenance OrderNumberProvenance { get; set; }
    public required FieldProvenance PurchaseDateProvenance { get; set; }
    public required FieldProvenance TotalAmountProvenance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public AppUser User { get; set; } = null!;
    public List<PurchaseItem> Items { get; set; } = [];
    public List<Document> Documents { get; set; } = [];
    public List<IssueCase> Cases { get; set; } = [];
}

public class PurchaseItem
{
    public Guid Id { get; set; }
    public Guid PurchaseId { get; set; }
    public required string ProductName { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public DateOnly? ReturnDeadline { get; set; }
    public required FieldProvenance ReturnDeadlineProvenance { get; set; }
    public DateOnly? CommercialWarrantyEnd { get; set; }
    public required FieldProvenance CommercialWarrantyEndProvenance { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Purchase Purchase { get; set; } = null!;
}
