using FastEndpoints;
using Products.Service.Abstractions;
using Shared.Application.Common;

namespace ECommerce.API.Endpoints.Products;

public sealed class RestoreProductRequest
{
    public Guid ProductId { get; set; }
}

public sealed class RestoreProductEndpoint : Endpoint<RestoreProductRequest, ApiResult<string>>
{
    private readonly IProductService _productService;

    public RestoreProductEndpoint(IProductService productService)
    {
        _productService = productService;
    }

    public override void Configure()
    {
        Post("products/{productId}/restore");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RestoreProductRequest req, CancellationToken ct)
    {
        var userId = ECommerce.API.Common.UserClaims.GetUserId(User);

        if (userId is null)
        {
            var unauthorized = ApiResult<string>.Fail(Messages.Authorization.Unauthorized, 401).WithStatus(401);
            await SendAsync(unauthorized, unauthorized.StatusCode, ct);
            return;
        }

        var result = await _productService.RestoreAsync(req.ProductId, ct);
        await SendAsync(result, result.StatusCode, ct);
    }
}
