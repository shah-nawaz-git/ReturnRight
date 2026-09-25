using ReturnRight.Api.Storage;

namespace ReturnRight.UnitTests;

public class UploadValidatorTests
{
    private const long MaxBytes = 10 * 1024 * 1024;

    private static readonly byte[] PdfHead = "%PDF-1.4\n"u8.ToArray();
    private static readonly byte[] JpegHead = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] ExeHead = "MZ\x90\x00\x03\x00\x00\x00"u8.ToArray();

    [Fact]
    public void Valid_pdf_accepted()
    {
        var result = UploadValidator.Validate(PdfHead, "application/pdf", "receipt.pdf", 1000, MaxBytes);
        Assert.True(result.IsValid);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(".pdf", result.Extension);
        Assert.Equal("receipt.pdf", result.SafeFileName);
    }

    [Fact]
    public void Valid_jpeg_accepted()
    {
        var result = UploadValidator.Validate(JpegHead, "image/jpeg", "photo.JPEG", 1000, MaxBytes);
        Assert.True(result.IsValid);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(".jpg", result.Extension);
    }

    [Fact]
    public void Valid_png_accepted()
    {
        var result = UploadValidator.Validate(PngHead, "image/png", "screenshot.png", 1000, MaxBytes);
        Assert.True(result.IsValid);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public void Executable_named_pdf_rejected()
    {
        var result = UploadValidator.Validate(ExeHead, "application/pdf", "virus.pdf", 1000, MaxBytes);
        Assert.False(result.IsValid);
        Assert.Equal(UploadValidator.AcceptOnlyMessage, result.Error);
    }

    [Fact]
    public void Png_bytes_named_pdf_rejected()
    {
        var result = UploadValidator.Validate(PngHead, "application/pdf", "fake.pdf", 1000, MaxBytes);
        Assert.False(result.IsValid);
        Assert.Equal(UploadValidator.MismatchMessage, result.Error);
    }

    [Fact]
    public void Declared_html_rejected()
    {
        var result = UploadValidator.Validate(PdfHead, "text/html", "receipt.pdf", 1000, MaxBytes);
        Assert.False(result.IsValid);
        Assert.Equal(UploadValidator.AcceptOnlyMessage, result.Error);
    }

    [Fact]
    public void Octet_stream_declared_type_allowed()
    {
        var result = UploadValidator.Validate(PdfHead, "application/octet-stream", "receipt.pdf", 1000, MaxBytes);
        Assert.True(result.IsValid);
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public void Oversize_rejected()
    {
        var result = UploadValidator.Validate(PdfHead, "application/pdf", "big.pdf", MaxBytes + 1, MaxBytes);
        Assert.False(result.IsValid);
        Assert.Equal("This file is larger than 10 MB.", result.Error);
    }

    [Fact]
    public void Zero_length_rejected()
    {
        var result = UploadValidator.Validate(PdfHead, "application/pdf", "empty.pdf", 0, MaxBytes);
        Assert.False(result.IsValid);
        Assert.Equal(UploadValidator.EmptyMessage, result.Error);
    }

    [Fact]
    public void Traversal_filename_is_sanitized()
    {
        var result = UploadValidator.Validate(PdfHead, "application/pdf", "../../evil.pdf", 1000, MaxBytes);
        Assert.True(result.IsValid);
        Assert.Equal("evil.pdf", result.SafeFileName);
    }

    [Fact]
    public void Windows_traversal_filename_is_sanitized()
    {
        var result = UploadValidator.Validate(PngHead, "image/png", @"..\..\x.png", 1000, MaxBytes);
        Assert.True(result.IsValid);
        Assert.Equal("x.png", result.SafeFileName);
    }

    [Fact]
    public void Control_characters_removed_from_filename()
    {
        var safe = UploadValidator.SafeFileName("my\u0007file\t name.pdf", ".pdf");
        Assert.Equal("myfile name.pdf", safe);
    }

    [Fact]
    public void Very_long_filename_truncated_with_extension_kept()
    {
        var name = new string('a', 300) + ".pdf";
        var safe = UploadValidator.SafeFileName(name, ".pdf");
        Assert.True(safe.Length <= 200);
        Assert.EndsWith(".pdf", safe);
    }

    [Fact]
    public void Empty_filename_uses_fallback()
    {
        Assert.Equal("document.pdf", UploadValidator.SafeFileName(null, ".pdf"));
        Assert.Equal("document.png", UploadValidator.SafeFileName("", ".png"));
    }
}
