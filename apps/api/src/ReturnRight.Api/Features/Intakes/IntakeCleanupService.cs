using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Storage;

namespace ReturnRight.Api.Features.Intakes;

public class IntakeCleanupService(AppDbContext db, IFileStorage storage, TimeProvider time)
{
    public async Task<int> CleanupExpiredAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var expired = await db.TemporaryIntakes
            .Where(t => t.ExpiresAt < now)
            .ToListAsync(ct);
        if (expired.Count == 0)
        {
            return 0;
        }

        db.TemporaryIntakes.RemoveRange(expired);
        await db.SaveChangesAsync(ct);

        foreach (var intake in expired)
        {
            await storage.DeleteAsync(intake.StorageKey, ct);
        }
        return expired.Count;
    }
}
