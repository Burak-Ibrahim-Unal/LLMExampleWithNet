using FastEndpoints;
using Products.Application.Contracts;
using Products.Service.Abstractions;
using Shared.Application.Abstractions;
using Shared.Application.Common;

namespace ECommerce.API.Endpoints.Products;

public sealed class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Stock { get; set; }
}

public sealed class CreateProductEndpoint : Endpoint<CreateProductRequest, ApiResult<ProductDto>>
{
    private readonly IProductService _productService;
    private readonly ISubscriptionQuotaService _subscriptionQuotaService;

    public CreateProductEndpoint(IProductService productService, ISubscriptionQuotaService subscriptionQuotaService)
    {
        _productService = productService;
        _subscriptionQuotaService = subscriptionQuotaService;
    }

    public override void Configure()
    {
        Post("products");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateProductRequest req, CancellationToken ct)
    {
        var userId = ECommerce.API.Common.UserClaims.GetUserId(User);

        if (userId is null)
        {
            var unauthorized = ApiResult<ProductDto>.Fail(Messages.Authorization.Unauthorized, 401).WithStatus(401);
            await SendAsync(unauthorized, unauthorized.StatusCode, ct);
            return;
        }

        var userPlan = HttpContext.Items.TryGetValue("UserPlan", out var userPlanValue)
            ? userPlanValue?.ToString() ?? "free"
            : "free";

        var exceedsQuota = await _subscriptionQuotaService.ExceedsCreateQuotaAsync(userId.Value, "products", userPlan, ct);

        if (exceedsQuota)
        {
            var quotaError = ApiResult<ProductDto>.Fail(Messages.Subscription.CreateQuotaExceeded, 403).WithStatus(403);
            await SendAsync(quotaError, quotaError.StatusCode, ct);
            return;
        }

        var result = await _productService.CreateAsync(req.Name, req.Price, req.Stock, ct);
        await SendAsync(result, result.StatusCode, ct);
    }
}
