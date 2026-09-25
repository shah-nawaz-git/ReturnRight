using System.Text;

namespace ReturnRight.Api.Storage;

public sealed record UploadValidation(
    bool IsValid,
    string? Error,
    string ContentType,
    string Extension,
    string SafeFileName);

/// <summary>
/// Validates uploads by content signature (not trust in names or declared types).
/// Pure and side-effect free so it can be unit tested exhaustively.
/// </summary>
public static class UploadValidator
{
    private static readonly (byte[] Magic, string ContentType, string CanonicalExt)[] Signatures =
    [
        ("%PDF-"u8.ToArray(), "application/pdf", ".pdf"),
        (new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg", ".jpg"),
        (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png", ".png"),
    ];

    private static readonly HashSet<string> AllowedDeclaredTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "application/octet-stream",
    };

    public const string AcceptOnlyMessage = "We can only accept PDF, JPG and PNG files.";
    public const string MismatchMessage = "The file type doesn't match its contents.";
    public const string EmptyMessage = "This file appears to be empty.";

    public static UploadValidation Validate(
        ReadOnlySpan<byte> head,
        string? declaredContentType,
        string? originalFileName,
        long length,
        long maxBytes)
    {
        var maxMb = maxBytes / (1024 * 1024);

        if (length <= 0)
        {
            return Invalid(EmptyMessage);
        }
        if (length > maxBytes)
        {
            return Invalid($"This file is larger than {maxMb} MB.");
        }

        if (!string.IsNullOrEmpty(declaredContentType)
            && !AllowedDeclaredTypes.Contains(declaredContentType.Trim()))
        {
            return Invalid(AcceptOnlyMessage);
        }

        (string contentType, string canonicalExt)? match = null;
        foreach (var (magic, type, ext) in Signatures)
        {
            if (head.Length >= magic.Length && head[..magic.Length].SequenceEqual(magic))
            {
                match = (type, ext);
                break;
            }
        }
        if (match is null)
        {
            return Invalid(AcceptOnlyMessage);
        }

        var (contentType, canonicalExtension) = match.Value;

        // A specific declared type that contradicts the real signature is rejected too.
        if (!string.IsNullOrEmpty(declaredContentType)
            && !declaredContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            && !declaredContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase))
        {
            return Invalid(MismatchMessage);
        }

        var extension = Path.GetExtension(
            (originalFileName ?? string.Empty).Replace('\\', '/')).ToLowerInvariant();
        var extensionOk = contentType switch
        {
            "application/pdf" => extension == ".pdf",
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            _ => false,
        };
        if (!extensionOk)
        {
            return Invalid(MismatchMessage);
        }

        return new UploadValidation(
            true,
            null,
            contentType,
            canonicalExtension,
            SafeFileName(originalFileName, canonicalExtension));
    }

    public static string SafeFileName(string? originalFileName, string canonicalExtension)
    {
        var name = Path.GetFileName((originalFileName ?? string.Empty).Replace('\\', '/'));

        var builder = new StringBuilder(name.Length);
        var lastWasWhitespace = false;
        foreach (var ch in name)
        {
            if (char.IsControl(ch) || ch is '"' or '<' or '>' or ':' or '*' or '?' or '|')
            {
                continue;
            }
            if (char.IsWhiteSpace(ch))
            {
                if (lastWasWhitespace)
                {
                    continue;
                }
                lastWasWhitespace = true;
                builder.Append(' ');
            }
            else
            {
                lastWasWhitespace = false;
                builder.Append(ch);
            }
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length > 200)
        {
            var ext = Path.GetExtension(cleaned);
            cleaned = cleaned[..(200 - ext.Length)] + ext;
        }
        if (cleaned.Length == 0)
        {
            cleaned = $"document{canonicalExtension}";
        }
        return cleaned;
    }

    private static UploadValidation Invalid(string error) =>
        new(false, error, string.Empty, string.Empty, string.Empty);
}
