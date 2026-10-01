namespace Shared.Kernel.Abstractions;

public interface ISoftDeletable
{
    DateTime? DeletedAtUtc { get; set; }
}
