using LightQueryProfiler.Shared.Services;

namespace LightQueryProfiler.Shared.UnitTests.Services;

public class AesGcmPasswordProtectionServiceTests
{
    private readonly AesGcmPasswordProtectionService _sut = new();

    [Fact]
    public void Encrypt_WhenPlainTextIsNull_ReturnsNull()
    {
        // Act
        var result = _sut.Encrypt(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Encrypt_WhenPlainTextIsEmpty_ReturnsEmpty()
    {
        // Act
        var result = _sut.Encrypt(string.Empty);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Encrypt_WhenPlainTextIsValid_ReturnsBase64String()
    {
        // Act
        var result = _sut.Encrypt("MySecurePassword!");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        // Must be valid Base64
        var bytes = Convert.FromBase64String(result);
        // Minimum bytes: 12 (nonce) + 16 (tag) + at least 1 (ciphertext)
        Assert.True(bytes.Length >= 29);
    }

    [Fact]
    public void Decrypt_WhenCipherTextIsNull_ReturnsNull()
    {
        // Act
        var result = _sut.Decrypt(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Decrypt_WhenCipherTextIsEmpty_ReturnsEmpty()
    {
        // Act
        var result = _sut.Decrypt(string.Empty);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Decrypt_WhenValidCipher_ReturnsOriginalPlainText()
    {
        // Arrange
        const string original = "SuperSecret123!";
        var cipher = _sut.Encrypt(original);

        // Act
        var result = _sut.Decrypt(cipher);

        // Assert
        Assert.Equal(original, result);
    }

    [Fact]
    public void Decrypt_WhenCipherIsLegacyPlainText_ReturnsCipherTextUnchanged()
    {
        // Arrange — a plain-text password that was never encrypted
        const string legacyPlainText = "plainpassword";

        // Act
        var result = _sut.Decrypt(legacyPlainText);

        // Assert — fallback: original value returned unchanged
        Assert.Equal(legacyPlainText, result);
    }

    [Fact]
    public void EncryptThenDecrypt_RoundTrip_ReturnsOriginal()
    {
        // Arrange
        const string original = "RoundTripPassword@2026";

        // Act
        var cipher = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(cipher);

        // Assert
        Assert.Equal(original, decrypted);
    }
}
