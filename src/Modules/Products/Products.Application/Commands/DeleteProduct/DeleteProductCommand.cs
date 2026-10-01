using MediatR;
using Shared.Application.Common;

namespace Products.Application.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid ProductId) : IRequest<ApiResult<string>>;
