using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace DevDeck.Core
{
    /// <summary>
    ///  Wraps and unwraps small secrets, tied to the signed-in user.
    /// </summary>
    /// <remarks>
    ///  The V2 original was DPAPI and nothing else. DPAPI has no equivalent on macOS or Linux, so
    ///  this keeps DPAPI where it exists and falls back to AES-GCM under a key file that only the
    ///  owning user can read.
    ///
    ///  The Windows blob format is unchanged from V2, so a settings.json written by either version
    ///  is readable by the other. AES blobs carry a "v1:" prefix, which a DPAPI blob never has.
    ///
    ///  This protects a settings file that is copied off the machine, or read by a different user
    ///  on it. It does not protect against code already running as this user - nothing local can.
    ///
    ///  TODO(port): the stronger answer on Unix is the platform secret store - Keychain via
    ///  `security`, Secret Service via `secret-tool` - which would hold the secret itself and leave
    ///  only a handle in settings.json. That changes the shape of this API, so it is not done here.
    /// </remarks>
    internal static class DataProtection
    {
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(
            ref DataBlob input, string? description, IntPtr entropy, IntPtr reserved,
            IntPtr prompt, uint flags, out DataBlob output);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(
            ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved,
            IntPtr prompt, uint flags, out DataBlob output);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Length;
            public IntPtr Data;
        }

        /// <summary>No UI, ever - this runs while settings are being saved.</summary>
        private const uint UiForbidden = 0x1;

        /// <summary>Marks a blob this class encrypted itself, rather than one DPAPI produced.</summary>
        private const string AesPrefix = "v1:";

        private const int NonceLength = 12;
        private const int TagLength = 16;

        /// <summary>Returns a base64 blob, or an empty string for empty input.</summary>
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain))
            {
                return string.Empty;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(plain);

                return OperatingSystem.IsWindows()
                    ? Convert.ToBase64String(Transform(bytes, protect: true))
                    : AesPrefix + Convert.ToBase64String(Encrypt(bytes));
            }
            catch (Exception)
            {
                // Better to lose the stored key and have it retyped than to write it in the clear.
                return string.Empty;
            }
        }

        /// <summary>Reverses <see cref="Protect"/>, returning empty for anything unreadable.</summary>
        public static string Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue))
            {
                return string.Empty;
            }

            try
            {
                // Read the format from the value, not from the running OS: a settings file moved
                // between machines should fail the decrypt, not be misread as the wrong format.
                if (protectedValue.StartsWith(AesPrefix, StringComparison.Ordinal))
                {
                    return Encoding.UTF8.GetString(
                        Decrypt(Convert.FromBase64String(protectedValue[AesPrefix.Length..])));
                }

                return OperatingSystem.IsWindows()
                    ? Encoding.UTF8.GetString(Transform(Convert.FromBase64String(protectedValue), protect: false))
                    : string.Empty;
            }
            catch (Exception)
            {
                // Settings hand-edited, or copied from another machine or user account.
                return string.Empty;
            }
        }

        private static byte[] Encrypt(byte[] input)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceLength);
            byte[] cipher = new byte[input.Length];
            byte[] tag = new byte[TagLength];

            using (AesGcm aes = new(UserKey(), TagLength))
            {
                aes.Encrypt(nonce, input, cipher, tag);
            }

            byte[] result = new byte[NonceLength + TagLength + cipher.Length];
            nonce.CopyTo(result, 0);
            tag.CopyTo(result, NonceLength);
            cipher.CopyTo(result, NonceLength + TagLength);
            return result;
        }

        private static byte[] Decrypt(byte[] blob)
        {
            if (blob.Length < NonceLength + TagLength)
            {
                throw new InvalidOperationException("Blob is too short to be one of ours.");
            }

            byte[] plain = new byte[blob.Length - NonceLength - TagLength];

            using AesGcm aes = new(UserKey(), TagLength);
            aes.Decrypt(
                blob.AsSpan(0, NonceLength),
                blob.AsSpan(NonceLength + TagLength),
                blob.AsSpan(NonceLength, TagLength),
                plain);

            return plain;
        }

        /// <summary>
        ///  The per-user key, generated on first use. The file mode is set on the empty file,
        ///  before any key bytes reach it, rather than after.
        /// </summary>
        private static byte[] UserKey()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevDeck");
            string path = Path.Combine(dir, "secret.key");

            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }

            Directory.CreateDirectory(dir);
            byte[] key = RandomNumberGenerator.GetBytes(32);

            using (FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                stream.Write(key);
            }

            return key;
        }

        private static byte[] Transform(byte[] input, bool protect)
        {
            IntPtr buffer = Marshal.AllocHGlobal(input.Length);
            DataBlob output = default;

            try
            {
                Marshal.Copy(input, 0, buffer, input.Length);
                DataBlob source = new() { Length = input.Length, Data = buffer };

                bool ok = protect
                    ? CryptProtectData(ref source, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
                    : CryptUnprotectData(ref source, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output);

                if (!ok)
                {
                    throw new InvalidOperationException($"DPAPI failed ({Marshal.GetLastWin32Error()}).");
                }

                byte[] result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, output.Length);
                return result;
            }
            finally
            {
                if (output.Data != IntPtr.Zero)
                {
                    LocalFree(output.Data);
                }

                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
