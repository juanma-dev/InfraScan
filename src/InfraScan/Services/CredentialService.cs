using System;
using System.Security.Cryptography;
using System.Text;

namespace InfraScan.Services
{
    public static class CredentialService
    {
        /// <summary>
        /// Encrypts a password using Windows DPAPI (CurrentUser scope).
        /// The result is Base64-encoded for JSON storage.
        /// </summary>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser
            );
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts a DPAPI-encrypted Base64 string back to plain text.
        /// </summary>
        public static string Decrypt(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                return string.Empty;
            }
        }
    }
}
