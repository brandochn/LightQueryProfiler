using LightQueryProfiler.WinFormsApp.Services;

namespace LightQueryProfiler.WinFormsApp.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="DpapiPasswordProtectionService"/>.
/// These tests require Windows (DPAPI is a Windows-only feature).
/// </summary>
public class DpapiPasswordProtectionServiceTests
{
    private readonly DpapiPasswordProtectionService _sut = new();

    // -------------------------------------------------------------------------
    // Encrypt – boundary / guard cases
    // -------------------------------------------------------------------------

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

    [Theory]
    [InlineData("MySecretP@ssw0rd!")]
    [InlineData("password123")]
    [InlineData("p")]
    [InlineData("unicode: áéíóú ñ 日本語")]
    public void Encrypt_WhenPlainTextProvided_ReturnsDifferentValue(string plainText)
    {
        // Act
        var result = _sut.Encrypt(plainText);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(plainText, result);
    }

    [Theory]
    [InlineData("MySecretP@ssw0rd!")]
    [InlineData("password123")]
    [InlineData("p")]
    [InlineData("unicode: áéíóú ñ 日本語")]
    public void Encrypt_WhenPlainTextProvided_ReturnsValidBase64(string plainText)
    {
        // Act
        var result = _sut.Encrypt(plainText);

        // Assert – should not throw
        Assert.NotNull(result);
        var decoded = Convert.FromBase64String(result);
        Assert.NotEmpty(decoded);
    }

    // -------------------------------------------------------------------------
    // Decrypt – boundary / guard cases
    // -------------------------------------------------------------------------

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

    [Theory]
    [InlineData("not-base64!!")]
    [InlineData("plaintext")]
    [InlineData("some old legacy password stored before encryption was added")]
    public void Decrypt_WhenValueIsLegacyPlainText_ReturnsSameValue(string legacyValue)
    {
        // Arrange – legacy plain-text values that were never encrypted with DPAPI
        // Act
        var result = _sut.Decrypt(legacyValue);

        // Assert – fallback: original value returned unchanged
        Assert.Equal(legacyValue, result);
    }

    // -------------------------------------------------------------------------
    // Round-trip: Encrypt → Decrypt
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("MySecretP@ssw0rd!")]
    [InlineData("password123")]
    [InlineData("p")]
    [InlineData("unicode: áéíóú ñ 日本語")]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPassword(string originalPassword)
    {
        // Act
        var encrypted = _sut.Encrypt(originalPassword);
        var decrypted = _sut.Decrypt(encrypted);

        // Assert
        Assert.Equal(originalPassword, decrypted);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_WhenPasswordHasSpecialSqlCharacters_ReturnsOriginalPassword()
    {
        // Arrange – passwords with SQL-sensitive characters that could cause issues if stored unescaped
        const string password = "P@ss'; DROP TABLE Connections;--";

        // Act
        var encrypted = _sut.Encrypt(password);
        var decrypted = _sut.Decrypt(encrypted);

        // Assert
        Assert.Equal(password, decrypted);
    }

    [Fact]
    public void Encrypt_CalledTwiceWithSameInput_ReturnsDifferentCipherTexts()
    {
        // DPAPI uses random salt, so each call produces a unique cipher text
        const string password = "MySecretP@ssw0rd!";

        // Act
        var first = _sut.Encrypt(password);
        var second = _sut.Encrypt(password);

        // Assert
        Assert.NotEqual(first, second);
    }
}
