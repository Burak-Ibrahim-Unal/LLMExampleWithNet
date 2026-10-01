using FastEndpoints;
using Products.Application.Contracts;
using Products.Service.Abstractions;
using Shared.Application.Common;

namespace ECommerce.API.Endpoints.Products;

public sealed class GetProductsEndpoint : EndpointWithoutRequest<ApiResult<List<ProductDto>>>
{
    private readonly IProductService _productService;

    public GetProductsEndpoint(IProductService productService)
    {
        _productService = productService;
    }

    public override void Configure()
    {
        Get("products");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = ECommerce.API.Common.UserClaims.GetUserId(User);

        if (userId is null)
        {
            var unauthorized = ApiResult<List<ProductDto>>.Fail(Messages.Authorization.Unauthorized, 401).WithStatus(401);
            await SendAsync(unauthorized, unauthorized.StatusCode, ct);
            return;
        }

        var result = await _productService.ListAsync(ct);
        await SendAsync(result, result.StatusCode, ct);
    }
}
