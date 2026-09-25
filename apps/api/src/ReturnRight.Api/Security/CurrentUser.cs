using System.Security.Claims;

namespace ReturnRight.Api.Security;

public class CurrentUser(IHttpContextAccessor accessor)
{
    public Guid Id
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(value, out var id))
            {
                throw new InvalidOperationException("The current user is not authenticated.");
            }
            return id;
        }
    }
}
