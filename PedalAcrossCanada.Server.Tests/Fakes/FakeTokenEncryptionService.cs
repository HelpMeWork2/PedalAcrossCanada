using PedalAcrossCanada.Server.Application.Interfaces;

namespace PedalAcrossCanada.Server.Tests.Fakes;

/// <summary>
/// Reversible Base64 "encryption" so tests can verify round-trip without Data Protection infrastructure.
/// </summary>
public sealed class FakeTokenEncryptionService : ITokenEncryptionService
{
    private const string Prefix = "ENC:";

    public string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);
        return Prefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));
    }

    public string Decrypt(string cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);
        if (!cipherText.StartsWith(Prefix))
            throw new InvalidOperationException("Invalid encrypted data.");
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText[Prefix.Length..]));
    }
}
