using MediatR;
using Shared.Application.Common;

namespace Products.Application.Commands.RestoreProduct;

public sealed record RestoreProductCommand(Guid ProductId) : IRequest<ApiResult<string>>;
