namespace ReturnRight.Api.Domain;

public class Document
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string StorageKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DocumentCategory Category { get; set; }
    public Guid? PurchaseId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AppUser User { get; set; } = null!;
    public Purchase? Purchase { get; set; }
}
