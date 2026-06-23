using System.Security.Cryptography;
using System.Text;

namespace OrleansMatchingServer;

public static class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Pbkdf2(password, salt, Iterations);

        return string.Join(
            ':',
            Prefix,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static PasswordVerificationResult Verify(string password, string storedHash)
    {
        if (storedHash.StartsWith(Prefix + ':', StringComparison.Ordinal))
            return VerifyPbkdf2(password, storedHash);

        return VerifyLegacySha256(password, storedHash);
    }

    private static PasswordVerificationResult VerifyPbkdf2(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 4 ||
            int.TryParse(parts[1], out var iterations) == false ||
            iterations <= 0)
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        var actualHash = Pbkdf2(password, salt, iterations);
        var verified = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);

        return verified
            ? new PasswordVerificationResult(true, iterations < Iterations)
            : PasswordVerificationResult.Failed;
    }

    private static PasswordVerificationResult VerifyLegacySha256(string password, string storedHash)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var legacyHash = Convert.ToHexString(bytes);
        var verified = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(legacyHash),
            Encoding.ASCII.GetBytes(storedHash));

        return verified
            ? new PasswordVerificationResult(true, NeedsRehash: true)
            : PasswordVerificationResult.Failed;
    }

    private static byte[] Pbkdf2(string password, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);
    }
}

public readonly record struct PasswordVerificationResult(bool Verified, bool NeedsRehash)
{
    public static PasswordVerificationResult Failed { get; } = new(false, false);
}
