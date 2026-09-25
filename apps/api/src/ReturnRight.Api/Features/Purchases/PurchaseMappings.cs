using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Documents;

namespace ReturnRight.Api.Features.Purchases;

public static class PurchaseMappings
{
    public static ProvenanceResponse ToResponse(FieldProvenance provenance) =>
        new(
            provenance.Source.ToString(),
            provenance.Confidence,
            provenance.ConfirmedByUser,
            provenance.SourceDocumentId);

    public static PurchaseItemResponse ToResponse(PurchaseItem item) =>
        new(
            item.Id,
            item.ProductName,
            item.Quantity,
            item.UnitPrice,
            item.ReturnDeadline,
            ToResponse(item.ReturnDeadlineProvenance),
            item.CommercialWarrantyEnd,
            ToResponse(item.CommercialWarrantyEndProvenance));

    public static PurchaseDetailResponse ToDetailResponse(Purchase purchase) =>
        new(
            purchase.Id,
            purchase.MerchantName,
            purchase.OrderNumber,
            purchase.PurchaseDate,
            purchase.Currency,
            purchase.TotalAmount,
            purchase.Notes,
            new PurchaseProvenanceResponse(
                ToResponse(purchase.MerchantNameProvenance),
                ToResponse(purchase.OrderNumberProvenance),
                ToResponse(purchase.PurchaseDateProvenance),
                ToResponse(purchase.TotalAmountProvenance)),
            purchase.Items
                .OrderBy(i => i.SortOrder)
                .Select(ToResponse)
                .ToArray(),
            purchase.Documents
                .OrderBy(d => d.CreatedAt)
                .Select(DocumentMappings.ToResponse)
                .ToArray(),
            purchase.Cases
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new PurchaseCaseSummary(
                    c.Id, c.ProblemType.ToString(), c.Status.ToString(), c.CreatedAt))
                .ToArray(),
            purchase.CreatedAt,
            purchase.UpdatedAt);

    public static PurchaseSummaryResponse ToSummaryResponse(Purchase purchase)
    {
        var firstItem = purchase.Items.OrderBy(i => i.SortOrder).FirstOrDefault();
        return new PurchaseSummaryResponse(
            purchase.Id,
            purchase.MerchantName,
            purchase.OrderNumber,
            purchase.PurchaseDate,
            purchase.Currency,
            purchase.TotalAmount,
            purchase.Items.Count,
            firstItem?.ProductName,
            purchase.Documents.Any(d => d.Category == DocumentCategory.PurchaseDocument),
            purchase.Cases.Count(c => c.Status is not CaseStatus.Resolved and not CaseStatus.Closed),
            purchase.CreatedAt);
    }

    /// <summary>Builds item-field provenance from an optional declared source.</summary>
    public static FieldProvenance ItemFieldProvenance(FieldSource? source, Guid? sourceDocumentId) =>
        source == Domain.FieldSource.ExtractedFromDocument
            ? FieldProvenance.Extracted(null, sourceDocumentId, confirmed: true)
            : FieldProvenance.User();
}
