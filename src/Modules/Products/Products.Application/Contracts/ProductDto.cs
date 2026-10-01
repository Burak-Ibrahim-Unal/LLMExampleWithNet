namespace Products.Application.Contracts;

public sealed record ProductDto(Guid Id, string Name, decimal Price, int Stock, DateTime? DeletedAtUtc);
