using System.Security.Cryptography;
using LockCopmuter.Models;

namespace LockCopmuter.Services;

public interface IPasswordService
{
    bool HasPassword(ScheduleSettings settings);
    void SetPassword(ScheduleSettings settings, string password);
    bool Verify(ScheduleSettings settings, string password);
}

public class PasswordService : IPasswordService
{
    public bool HasPassword(ScheduleSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.PasswordHashBase64) &&
        !string.IsNullOrWhiteSpace(settings.PasswordSaltBase64);

    public void SetPassword(ScheduleSettings settings, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Hash(password, salt);

        settings.PasswordSaltBase64 = Convert.ToBase64String(salt);
        settings.PasswordHashBase64 = Convert.ToBase64String(hash);
    }

    public bool Verify(ScheduleSettings settings, string password)
    {
        if (!HasPassword(settings)) return true;

        var salt = Convert.FromBase64String(settings.PasswordSaltBase64);
        var expected = Convert.FromBase64String(settings.PasswordHashBase64);
        var actual = Hash(password, salt);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] Hash(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations: 120_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);
    }
}
