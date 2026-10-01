using System.Security.Claims;

namespace ECommerce.API.Common.Middleware;

public sealed class UserIdPreProcessor
{
    private readonly RequestDelegate _next;

    public UserIdPreProcessor(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            if (context.Request.Headers.TryGetValue("x-user-id", out var rawUserId)
                && Guid.TryParse(rawUserId, out var userId))
            {
                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    authenticationType: "header");

                context.User = new ClaimsPrincipal(identity);
            }
        }

        var currentUserId = ECommerce.API.Common.UserClaims.GetUserId(context.User);

        if (currentUserId is not null)
        {
            context.Items["CurrentUserId"] = currentUserId.Value;
        }

        await _next(context);
    }
}
