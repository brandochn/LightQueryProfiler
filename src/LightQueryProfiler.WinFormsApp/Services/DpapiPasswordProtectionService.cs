using LightQueryProfiler.Shared.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace LightQueryProfiler.WinFormsApp.Services
{
    /// <summary>
    /// Encrypts and decrypts passwords using the Windows Data Protection API (DPAPI)
    /// with <see cref="DataProtectionScope.CurrentUser"/> scope.
    /// <para>
    /// Encrypted values are stored as Base64 strings so they can be persisted in SQLite.
    /// Decryption gracefully falls back to returning the original value when the input
    /// was not encrypted (e.g. legacy plain-text entries stored before this fix).
    /// </para>
    /// </summary>
    public sealed class DpapiPasswordProtectionService : IPasswordProtectionService
    {
        /// <summary>
        /// Encrypts a plain-text password using DPAPI and returns a Base64-encoded cipher string.
        /// </summary>
        /// <param name="plainText">The plain-text password to encrypt.</param>
        /// <returns>
        /// A Base64-encoded encrypted string, or <c>null</c> / empty when
        /// <paramref name="plainText"/> is <c>null</c> or empty.
        /// </returns>
        public string? Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts a Base64-encoded DPAPI cipher string and returns the original plain-text password.
        /// </summary>
        /// <param name="cipherText">The Base64-encoded encrypted password to decrypt.</param>
        /// <returns>
        /// The decrypted plain-text password. When decryption fails (e.g. the value is a
        /// legacy plain-text entry), the original <paramref name="cipherText"/> is returned
        /// unchanged so that existing connections continue to work without data migration.
        /// Returns <c>null</c> or empty when <paramref name="cipherText"/> is <c>null</c> or empty.
        /// </returns>
        public string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return cipherText;
            }

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                // The value was not encrypted with DPAPI (legacy plain-text entry).
                // Return the original value so existing connections remain usable.
                return cipherText;
            }
        }
    }
}
