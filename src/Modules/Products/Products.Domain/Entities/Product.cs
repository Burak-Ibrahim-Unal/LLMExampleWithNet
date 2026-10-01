using Shared.Kernel.Abstractions;

namespace Products.Domain.Entities;

public sealed class Product : EntityBase, ISoftDeletable
{
    private Product()
    {
    }

    public Product(string name, decimal price, int stock)
    {
        Name = name;
        Price = price;
        Stock = stock;
    }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public int Stock { get; private set; }

    public DateTime? DeletedAtUtc { get; set; }

    public void Update(string name, decimal price, int stock)
    {
        Name = name;
        Price = price;
        Stock = stock;
        MarkUpdated();
    }

    public void SoftDelete()
    {
        DeletedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Restore()
    {
        DeletedAtUtc = null;
        MarkUpdated();
    }
}
