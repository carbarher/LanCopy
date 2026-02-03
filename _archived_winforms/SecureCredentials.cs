using System;
using System.Security.Cryptography;
using System.Text;

namespace SlskDown
{
    /// <summary>
    /// GestiÃ³n segura de credenciales usando DPAPI de Windows
    /// </summary>
    public static class SecureCredentials
    {
        /// <summary>
        /// Encripta un password usando DPAPI (Data Protection API)
        /// Solo puede ser desencriptado por el mismo usuario en la misma mÃ¡quina
        /// </summary>
        public static string EncryptPassword(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword))
                return string.Empty;

            try
            {
                var data = Encoding.UTF8.GetBytes(plainPassword);
                var encrypted = ProtectedData.Protect(
                    data,
                    null, // No salt adicional
                    DataProtectionScope.CurrentUser // Solo este usuario
                );
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error encriptando password: {ex.Message}");
                return plainPassword; // Fallback a texto plano
            }
        }

        /// <summary>
        /// Desencripta un password encriptado con DPAPI
        /// </summary>
        public static string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
                return string.Empty;

            try
            {
                var data = Convert.FromBase64String(encryptedPassword);
                var decrypted = ProtectedData.Unprotect(
                    data,
                    null,
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (FormatException)
            {
                // No estÃ¡ en Base64, probablemente es texto plano (migraciÃ³n)
                return encryptedPassword;
            }
            catch (CryptographicException)
            {
                // No puede desencriptar, probablemente es texto plano
                return encryptedPassword;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error desencriptando password: {ex.Message}");
                return encryptedPassword;
            }
        }

        /// <summary>
        /// Verifica si un string estÃ¡ encriptado (formato Base64 vÃ¡lido)
        /// </summary>
        public static bool IsEncrypted(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            try
            {
                Convert.FromBase64String(password);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Migra un password de texto plano a encriptado
        /// </summary>
        public static string MigrateToEncrypted(string password)
        {
            if (IsEncrypted(password))
                return password; // Ya estÃ¡ encriptado

            return EncryptPassword(password);
        }
    }
}

