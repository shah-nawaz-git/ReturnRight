using ReturnRight.Api.Extraction;

namespace ReturnRight.IntegrationTests;

/// <summary>
/// Deterministic extractor double. Mirrors the real extractor's fixture data:
/// SoundMarket / 2026-09-10 / SM-48213 / EUR 389.99 / Auralis X4 Headphones.
/// </summary>
public sealed class FakeExtractorClient : IExtractorClient
{
    public bool ThrowOnExtract { get; set; }
    public int CallCount { get; private set; }

    public Task<ExtractionOutcome> ExtractAsync(
        Stream content, string fileName, string contentType, CancellationToken ct)
    {
        CallCount++;
        if (ThrowOnExtract)
        {
            throw new HttpRequestException("Fake extractor is unavailable.");
        }

        return Task.FromResult(new ExtractionOutcome(
            "succeeded",
            UsedOcr: false,
            PagesProcessed: 1,
            Candidates:
            [
                new FieldCandidate("merchantName", "SoundMarket", 0.98, "SoundMarket"),
                new FieldCandidate("purchaseDate", "2026-09-10", 0.91, "10 Sep 2026"),
                new FieldCandidate("orderNumber", "SM-48213", 0.93, "Order SM-48213"),
                new FieldCandidate("currency", "EUR", 0.9, "€"),
                new FieldCandidate("totalAmount", "389.99", 0.95, "Total €389.99"),
            ],
            Items:
            [
                new ItemCandidate("Auralis X4 Headphones", 1, 389.99m, 0.9),
            ],
            Message: null));
    }
}
