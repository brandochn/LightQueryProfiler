namespace LightQueryProfiler.Shared.Services.Interfaces
{
    /// <summary>
    /// Provides methods to encrypt and decrypt sensitive data such as connection passwords.
    /// </summary>
    public interface IPasswordProtectionService
    {
        /// <summary>
        /// Encrypts a plain-text password and returns a Base64-encoded cipher string.
        /// Returns <c>null</c> or empty when <paramref name="plainText"/> is <c>null</c> or empty.
        /// </summary>
        /// <param name="plainText">The plain-text password to encrypt.</param>
        /// <returns>A Base64-encoded encrypted string, or the original value when it is null or empty.</returns>
        string? Encrypt(string? plainText);

        /// <summary>
        /// Decrypts a Base64-encoded cipher string and returns the original plain-text password.
        /// Falls back to returning the original value when decryption fails (e.g. legacy plain-text entries).
        /// Returns <c>null</c> or empty when <paramref name="cipherText"/> is <c>null</c> or empty.
        /// </summary>
        /// <param name="cipherText">The Base64-encoded encrypted password to decrypt.</param>
        /// <returns>The plain-text password, or the original value if decryption fails.</returns>
        string? Decrypt(string? cipherText);
    }
}
