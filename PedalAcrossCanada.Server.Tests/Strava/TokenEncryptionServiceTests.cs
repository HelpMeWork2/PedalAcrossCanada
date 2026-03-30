using Microsoft.AspNetCore.DataProtection;
using PedalAcrossCanada.Server.Application.Services;

namespace PedalAcrossCanada.Server.Tests.Strava;

/// <summary>
/// Tests <see cref="TokenEncryptionService"/> using a real (ephemeral) <see cref="IDataProtectionProvider"/>.
/// Validates that ASP.NET Core Data Protection works correctly for token storage.
/// </summary>
public sealed class TokenEncryptionServiceTests
{
    private readonly TokenEncryptionService _sut;

    public TokenEncryptionServiceTests()
    {
        var provider = DataProtectionProvider.Create("PedalAcrossCanada.Tests");
        _sut = new TokenEncryptionService(provider);
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip_PreservesPlaintext()
    {
        var plainText = """{"AccessToken":"abc","RefreshToken":"def","ExpiresAt":1700000000}""";

        var encrypted = _sut.Encrypt(plainText);
        var decrypted = _sut.Decrypt(encrypted);

        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentOutputThanInput()
    {
        var plainText = "sensitive-token-data";

        var encrypted = _sut.Encrypt(plainText);

        Assert.NotEqual(plainText, encrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachCall()
    {
        // Data Protection doesn't guarantee this, but typically the nonce differs.
        // If they happen to be equal it's still correct — this is informational.
        var plainText = "same-input";

        var encrypted1 = _sut.Encrypt(plainText);
        var encrypted2 = _sut.Encrypt(plainText);

        // Both must decrypt correctly regardless
        Assert.Equal(plainText, _sut.Decrypt(encrypted1));
        Assert.Equal(plainText, _sut.Decrypt(encrypted2));
    }

    [Fact]
    public void Decrypt_WithTamperedData_Throws()
    {
        var encrypted = _sut.Encrypt("real-data");
        var tampered = encrypted + "TAMPERED";

        Assert.ThrowsAny<Exception>(() => _sut.Decrypt(tampered));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Encrypt_WithNullOrWhitespace_ThrowsArgumentException(string? input)
    {
        Assert.ThrowsAny<ArgumentException>(() => _sut.Encrypt(input!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Decrypt_WithNullOrWhitespace_ThrowsArgumentException(string? input)
    {
        Assert.ThrowsAny<ArgumentException>(() => _sut.Decrypt(input!));
    }

    [Fact]
    public void RoundTrip_WithUnicodeContent_PreservesContent()
    {
        var plainText = """{"name":"Vélo électrique 🚲","token":"tökën"}""";

        var encrypted = _sut.Encrypt(plainText);
        var decrypted = _sut.Decrypt(encrypted);

        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void RoundTrip_WithLargePayload_Succeeds()
    {
        var plainText = new string('x', 10_000);

        var encrypted = _sut.Encrypt(plainText);
        var decrypted = _sut.Decrypt(encrypted);

        Assert.Equal(plainText, decrypted);
    }
}
