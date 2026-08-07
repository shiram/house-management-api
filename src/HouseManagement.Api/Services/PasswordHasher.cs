using System.Security.Cryptography;

namespace HouseManagement.Api.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = new byte[SaltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(HashSize);

        var result = new byte[1 + SaltSize + HashSize];
        result[0] = 0; // version
        Buffer.BlockCopy(salt, 0, result, 1, SaltSize);
        Buffer.BlockCopy(hash, 0, result, 1 + SaltSize, HashSize);

        return Convert.ToBase64String(result);
    }

    public bool Verify(string hashed, string password)
    {
        try
        {
            var bytes = Convert.FromBase64String(hashed);
            if (bytes.Length != 1 + SaltSize + HashSize) return false;
            var salt = new byte[SaltSize];
            Buffer.BlockCopy(bytes, 1, salt, 0, SaltSize);
            var storedHash = new byte[HashSize];
            Buffer.BlockCopy(bytes, 1 + SaltSize, storedHash, 0, HashSize);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            var computedHash = pbkdf2.GetBytes(HashSize);

            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
        catch
        {
            return false;
        }
    }
}