using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Email;
using ReturnRight.Api.Features.Cases;
using ReturnRight.Api.Persistence;

namespace ReturnRight.Api.Features.Reminders;

public record ReminderProcessResult(int Processed, int Sent, int Failed, int Cancelled);

public class ReminderProcessor(
    AppDbContext db,
    IEmailSender emailSender,
    TimeProvider time,
    IOptions<ReminderOptions> options,
    IConfiguration configuration,
    ILogger<ReminderProcessor> logger)
{
    public async Task<ReminderProcessResult> ProcessDueAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var due = await db.Reminders
            .Include(r => r.FollowUp)
            .Include(r => r.Case).ThenInclude(c => c.Purchase)
            .Include(r => r.Case).ThenInclude(c => c.AffectedItems).ThenInclude(a => a.PurchaseItem)
            .Include(r => r.User)
            .Where(r => r.Status == ReminderStatus.Scheduled && r.NextAttemptAt <= now)
            .OrderBy(r => r.NextAttemptAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(ct);

        var result = new ReminderProcessResult(due.Count, 0, 0, 0);
        foreach (var reminder in due)
        {
            var outcome = await ProcessOneAsync(reminder, now, ct);
            await db.SaveChangesAsync(ct); // per reminder: one failure never rolls back others
            result = outcome switch
            {
                ProcessOutcome.Sent => result with { Sent = result.Sent + 1 },
                ProcessOutcome.Failed => result with { Failed = result.Failed + 1 },
                ProcessOutcome.Cancelled => result with { Cancelled = result.Cancelled + 1 },
                _ => result,
            };
            logger.LogInformation("Reminder {Id} -> {Status}", reminder.Id, reminder.Status);
        }
        return result;
    }

    private enum ProcessOutcome { Sent, Retried, Failed, Cancelled }

    private async Task<ProcessOutcome> ProcessOneAsync(
        Reminder reminder, DateTimeOffset now, CancellationToken ct)
    {
        var followUpDone = reminder.FollowUp is not null
            && (reminder.FollowUp.CompletedAt is not null || reminder.FollowUp.CancelledAt is not null);
        var caseDone = reminder.Case.Status is CaseStatus.Resolved or CaseStatus.Closed;
        if (followUpDone || caseDone)
        {
            reminder.Status = ReminderStatus.Cancelled;
            return ProcessOutcome.Cancelled;
        }

        var caseTitle = CaseTitle.Build(reminder.Case);
        try
        {
            if (reminder.Channel == ReminderChannel.Email)
            {
                var caseUrl = CaseUrl(reminder.CaseId);
                var message = EmailTemplates.FollowUpReminder(
                    reminder.User.Email!,
                    caseTitle,
                    reminder.FollowUp?.Title ?? "Check in with the seller",
                    reminder.ScheduledFor,
                    caseUrl);
                await emailSender.SendAsync(message, ct);
            }
            else
            {
                db.InAppNotifications.Add(new InAppNotification
                {
                    UserId = reminder.UserId,
                    CaseId = reminder.CaseId,
                    Title = $"Follow-up due: {reminder.FollowUp?.Title ?? "Check in"}",
                    Body = $"Your follow-up for {caseTitle} is due {reminder.ScheduledFor:d MMM yyyy}.",
                    CreatedAt = now,
                });
            }

            reminder.Status = ReminderStatus.Sent;
            reminder.SentAt = now;
            return ProcessOutcome.Sent;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            reminder.AttemptCount++;
            reminder.LastAttemptAt = now;
            reminder.LastError = TruncatedError(ex, reminder.User.Email);

            var delay = ReminderRetryPolicy.NextDelay(
                options.Value.EffectiveRetryDelays, reminder.AttemptCount);
            if (delay is not null)
            {
                reminder.NextAttemptAt = now + delay.Value;
                return ProcessOutcome.Retried;
            }

            reminder.Status = ReminderStatus.Failed;
            if (reminder.Channel == ReminderChannel.Email)
            {
                try
                {
                    db.InAppNotifications.Add(new InAppNotification
                    {
                        UserId = reminder.UserId,
                        CaseId = reminder.CaseId,
                        Title = "We couldn't send your reminder email",
                        Body = $"Your follow-up '{reminder.FollowUp?.Title ?? "Check in"}' is due "
                            + $"{reminder.ScheduledFor:d MMM yyyy}, but the reminder email couldn't be sent.",
                        CreatedAt = now,
                    });
                }
                catch (Exception notificationError)
                {
                    logger.LogWarning(notificationError, "Failed-notification insert failed");
                }
            }
            return ProcessOutcome.Failed;
        }
    }

    private string CaseUrl(Guid caseId)
    {
        var baseUrl = configuration["App:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
        return $"{baseUrl}/cases/{caseId}";
    }

    private static string TruncatedError(Exception ex, string? recipientEmail)
    {
        var text = $"{ex.GetType().Name}: {ex.Message}";
        if (!string.IsNullOrEmpty(recipientEmail))
        {
            text = text.Replace(recipientEmail, "[redacted]", StringComparison.OrdinalIgnoreCase);
        }
        return text.Length <= 500 ? text : text[..500];
    }
}
