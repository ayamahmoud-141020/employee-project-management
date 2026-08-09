namespace EPM.Application.Abstractions;

/// <summary>
/// Password hashing. Kept behind an interface so the algorithm can be replaced without
/// touching a handler, and so the domain never learns what a salt is.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Verifies a password against a stored hash. Implementations must compare in constant
    /// time — a fast "wrong" answer leaks how much of the hash matched.
    /// </summary>
    bool Verify(string password, string hash);

    /// <summary>
    /// A valid hash of a value nobody knows, for the login path to verify against when no
    /// account matches the submitted email.
    /// </summary>
    /// <remarks>
    /// Returning early instead makes "no such user" measurably faster than "wrong password",
    /// which is enough to enumerate registered addresses with a stopwatch. It has to be a
    /// real hash — a malformed string would be rejected on parse and cost nothing, defeating
    /// the point — and it must be precomputed, because deriving one per failed login would
    /// hand an attacker a cheap way to make the server do expensive work.
    /// </remarks>
    string DummyHash { get; }
}
