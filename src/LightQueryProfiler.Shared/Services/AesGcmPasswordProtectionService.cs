using LightQueryProfiler.Shared.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace LightQueryProfiler.Shared.Services
{
    /// <summary>
    /// Cross-platform password encryption service using AES-256-GCM.
    /// The encryption key is derived per user and machine using PBKDF2-SHA256,
    /// mirroring the scope of Windows DPAPI CurrentUser without platform dependency.
    /// </summary>
    /// <remarks>
    /// Storage format (Base64-encoded): [ nonce (12 bytes) | tag (16 bytes) | ciphertext ]
    /// </remarks>
    public class AesGcmPasswordProtectionService : IPasswordProtectionService
    {
        private const string AppConstant = "LightQueryProfiler_v1";
        private const int NonceSizeBytes = 12;
        private const int TagSizeBytes = 16;
        private const int KeySizeBytes = 32;
        private const int Pbkdf2Iterations = 100_000;

        /// <inheritdoc />
        public string? Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            var key = DeriveKey();
            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSizeBytes];

            using var aesGcm = new AesGcm(key, TagSizeBytes);
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // Concatenate: nonce (12) + tag (16) + ciphertext
            var combined = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, combined, 0, NonceSizeBytes);
            Buffer.BlockCopy(tag, 0, combined, NonceSizeBytes, TagSizeBytes);
            Buffer.BlockCopy(cipherBytes, 0, combined, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);

            return Convert.ToBase64String(combined);
        }

        /// <inheritdoc />
        public string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return cipherText;
            }

            try
            {
                var combined = Convert.FromBase64String(cipherText);

                // Minimum: 12 (nonce) + 16 (tag) = 28 bytes
                if (combined.Length < NonceSizeBytes + TagSizeBytes)
                {
                    return cipherText;
                }

                var nonce = combined[..NonceSizeBytes];
                var tag = combined[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
                var cipherBytes = combined[(NonceSizeBytes + TagSizeBytes)..];

                var key = DeriveKey();
                var plainBytes = new byte[cipherBytes.Length];

                using var aesGcm = new AesGcm(key, TagSizeBytes);
                aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (FormatException)
            {
                // Not a valid Base64 string — treat as legacy plain-text
                return cipherText;
            }
            catch (CryptographicException)
            {
                // Decryption failed (wrong key, corrupted data) — return original as fallback
                return cipherText;
            }
        }

        /// <summary>
        /// Derives a 256-bit AES key scoped to the current OS user and machine
        /// using PBKDF2-SHA256 with 100,000 iterations.
        /// </summary>
        private static byte[] DeriveKey()
        {
            var password = $"{AppConstant}:{Environment.UserName}";
            var saltSource = Encoding.UTF8.GetBytes(Environment.MachineName);
            var salt = SHA256.HashData(saltSource);

            return Rfc2898DeriveBytes.Pbkdf2(
                password: Encoding.UTF8.GetBytes(password),
                salt: salt,
                iterations: Pbkdf2Iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: KeySizeBytes);
        }
    }
}
