using FluentValidation;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Documents;

namespace ReturnRight.Api.Features.Purchases;

public record PurchaseSummaryResponse(
    Guid Id,
    string MerchantName,
    string? OrderNumber,
    DateOnly? PurchaseDate,
    string Currency,
    decimal? TotalAmount,
    int ItemCount,
    string? FirstItemName,
    bool HasProof,
    int ActiveCaseCount,
    DateTimeOffset CreatedAt);

public record ProvenanceResponse(
    string Source,
    double? Confidence,
    bool ConfirmedByUser,
    Guid? SourceDocumentId);

public record PurchaseItemResponse(
    Guid Id,
    string ProductName,
    int Quantity,
    decimal? UnitPrice,
    DateOnly? ReturnDeadline,
    ProvenanceResponse ReturnDeadlineProvenance,
    DateOnly? CommercialWarrantyEnd,
    ProvenanceResponse CommercialWarrantyEndProvenance);

public record PurchaseCaseSummary(
    Guid Id,
    string ProblemType,
    string Status,
    DateTimeOffset CreatedAt);

public record PurchaseProvenanceResponse(
    ProvenanceResponse MerchantName,
    ProvenanceResponse OrderNumber,
    ProvenanceResponse PurchaseDate,
    ProvenanceResponse TotalAmount);

public record PurchaseDetailResponse(
    Guid Id,
    string MerchantName,
    string? OrderNumber,
    DateOnly? PurchaseDate,
    string Currency,
    decimal? TotalAmount,
    string? Notes,
    PurchaseProvenanceResponse Provenance,
    PurchaseItemResponse[] Items,
    DocumentResponse[] Documents,
    PurchaseCaseSummary[] Cases,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record PurchaseItemInput(
    Guid? Id,
    string? ProductName,
    int? Quantity,
    decimal? UnitPrice,
    DateOnly? ReturnDeadline,
    DateOnly? CommercialWarrantyEnd,
    FieldSource? ReturnDeadlineSource,
    FieldSource? CommercialWarrantyEndSource);

public record CreatePurchaseRequest(
    string? MerchantName,
    string? OrderNumber,
    DateOnly? PurchaseDate,
    string? Currency,
    decimal? TotalAmount,
    string? Notes,
    List<PurchaseItemInput>? Items);

public record UpdatePurchaseRequest(
    string? MerchantName,
    string? OrderNumber,
    DateOnly? PurchaseDate,
    string? Currency,
    decimal? TotalAmount,
    string? Notes,
    List<PurchaseItemInput>? Items);

public sealed class PurchaseItemInputValidator : AbstractValidator<PurchaseItemInput>
{
    public PurchaseItemInputValidator()
    {
        RuleFor(i => i.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(i => i.Quantity).NotNull().GreaterThanOrEqualTo(1);
        RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).When(i => i.UnitPrice is not null);
    }
}

public sealed class CreatePurchaseRequestValidator : AbstractValidator<CreatePurchaseRequest>
{
    public CreatePurchaseRequestValidator()
    {
        RuleFor(r => r.MerchantName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.OrderNumber).MaximumLength(100);
        RuleFor(r => r.Currency).NotEmpty().Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter code like EUR or USD.");
        RuleFor(r => r.TotalAmount).GreaterThanOrEqualTo(0).When(r => r.TotalAmount is not null);
        RuleFor(r => r.Notes).MaximumLength(2000);
        RuleFor(r => r.Items).NotNull();
        RuleForEach(r => r.Items).SetValidator(new PurchaseItemInputValidator());
    }
}

public sealed class UpdatePurchaseRequestValidator : AbstractValidator<UpdatePurchaseRequest>
{
    public UpdatePurchaseRequestValidator()
    {
        RuleFor(r => r.MerchantName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.OrderNumber).MaximumLength(100);
        RuleFor(r => r.Currency).NotEmpty().Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter code like EUR or USD.");
        RuleFor(r => r.TotalAmount).GreaterThanOrEqualTo(0).When(r => r.TotalAmount is not null);
        RuleFor(r => r.Notes).MaximumLength(2000);
        RuleFor(r => r.Items).NotNull();
        RuleForEach(r => r.Items).SetValidator(new PurchaseItemInputValidator());
    }
}
