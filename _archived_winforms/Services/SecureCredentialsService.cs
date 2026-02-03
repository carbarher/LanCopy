using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SlskDown.Services
{
    /// <summary>
    /// Secure credential storage using Windows Credential Manager with DPAPI fallback.
    /// Provides encrypted storage for sensitive credentials without hardcoding in source.
    /// </summary>
    public class SecureCredentialsService
    {
        private const string CredentialTarget = "SlskDown_SoulseekCredentials";
        private const string FallbackFile = "credentials.dat";
        private readonly string dataDir;
        private readonly bool portableMode;

        public SecureCredentialsService(string dataDirectory, bool portable = false)
        {
            dataDir = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
            portableMode = portable;

            if (!System.IO.Directory.Exists(dataDir))
            {
                System.IO.Directory.CreateDirectory(dataDir);
            }
        }

        /// <summary>
        /// Saves credentials securely using Windows Credential Manager or DPAPI fallback.
        /// </summary>
        public bool SaveCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Username and password cannot be empty");
            }

            try
            {
                if (!portableMode && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Use Windows Credential Manager (most secure)
                    return SaveToCredentialManager(username, password);
                }
                else
                {
                    // Fallback to DPAPI encrypted file (portable mode or non-Windows)
                    return SaveToEncryptedFile(username, password);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving credentials: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads credentials from secure storage.
        /// Returns (null, null) if no credentials are stored.
        /// </summary>
        public (string username, string password)? LoadCredentials()
        {
            try
            {
                if (!portableMode && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return LoadFromCredentialManager();
                }
                else
                {
                    return LoadFromEncryptedFile();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading credentials: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears stored credentials from all storage locations.
        /// </summary>
        public void ClearCredentials()
        {
            try
            {
                if (!portableMode && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    DeleteFromCredentialManager();
                }

                // Also delete encrypted file if it exists
                var filePath = System.IO.Path.Combine(dataDir, FallbackFile);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing credentials: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if credentials are stored.
        /// </summary>
        public bool HasSavedCredentials()
        {
            try
            {
                var creds = LoadCredentials();
                return creds.HasValue && 
                       !string.IsNullOrWhiteSpace(creds.Value.username) && 
                       !string.IsNullOrWhiteSpace(creds.Value.password);
            }
            catch
            {
                return false;
            }
        }

        #region Windows Credential Manager

        private bool SaveToCredentialManager(string username, string password)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var credentialBlobPtr = Marshal.AllocHGlobal(passwordBytes.Length);
            
            try
            {
                Marshal.Copy(passwordBytes, 0, credentialBlobPtr, passwordBytes.Length);
                
                var credential = new NativeMethods.CREDENTIAL
                {
                    Type = NativeMethods.CRED_TYPE_GENERIC,
                    TargetName = CredentialTarget,
                    UserName = username,
                    CredentialBlob = credentialBlobPtr,
                    CredentialBlobSize = passwordBytes.Length,
                    Persist = NativeMethods.CRED_PERSIST_LOCAL_MACHINE,
                    AttributeCount = 0,
                    Attributes = IntPtr.Zero,
                    Comment = "SlskDown Soulseek Credentials"
                };

                return NativeMethods.CredWrite(ref credential, 0);
            }
            finally
            {
                if (credentialBlobPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(credentialBlobPtr);
            }
        }

        private (string username, string password)? LoadFromCredentialManager()
        {
            if (NativeMethods.CredRead(CredentialTarget, NativeMethods.CRED_TYPE_GENERIC, 0, out var credPtr))
            {
                try
                {
                    var credential = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(credPtr);
                    var passwordBytes = new byte[credential.CredentialBlobSize];
                    Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, credential.CredentialBlobSize);
                    var password = Encoding.UTF8.GetString(passwordBytes);
                    return (credential.UserName, password);
                }
                finally
                {
                    NativeMethods.CredFree(credPtr);
                }
            }

            return null;
        }

        private void DeleteFromCredentialManager()
        {
            NativeMethods.CredDelete(CredentialTarget, NativeMethods.CRED_TYPE_GENERIC, 0);
        }

        #endregion

        #region DPAPI Encrypted File (Fallback)

        private bool SaveToEncryptedFile(string username, string password)
        {
            var data = $"{username}|{password}";
            var bytes = Encoding.UTF8.GetBytes(data);
            
            byte[] encrypted;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use DPAPI on Windows
                encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                // Basic encryption for non-Windows (less secure, but better than plaintext)
                encrypted = SimpleEncrypt(bytes);
            }

            var filePath = System.IO.Path.Combine(dataDir, FallbackFile);
            System.IO.File.WriteAllBytes(filePath, encrypted);
            return true;
        }

        private (string username, string password)? LoadFromEncryptedFile()
        {
            var filePath = System.IO.Path.Combine(dataDir, FallbackFile);
            if (!System.IO.File.Exists(filePath))
            {
                return null;
            }

            var encrypted = System.IO.File.ReadAllBytes(filePath);
            
            byte[] decrypted;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                decrypted = SimpleDecrypt(encrypted);
            }

            var data = Encoding.UTF8.GetString(decrypted);
            var parts = data.Split('|');
            
            if (parts.Length == 2)
            {
                return (parts[0], parts[1]);
            }

            return null;
        }

        #endregion

        #region Simple Encryption (Non-Windows fallback)

        private byte[] SimpleEncrypt(byte[] data)
        {
            // Simple XOR encryption with machine-specific key
            // NOTE: This is NOT cryptographically secure, just obfuscation
            // Only used as last resort on non-Windows systems
            var key = GetMachineKey();
            var result = new byte[data.Length];
            
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(data[i] ^ key[i % key.Length]);
            }
            
            return result;
        }

        private byte[] SimpleDecrypt(byte[] data)
        {
            // XOR is symmetric
            return SimpleEncrypt(data);
        }

        private byte[] GetMachineKey()
        {
            // Generate a machine-specific key
            var machineId = Environment.MachineName + Environment.UserName;
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
            }
        }

        #endregion

        #region Native Methods (Windows Credential Manager)

        private static class NativeMethods
        {
            public const int CRED_TYPE_GENERIC = 1;
            public const int CRED_PERSIST_LOCAL_MACHINE = 2;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct CREDENTIAL
            {
                public int Flags;
                public int Type;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string TargetName;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string Comment;
                public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
                public int CredentialBlobSize;
                public IntPtr CredentialBlob;
                public int Persist;
                public int AttributeCount;
                public IntPtr Attributes;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string TargetAlias;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string UserName;
            }

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] int flags);

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredDelete(string target, int type, int flags);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern void CredFree([In] IntPtr credential);
        }

        #endregion
    }
}
