using MediatR;
using Products.Application.BusinessRules;
using Products.Domain.Repositories;
using Shared.Application.Common;

namespace Products.Application.Commands.DeleteProduct;

public sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, ApiResult<string>>
{
    private readonly IProductRepository _productRepository;
    private readonly ProductBusinessRules _rules;

    public DeleteProductCommandHandler(IProductRepository productRepository, ProductBusinessRules rules)
    {
        _productRepository = productRepository;
        _rules = rules;
    }

    public async Task<ApiResult<string>> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var entity = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);

        var notFoundError = _rules.CheckProductFound<string>(entity);
        if (notFoundError is not null)
        {
            return notFoundError;
        }

        entity!.SoftDelete();
        _productRepository.Update(entity);
        await _productRepository.SaveChangesAsync(cancellationToken);

        return ApiResult<string>.Ok(Messages.Products.Deleted, Messages.Products.Deleted).WithStatus(200);
    }
}
