using FastEndpoints;
using Products.Service.Abstractions;
using Shared.Application.Common;

namespace ECommerce.API.Endpoints.Products;

public sealed class DeleteProductRequest
{
    public Guid ProductId { get; set; }
}

public sealed class DeleteProductEndpoint : Endpoint<DeleteProductRequest, ApiResult<string>>
{
    private readonly IProductService _productService;

    public DeleteProductEndpoint(IProductService productService)
    {
        _productService = productService;
    }

    public override void Configure()
    {
        Delete("products/{productId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DeleteProductRequest req, CancellationToken ct)
    {
        var userId = ECommerce.API.Common.UserClaims.GetUserId(User);

        if (userId is null)
        {
            var unauthorized = ApiResult<string>.Fail(Messages.Authorization.Unauthorized, 401).WithStatus(401);
            await SendAsync(unauthorized, unauthorized.StatusCode, ct);
            return;
        }

        var result = await _productService.DeleteAsync(req.ProductId, ct);
        await SendAsync(result, result.StatusCode, ct);
    }
}
