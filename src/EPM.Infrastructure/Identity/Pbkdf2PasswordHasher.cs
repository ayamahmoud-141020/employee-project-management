using System.Security.Cryptography;
using EPM.Application.Abstractions;

namespace EPM.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing, in the same storage format ASP.NET Core Identity
/// uses so a later migration onto Identity would not invalidate existing passwords.
/// </summary>
/// <remarks>
/// Written out rather than pulled from a package because the whole implementation is 40 lines
/// and the project does not otherwise need ASP.NET Core Identity's user store, roles tables
/// or sign-in manager. Argon2id would be a stronger choice but has no in-box implementation.
/// </remarks>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int SubkeySize = 32;

    // OWASP's floor for PBKDF2-HMAC-SHA256. Raise it as hardware improves; the value is
    // written into each hash, so old passwords keep verifying with the count they were
    // created under.
    private const int Iterations = 210_000;

    // Derived once for the lifetime of the process, off a value that never leaves this field.
    // Registered as a singleton, so every failed login shares the one derivation.
    private static readonly Lazy<string> DummyHashValue =
        new(() => new Pbkdf2PasswordHasher().Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

    public string DummyHash => DummyHashValue.Value;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, SubkeySize);

        // Layout: [iterations][saltSize][salt][subkey] — self-describing, so verification
        // never has to guess which parameters produced a stored hash.
        var payload = new byte[sizeof(int) + sizeof(int) + SaltSize + SubkeySize];

        BitConverter.TryWriteBytes(payload.AsSpan(0, sizeof(int)), Iterations);
        BitConverter.TryWriteBytes(payload.AsSpan(sizeof(int), sizeof(int)), SaltSize);
        salt.CopyTo(payload.AsSpan(sizeof(int) * 2, SaltSize));
        subkey.CopyTo(payload.AsSpan(sizeof(int) * 2 + SaltSize, SubkeySize));

        return Convert.ToBase64String(payload);
    }

    public bool Verify(string password, string hash)
    {
        byte[] payload;

        try
        {
            payload = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            // A corrupted or hand-edited hash is a failed login, not a 500.
            return false;
        }

        if (payload.Length <= sizeof(int) * 2)
        {
            return false;
        }

        var iterations = BitConverter.ToInt32(payload, 0);
        var saltSize = BitConverter.ToInt32(payload, sizeof(int));

        if (iterations <= 0 || saltSize <= 0 || payload.Length != sizeof(int) * 2 + saltSize + SubkeySize)
        {
            return false;
        }

        var salt = payload.AsSpan(sizeof(int) * 2, saltSize).ToArray();
        var expectedSubkey = payload.AsSpan(sizeof(int) * 2 + saltSize).ToArray();

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, expectedSubkey.Length);

        // Constant-time: a plain == would return early on the first differing byte, which is
        // enough of a timing signal to recover a hash one byte at a time.
        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}
