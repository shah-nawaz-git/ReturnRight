using ReturnRight.Api.Domain;

namespace ReturnRight.Api.Features.Documents;

public record DocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Category,
    DateTimeOffset CreatedAt,
    bool IsImage);

public static class DocumentMappings
{
    public static DocumentResponse ToResponse(Document document) =>
        new(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.Category.ToString(),
            document.CreatedAt,
            document.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));

    public static string ContentDispositionHeader(string disposition, string fileName)
    {
        // ASCII fallback for filename= plus RFC 5987 filename*= for anything else.
        var ascii = new string(fileName.Select(c => c is >= ' ' and <= '~' ? c : '_').ToArray());
        ascii = ascii.Replace('"', '\'').Replace('\\', '_');
        var encoded = Uri.EscapeDataString(fileName);
        return $"{disposition}; filename=\"{ascii}\"; filename*=UTF-8''{encoded}";
    }
}
