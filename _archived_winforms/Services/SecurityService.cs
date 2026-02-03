using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SlskDown.Services
{
    /// <summary>
    /// ImplementaciÃ³n del servicio de seguridad usando DPAPI
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private static readonly string[] DangerousPatterns = new[]
        {
            "<script", "</script>", "javascript:", "eval(", "onclick=",
            "../", "..\\", "<", ">", "DROP TABLE", "DELETE FROM"
        };

        /// <summary>
        /// Encripta datos usando DPAPI (Data Protection API) de Windows
        /// Los datos solo pueden ser desencriptados por el mismo usuario en la misma mÃ¡quina
        /// </summary>
        public byte[] Protect(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentNullException(nameof(data));

            try
            {
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                return ProtectedData.Protect(
                    dataBytes,
                    null, // Entropy opcional
                    DataProtectionScope.CurrentUser
                );
            }
            catch (Exception ex)
            {
                throw new CryptographicException("Error al encriptar datos", ex);
            }
        }

        /// <summary>
        /// Desencripta datos usando DPAPI
        /// </summary>
        public string Unprotect(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                throw new ArgumentNullException(nameof(encryptedData));

            try
            {
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedData,
                    null,
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                throw new CryptographicException("Error al desencriptar datos", ex);
            }
        }

        /// <summary>
        /// Valida que una query de bÃºsqueda sea segura
        /// </summary>
        public bool ValidateSearchQuery(string query, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(query))
            {
                errorMessage = "La bÃºsqueda no puede estar vacÃ­a";
                return false;
            }

            if (query.Length > 500)
            {
                errorMessage = "La bÃºsqueda es demasiado larga (mÃ¡ximo 500 caracteres)";
                return false;
            }

            // Verificar patrones peligrosos
            foreach (var pattern in DangerousPatterns)
            {
                if (query.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"La bÃºsqueda contiene caracteres no permitidos: {pattern}";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Sanitiza un path de archivo removiendo caracteres peligrosos
        /// </summary>
        public string SanitizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            // Remover caracteres invÃ¡lidos
            var invalidChars = Path.GetInvalidPathChars()
                .Concat(Path.GetInvalidFileNameChars())
                .Distinct()
                .ToArray();

            var sanitized = new StringBuilder(path.Length);
            foreach (char c in path)
            {
                if (!invalidChars.Contains(c))
                    sanitized.Append(c);
            }

            // Prevenir path traversal
            var result = sanitized.ToString()
                .Replace("../", "")
                .Replace("..\\", "")
                .Replace("..", "");

            return result;
        }
    }
}

