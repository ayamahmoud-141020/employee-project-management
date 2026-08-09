using EPM.Application.Abstractions;

namespace EPM.Infrastructure.Time;

/// <summary>
/// The real clock. The only place in the codebase that reads system time — everything else
/// takes <see cref="IDateTimeProvider"/> so it can be frozen in a test.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
