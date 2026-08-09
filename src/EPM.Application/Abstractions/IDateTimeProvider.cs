namespace EPM.Application.Abstractions;

/// <summary>
/// The clock, injected rather than read statically.
/// </summary>
/// <remarks>
/// Exists so rules like "hire date cannot be in the future" are testable without freezing
/// system time. Handlers read the date here and pass it into the domain, which never reaches
/// for DateTime.Now itself.
/// </remarks>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    /// <summary>
    /// Today in UTC. Dates in this system are calendar facts (a hire date, a project start),
    /// not instants, so they are anchored to one zone rather than the server's local setting.
    /// </summary>
    DateOnly Today { get; }
}
