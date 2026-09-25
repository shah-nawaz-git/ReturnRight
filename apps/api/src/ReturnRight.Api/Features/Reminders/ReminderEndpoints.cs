namespace ReturnRight.Api.Features.Reminders;

public static class DevReminderEndpoints
{
    /// <summary>Dev/test hooks — mapped on /api/dev (test endpoints only).</summary>
    public static RouteGroupBuilder MapDevReminderEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/reminders/process", ProcessDue);
        return group;
    }

    private static async Task<IResult> ProcessDue(
        ReminderProcessor processor, CancellationToken ct)
    {
        var result = await processor.ProcessDueAsync(ct);
        return Results.Ok(result);
    }
}
