using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Persistence;

namespace ReturnRight.Api.Features.Reminders;

public class ReminderScheduler(AppDbContext db, TimeProvider time)
{
    /// <summary>Creates one reminder per enabled channel for a follow-up.</summary>
    public Task ScheduleForFollowUpAsync(
        FollowUp followUp, AppUser user, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var channels = new List<ReminderChannel>();
        if (user.EmailRemindersEnabled)
        {
            channels.Add(ReminderChannel.Email);
        }
        if (user.InAppRemindersEnabled)
        {
            channels.Add(ReminderChannel.InApp);
        }

        foreach (var channel in channels)
        {
            db.Reminders.Add(new Reminder
            {
                UserId = user.Id,
                CaseId = followUp.CaseId,
                FollowUpId = followUp.Id,
                Channel = channel,
                ScheduledFor = followUp.DueAt,
                NextAttemptAt = followUp.DueAt,
                Status = ReminderStatus.Scheduled,
                CreatedAt = now,
            });
        }
        return Task.CompletedTask;
    }

    public async Task CancelForFollowUpAsync(Guid followUpId, CancellationToken ct)
    {
        var scheduled = await db.Reminders
            .Where(r => r.FollowUpId == followUpId && r.Status == ReminderStatus.Scheduled)
            .ToListAsync(ct);
        foreach (var reminder in scheduled)
        {
            reminder.Status = ReminderStatus.Cancelled;
        }
    }

    public async Task CancelForCaseAsync(Guid caseId, CancellationToken ct)
    {
        var scheduled = await db.Reminders
            .Where(r => r.CaseId == caseId && r.Status == ReminderStatus.Scheduled)
            .ToListAsync(ct);
        foreach (var reminder in scheduled)
        {
            reminder.Status = ReminderStatus.Cancelled;
        }
    }
}
