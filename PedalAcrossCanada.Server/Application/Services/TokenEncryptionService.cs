using Microsoft.AspNetCore.DataProtection;
using PedalAcrossCanada.Server.Application.Interfaces;

namespace PedalAcrossCanada.Server.Application.Services;

public class TokenEncryptionService : ITokenEncryptionService
{
    private const string Purpose = "PedalAcrossCanada.Strava.Tokens";
    private readonly IDataProtector _protector;

    public TokenEncryptionService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);
        return _protector.Protect(plainText);
    }

    public string Decrypt(string cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);
        return _protector.Unprotect(cipherText);
    }
}
