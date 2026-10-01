using MediatR;
using Products.Application.Contracts;
using Shared.Application.Common;

namespace Products.Application.Commands.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price, int Stock) : IRequest<ApiResult<ProductDto>>;
