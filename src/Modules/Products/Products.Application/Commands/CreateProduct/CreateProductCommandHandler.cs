using MediatR;
using Products.Application.BusinessRules;
using Products.Application.Contracts;
using Products.Domain.Entities;
using Products.Domain.Repositories;
using Shared.Application.Common;

namespace Products.Application.Commands.CreateProduct;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ApiResult<ProductDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly ProductBusinessRules _rules;

    public CreateProductCommandHandler(IProductRepository productRepository, ProductBusinessRules rules)
    {
        _productRepository = productRepository;
        _rules = rules;
    }

    public async Task<ApiResult<ProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();

        var nameError = _rules.CheckNameRequired<ProductDto>(normalizedName);
        if (nameError is not null)
        {
            return nameError;
        }

        var priceError = _rules.CheckPricePositive<ProductDto>(request.Price);
        if (priceError is not null)
        {
            return priceError;
        }

        var stockError = _rules.CheckStockNonNegative<ProductDto>(request.Stock);
        if (stockError is not null)
        {
            return stockError;
        }

        var duplicateError = await _rules.CheckDuplicateAsync<ProductDto>(normalizedName, cancellationToken: cancellationToken);
        if (duplicateError is not null)
        {
            return duplicateError;
        }

        var product = new Product(normalizedName, request.Price, request.Stock);

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        var dto = new ProductDto(product.Id, product.Name, product.Price, product.Stock, product.DeletedAtUtc);

        return ApiResult<ProductDto>.Created(dto, Messages.Products.Created).WithStatus(201);
    }
}
