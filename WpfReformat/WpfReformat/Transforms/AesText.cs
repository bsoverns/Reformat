using System.Security.Cryptography;
using System.Text;

namespace WpfReformat.Transforms
{
    /// <summary>
    /// Two AES formats:
    /// <list type="bullet">
    /// <item><b>Legacy</b> (AES-256-CBC, password padded with a built-in key, fixed IV). Kept byte-for-byte
    /// compatible with the WinForms version so existing ciphertext still decrypts. Weak: identical inputs
    /// produce identical output and the key material is in the source code.</item>
    /// <item><b>Strong</b> (AES-256-GCM, PBKDF2-SHA256 key, random salt and nonce, authenticated).
    /// Output is <c>RF2:</c> + Base64(salt | nonce | tag | ciphertext).</item>
    /// </list>
    /// </summary>
    public static class AesText
    {
        public const int LegacyMaxPasswordLength = 32;
        public const string StrongPrefix = "RF2:";

        private const string LegacyKey = "alio1j304klsk49fnhcvlslwie8tgxzj"; // 32
        private const string LegacyIV = "fjdkwnsksdjklfsk";                 // 16

        private const int SaltSize = 16;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int Iterations = 600_000;

        // ---- Legacy (compatible with WinForms Reformat) ----

        private static Aes CreateLegacyAes(string password)
        {
            if (password.Length > LegacyMaxPasswordLength)
                throw new InvalidOperationException($"The password is too long. It must be {LegacyMaxPasswordLength} characters or fewer.");

            var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Encoding.UTF8.GetBytes(password + LegacyKey[password.Length..]);
            aes.IV = Encoding.UTF8.GetBytes(LegacyIV);
            return aes;
        }

        public static string EncryptLegacy(string plaintext, string password)
        {
            using var aes = CreateLegacyAes(password);
            var pt = Encoding.UTF8.GetBytes(plaintext);
            return Convert.ToBase64String(aes.EncryptCbc(pt, aes.IV));
        }

        public static string DecryptLegacy(string base64Cipher, string password)
        {
            using var aes = CreateLegacyAes(password);
            try
            {
                var ct = Convert.FromBase64String(base64Cipher.Trim());
                return Encoding.UTF8.GetString(aes.DecryptCbc(ct, aes.IV));
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                throw new InvalidOperationException("Decryption failed. The password is wrong or the input is not Reformat ciphertext.");
            }
        }

        // ---- Strong (AES-GCM + PBKDF2) ----

        private static byte[] DeriveKey(string password, byte[] salt) =>
            Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);

        public static string EncryptStrong(string plaintext, string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("A password is required.");

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var pt = Encoding.UTF8.GetBytes(plaintext);
            var ct = new byte[pt.Length];
            var tag = new byte[TagSize];

            using (var gcm = new AesGcm(DeriveKey(password, salt), TagSize))
                gcm.Encrypt(nonce, pt, ct, tag);

            return StrongPrefix + Convert.ToBase64String([.. salt, .. nonce, .. tag, .. ct]);
        }

        public static string DecryptStrong(string payload, string password)
        {
            try
            {
                var data = Convert.FromBase64String(payload.Trim()[StrongPrefix.Length..]);
                if (data.Length < SaltSize + NonceSize + TagSize)
                    throw new FormatException();

                var salt = data.AsSpan(0, SaltSize);
                var nonce = data.AsSpan(SaltSize, NonceSize);
                var tag = data.AsSpan(SaltSize + NonceSize, TagSize);
                var ct = data.AsSpan(SaltSize + NonceSize + TagSize);
                var pt = new byte[ct.Length];

                using var gcm = new AesGcm(DeriveKey(password, salt.ToArray()), TagSize);
                gcm.Decrypt(nonce, ct, tag, pt);
                return Encoding.UTF8.GetString(pt);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                throw new InvalidOperationException("Decryption failed. The password is wrong or the data was modified.");
            }
        }

        /// <summary>Decrypt either format, detected by the <c>RF2:</c> prefix.</summary>
        public static string Decrypt(string input, string password) =>
            input.TrimStart().StartsWith(StrongPrefix, StringComparison.Ordinal)
                ? DecryptStrong(input.TrimStart(), password)
                : DecryptLegacy(input, password);
    }
}
