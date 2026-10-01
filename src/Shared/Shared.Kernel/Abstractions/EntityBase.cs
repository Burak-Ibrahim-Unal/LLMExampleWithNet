namespace Shared.Kernel.Abstractions;

public abstract class EntityBase
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; private set; }

    public void MarkUpdated(DateTime? utcNow = null)
    {
        UpdatedAtUtc = utcNow ?? DateTime.UtcNow;
    }
}
