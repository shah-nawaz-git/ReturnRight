using System.Text.Json;
using ReturnRight.Api.Domain;

namespace ReturnRight.Api.Features.Cases;

public class CaseTimelineWriter(TimeProvider time)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add(
        IssueCase issueCase, TimelineEventType type, string summary, object? metadata = null)
    {
        var now = time.GetUtcNow();
        issueCase.DismissedNextActionKey = null; // any activity re-evaluates the suggestion
        issueCase.TimelineEvents.Add(new CaseTimelineEvent
        {
            CaseId = issueCase.Id,
            EventType = type,
            OccurredAt = now,
            Summary = summary,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOptions),
            CreatedAt = now,
        });
    }
}
