namespace ReturnRight.Api.Storage;

/// <summary>Buffers an uploaded file once and runs UploadValidator on it.</summary>
public static class UploadHelper
{
    public static async Task<(UploadValidation Validation, MemoryStream Buffer)> ValidateAsync(
        IFormFile file, IConfiguration configuration, CancellationToken ct)
    {
        var maxBytes = configuration.GetValue("Storage:MaxUploadBytes", 10L * 1024 * 1024);

        var buffer = new MemoryStream();
        await using (var source = file.OpenReadStream())
        {
            var head = new byte[16];
            var headLength = await source.ReadAsync(head, ct);
            buffer.Write(head, 0, headLength);
            await source.CopyToAsync(buffer, ct);
        }
        buffer.Position = 0;

        var headSpan = buffer.GetBuffer().AsSpan(0, Math.Min(16, (int)buffer.Length));
        var validation = UploadValidator.Validate(
            headSpan, file.ContentType, file.FileName, buffer.Length, maxBytes);
        return (validation, buffer);
    }
}
