namespace ECommerce.API.Common.Middleware;

public sealed class SubscriptionPreProcessor
{
    private readonly RequestDelegate _next;

    public SubscriptionPreProcessor(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var plan = context.Request.Headers.TryGetValue("x-user-plan", out var userPlan)
            ? userPlan.ToString()
            : "free";

        context.Items["UserPlan"] = plan;

        await _next(context);
    }
}
