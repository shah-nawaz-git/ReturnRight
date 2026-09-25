using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ReturnRight.Api.Features.Cases;
using ReturnRight.Api.Storage;

namespace ReturnRight.Api.Features.Exports;

public record GeneratedCaseFile(byte[] Content, string FileName);

public class CaseFileGenerator(
    CaseDetailBuilder builder,
    IFileStorage storage,
    TimeProvider time,
    ILogger<CaseFileGenerator> logger)
{
    public async Task<GeneratedCaseFile?> GenerateAsync(
        Guid caseId, Guid userId, CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(caseId, userId, ct);
        if (issueCase is null)
        {
            return null;
        }

        var images = new List<CaseFileImage>();
        foreach (var evidence in issueCase.Evidence
                     .Where(e => e.Document.ContentType.StartsWith(
                         "image/", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(e => e.CreatedAt)
                     .Take(12))
        {
            try
            {
                await using var stream =
                    await storage.OpenReadAsync(evidence.Document.StorageKey, ct);
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                images.Add(new CaseFileImage(
                    evidence, Image.FromBinaryData(buffer.ToArray())));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    "Skipping unreadable image {DocumentId} ({Type})",
                    evidence.DocumentId, ex.GetType().Name);
            }
        }

        var title = CaseTitle.Build(issueCase);
        var document = new CaseFileDocument(issueCase, title, time.GetUtcNow(), images);
        var stream2 = new MemoryStream();
        document.GeneratePdf(stream2);

        var fileName = $"ReturnRight-Case-File-{caseId.ToString("N")[..8]}.pdf";
        return new GeneratedCaseFile(stream2.ToArray(), fileName);
    }
}
