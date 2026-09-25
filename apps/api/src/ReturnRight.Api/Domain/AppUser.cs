using Microsoft.AspNetCore.Identity;

namespace ReturnRight.Api.Domain;

public class AppUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; }
    public bool EmailRemindersEnabled { get; set; } = true;
    public bool InAppRemindersEnabled { get; set; } = true;
}
