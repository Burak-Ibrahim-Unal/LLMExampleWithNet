using MediatR;
using Products.Application.Contracts;
using Products.Domain.Repositories;
using Shared.Application.Common;

namespace Products.Application.Queries.GetProducts;

public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, ApiResult<List<ProductDto>>>
{
    private readonly IProductRepository _productRepository;

    public GetProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ApiResult<List<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _productRepository.ListAsync(cancellationToken);

        var payload = products
            .Select(x => new ProductDto(x.Id, x.Name, x.Price, x.Stock, x.DeletedAtUtc))
            .ToList();

        return ApiResult<List<ProductDto>>.Ok(payload).WithStatus(200);
    }
}
