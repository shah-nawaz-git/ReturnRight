using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReturnRight.Api.Extraction;

public class HttpExtractorClient(HttpClient httpClient, ILogger<HttpExtractorClient> logger)
    : IExtractorClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<ExtractionOutcome> ExtractAsync(
        Stream content, string fileName, string contentType, CancellationToken ct)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            using var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(streamContent, "file", fileName);

            using var response = await httpClient.PostAsync("/extract", form, ct);
            response.EnsureSuccessStatusCode();

            var dto = await response.Content
                .ReadFromJsonAsync<ExtractorResultDto>(JsonOptions, ct);
            if (dto is null)
            {
                return Failed();
            }

            return new ExtractionOutcome(
                dto.Status ?? "failed",
                dto.UsedOcr,
                dto.PagesProcessed,
                dto.Candidates?.Select(c => new FieldCandidate(
                        c.Field ?? string.Empty, c.Value ?? string.Empty, c.Confidence, c.Evidence))
                    .ToList() ?? [],
                dto.Items?.Select(i => new ItemCandidate(
                        i.Name ?? string.Empty, i.Quantity, i.UnitPrice, i.Confidence))
                    .ToList() ?? [],
                dto.Message);
        }
        catch (Exception ex)
        {
            // Never log file contents — only the failure type.
            logger.LogWarning("Extractor call failed ({Type})", ex.GetType().Name);
            return Failed();
        }
    }

    private static ExtractionOutcome Failed() =>
        new("failed", false, 0, [], [], "unavailable");

    private sealed class ExtractorResultDto
    {
        public string? Status { get; set; }
        public bool UsedOcr { get; set; }
        public int PagesProcessed { get; set; }
        public List<CandidateDto>? Candidates { get; set; }
        public List<ItemDto>? Items { get; set; }
        public string? Message { get; set; }
    }

    private sealed class CandidateDto
    {
        public string? Field { get; set; }
        public string? Value { get; set; }
        public double Confidence { get; set; }
        public string? Evidence { get; set; }
    }

    private sealed class ItemDto
    {
        public string? Name { get; set; }
        public int? Quantity { get; set; }
        // The extractor emits unit_price as a string ("389.99").
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? UnitPrice { get; set; }
        public double Confidence { get; set; }
    }
}
