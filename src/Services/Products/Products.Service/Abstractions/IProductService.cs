using Products.Application.Contracts;
using Shared.Application.Common;

namespace Products.Service.Abstractions;

public interface IProductService
{
    Task<ApiResult<ProductDto>> CreateAsync(string name, decimal price, int stock, CancellationToken cancellationToken = default);

    Task<ApiResult<List<ProductDto>>> ListAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<string>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiResult<string>> RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
