using System.Security.Cryptography;

namespace Manifest.Services;

/// <summary>
/// PBKDF2-SHA256, from the framework, because a public server needs password
/// storage that survives a stolen database file and this is the strongest option
/// that adds no dependency. Argon2id would be better still but every .NET binding
/// for it is a native package, and shipping one would mean the Docker image and
/// every developer machine had to carry it.
/// </summary>
public static class Passwords
{
    /// <summary>OWASP's 2023 floor for PBKDF2-SHA256. Roughly 100ms on a modest VPS.</summary>
    public const int Iterations = 210_000;

    const int SaltBytes = 16;
    const int HashBytes = 32;

    /// <summary>
    /// The iteration count is stored in the string rather than read from the constant
    /// above, so raising the constant later leaves every existing password verifiable
    /// instead of locking everyone out.
    /// </summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations,
                                             HashAlgorithmName.SHA256, HashBytes);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}"
               + $"${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256") return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations is < 1 or > 10_000_000)
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations,
                                               HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// What the rules are, in one place, so the API message and the login page's
    /// hint cannot drift apart. Deliberately only a length floor: composition rules
    /// push people towards "Passw0rd!" and away from long passphrases.
    /// </summary>
    public const int MinPasswordLength = 10;

    public static string? RejectPassword(string? password) => password switch
    {
        null or "" => "A password is required.",
        { Length: < MinPasswordLength } =>
            $"Use at least {MinPasswordLength} characters — a phrase is easier to "
            + "remember than a short scramble, and harder to guess.",
        { Length: > 400 } => "That password is unreasonably long.",
        _ => null,
    };

    /// <summary>
    /// Usernames are the public handle and a URL-safe subset keeps them boring:
    /// no spaces to trim differently in two places, no case games (Admin vs admin),
    /// nothing that needs escaping when it lands in HTML.
    /// </summary>
    public static string? RejectUsername(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "A username is required.";
        var n = name.Trim();
        if (n.Length is < 2 or > 32) return "Usernames are 2 to 32 characters.";
        foreach (var c in n)
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.'))
                return "Usernames use letters, digits, dot, dash and underscore only.";
        return null;
    }
}
