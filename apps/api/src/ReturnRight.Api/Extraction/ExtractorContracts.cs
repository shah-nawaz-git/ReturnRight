namespace ReturnRight.Api.Extraction;

public record FieldCandidate(
    string Field,
    string Value,
    double Confidence,
    string? Evidence);

public record ItemCandidate(
    string Name,
    int? Quantity,
    decimal? UnitPrice,
    double Confidence);

public record ExtractionOutcome(
    string Status,
    bool UsedOcr,
    int PagesProcessed,
    List<FieldCandidate> Candidates,
    List<ItemCandidate> Items,
    string? Message);

public interface IExtractorClient
{
    Task<ExtractionOutcome> ExtractAsync(
        Stream content, string fileName, string contentType, CancellationToken ct);
}
