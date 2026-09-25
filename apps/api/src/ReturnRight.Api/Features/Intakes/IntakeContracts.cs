using FluentValidation;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Extraction;
using ReturnRight.Api.Features.Purchases;

namespace ReturnRight.Api.Features.Intakes;

public record IntakeResponse(
    Guid Id,
    string Status,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset ExpiresAt,
    bool UsedOcr,
    List<FieldCandidate> Candidates,
    List<ItemCandidate> Items,
    string? Message);

public record ProvenanceInput(FieldSource Source, double? Confidence);

public record ConfirmProvenanceInput(
    ProvenanceInput? MerchantName,
    ProvenanceInput? OrderNumber,
    ProvenanceInput? PurchaseDate,
    ProvenanceInput? TotalAmount);

public record ConfirmIntakeRequest(
    string? MerchantName,
    string? OrderNumber,
    DateOnly? PurchaseDate,
    string? Currency,
    decimal? TotalAmount,
    string? Notes,
    List<PurchaseItemInput>? Items,
    ConfirmProvenanceInput? Provenance);

public sealed class ConfirmIntakeRequestValidator : AbstractValidator<ConfirmIntakeRequest>
{
    public ConfirmIntakeRequestValidator()
    {
        RuleFor(r => r.MerchantName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.OrderNumber).MaximumLength(100);
        RuleFor(r => r.Currency).NotEmpty().Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter code like EUR or USD.");
        RuleFor(r => r.TotalAmount).GreaterThanOrEqualTo(0).When(r => r.TotalAmount is not null);
        RuleFor(r => r.Notes).MaximumLength(2000);
        RuleFor(r => r.Items).NotNull().NotEmpty()
            .WithMessage("Add at least one item.");
        RuleForEach(r => r.Items).SetValidator(new PurchaseItemInputValidator());
        RuleFor(r => r.Provenance!.MerchantName!.Confidence).InclusiveBetween(0, 1)
            .When(r => r.Provenance?.MerchantName?.Confidence is not null);
        RuleFor(r => r.Provenance!.OrderNumber!.Confidence).InclusiveBetween(0, 1)
            .When(r => r.Provenance?.OrderNumber?.Confidence is not null);
        RuleFor(r => r.Provenance!.PurchaseDate!.Confidence).InclusiveBetween(0, 1)
            .When(r => r.Provenance?.PurchaseDate?.Confidence is not null);
        RuleFor(r => r.Provenance!.TotalAmount!.Confidence).InclusiveBetween(0, 1)
            .When(r => r.Provenance?.TotalAmount?.Confidence is not null);
    }
}
