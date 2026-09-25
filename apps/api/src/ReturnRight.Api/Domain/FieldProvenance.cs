namespace ReturnRight.Api.Domain;

public class FieldProvenance
{
    public FieldSource Source { get; set; }
    public double? Confidence { get; set; }
    public bool ConfirmedByUser { get; set; }
    public Guid? SourceDocumentId { get; set; }

    public static FieldProvenance User() => new() { Source = FieldSource.UserEntered, ConfirmedByUser = true };

    public static FieldProvenance Extracted(double? confidence, Guid? documentId, bool confirmed) =>
        new()
        {
            Source = FieldSource.ExtractedFromDocument,
            Confidence = confidence,
            SourceDocumentId = documentId,
            ConfirmedByUser = confirmed,
        };
}
