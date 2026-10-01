using System.Security.Claims;

namespace ECommerce.API.Common;

public static class UserClaims
{
    public static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }
}
