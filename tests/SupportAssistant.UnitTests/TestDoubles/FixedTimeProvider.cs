namespace SupportAssistant.UnitTests.TestDoubles;

internal sealed class FixedTimeProvider(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
}
