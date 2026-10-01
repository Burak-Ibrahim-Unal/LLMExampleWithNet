using MediatR;
using Products.Application.Contracts;
using Shared.Application.Common;

namespace Products.Application.Queries.GetProducts;

public sealed record GetProductsQuery : IRequest<ApiResult<List<ProductDto>>>;
