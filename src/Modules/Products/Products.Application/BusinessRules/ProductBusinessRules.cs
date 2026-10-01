using Products.Domain.Entities;
using Products.Domain.Repositories;
using Shared.Application.Common;

namespace Products.Application.BusinessRules;

public sealed class ProductBusinessRules
{
    private readonly IProductRepository _productRepository;

    public ProductBusinessRules(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public ApiResult<T>? CheckNameRequired<T>(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Products.NameRequired, 400).WithStatus(400);
    }

    public ApiResult<T>? CheckPricePositive<T>(decimal price)
    {
        if (price > 0)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Products.PriceMustBePositive, 400).WithStatus(400);
    }

    public ApiResult<T>? CheckStockNonNegative<T>(int stock)
    {
        if (stock >= 0)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Products.StockCannotBeNegative, 400).WithStatus(400);
    }

    public async Task<ApiResult<T>?> CheckDuplicateAsync<T>(
        string normalizedName,
        Guid? excludingProductId = null,
        CancellationToken cancellationToken = default)
    {
        var exists = await _productRepository.ExistsByNameAsync(normalizedName, excludingProductId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Products.DuplicateName, 409).WithStatus(409);
    }

    public ApiResult<T>? CheckProductFound<T>(Product? product)
    {
        if (product is not null)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Products.NotFound, 404).WithStatus(404);
    }

    public ApiResult<T>? CheckCanRestore<T>(Product? product)
    {
        if (product is null)
        {
            return ApiResult<T>.Fail(Messages.Products.NotFound, 404).WithStatus(404);
        }

        if (product.DeletedAtUtc is not null)
        {
            return null;
        }

        return ApiResult<T>.Fail(Messages.Products.AlreadyActive, 400).WithStatus(400);
    }
}
