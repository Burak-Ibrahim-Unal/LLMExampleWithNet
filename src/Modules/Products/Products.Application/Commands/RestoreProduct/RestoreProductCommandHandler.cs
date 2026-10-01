using MediatR;
using Products.Application.BusinessRules;
using Products.Domain.Repositories;
using Shared.Application.Common;

namespace Products.Application.Commands.RestoreProduct;

public sealed class RestoreProductCommandHandler : IRequestHandler<RestoreProductCommand, ApiResult<string>>
{
    private readonly IProductRepository _productRepository;
    private readonly ProductBusinessRules _rules;

    public RestoreProductCommandHandler(IProductRepository productRepository, ProductBusinessRules rules)
    {
        _productRepository = productRepository;
        _rules = rules;
    }

    public async Task<ApiResult<string>> Handle(RestoreProductCommand request, CancellationToken cancellationToken)
    {
        var entity = await _productRepository.GetByIdWithDeletedAsync(request.ProductId, cancellationToken);

        var restoreRuleError = _rules.CheckCanRestore<string>(entity);
        if (restoreRuleError is not null)
        {
            return restoreRuleError;
        }

        entity!.Restore();
        _productRepository.Update(entity);
        await _productRepository.SaveChangesAsync(cancellationToken);

        return ApiResult<string>.Ok(Messages.Products.Restored, Messages.Products.Restored).WithStatus(200);
    }
}
