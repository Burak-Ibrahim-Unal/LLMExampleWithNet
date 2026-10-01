using MediatR;
using Products.Application.Commands.CreateProduct;
using Products.Application.Commands.DeleteProduct;
using Products.Application.Commands.RestoreProduct;
using Products.Application.Contracts;
using Products.Application.Queries.GetProducts;
using Products.Service.Abstractions;
using Shared.Application.Common;

namespace Products.Service;

public sealed class ProductService : IProductService
{
    private readonly ISender _sender;

    public ProductService(ISender sender)
    {
        _sender = sender;
    }

    public Task<ApiResult<ProductDto>> CreateAsync(string name, decimal price, int stock, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new CreateProductCommand(name, price, stock), cancellationToken);
    }

    public Task<ApiResult<List<ProductDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _sender.Send(new GetProductsQuery(), cancellationToken);
    }

    public Task<ApiResult<string>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new DeleteProductCommand(id), cancellationToken);
    }

    public Task<ApiResult<string>> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _sender.Send(new RestoreProductCommand(id), cancellationToken);
    }
}
