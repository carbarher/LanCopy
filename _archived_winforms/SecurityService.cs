using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace SlskDown
{
    /// <summary>
    /// Servicio de seguridad para encriptaciÃ³n de credenciales con DPAPI
    /// </summary>
    public class SecurityService
    {
        private static readonly string Entropy = "SlskDown_Security_Key_2025";
        
        /// <summary>
        /// Estructura para credenciales encriptadas
        /// </summary>
        public struct EncryptedCredentials
        {
            public string EncryptedUsername { get; set; }
            public string EncryptedPassword { get; set; }
            public DateTime CreatedAt { get; set; }
            public string MachineId { get; set; }
            public bool IsEncrypted { get; set; }
        }
        
        /// <summary>
        /// Encriptar credenciales usando DPAPI
        /// </summary>
        public static EncryptedCredentials EncryptCredentials(string username, string password)
        {
            try
            {
                Console.WriteLine("[SecurityService] ðŸ” Encriptando credenciales con DPAPI");
                
                var encryptedUsername = EncryptString(username);
                var encryptedPassword = EncryptString(password);
                
                var credentials = new EncryptedCredentials
                {
                    EncryptedUsername = Convert.ToBase64String(encryptedUsername),
                    EncryptedPassword = Convert.ToBase64String(encryptedPassword),
                    CreatedAt = DateTime.Now,
                    MachineId = GetMachineId(),
                    IsEncrypted = true
                };
                
                Console.WriteLine("[SecurityService] âœ… Credenciales encriptadas exitosamente");
                return credentials;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecurityService] âŒ Error encriptando credenciales: {ex.Message}");
                
                // Fallback: guardar sin encriptar pero marcar como no encriptado
                return new EncryptedCredentials
                {
                    EncryptedUsername = username,
                    EncryptedPassword = password,
                    CreatedAt = DateTime.Now,
                    MachineId = GetMachineId(),
                    IsEncrypted = false
                };
            }
        }
        
        /// <summary>
        /// Desencriptar credenciales usando DPAPI
        /// </summary>
        public static (string username, string password) DecryptCredentials(EncryptedCredentials credentials)
        {
            try
            {
                Console.WriteLine("[SecurityService] ðŸ”“ Desencriptando credenciales");
                
                if (!credentials.IsEncrypted)
                {
                    Console.WriteLine("[SecurityService] â„¹ï¸ Credenciales no encriptadas, retornando tal cual");
                    return (credentials.EncryptedUsername, credentials.EncryptedPassword);
                }
                
                // Verificar que coincida el ID de mÃ¡quina
                var currentMachineId = GetMachineId();
                if (credentials.MachineId != currentMachineId)
                {
                    Console.WriteLine("[SecurityService] âš ï¸ ID de mÃ¡quina diferente, credenciales invÃ¡lidas");
                    throw new UnauthorizedAccessException("Credenciales encriptadas para otra mÃ¡quina");
                }
                
                var usernameBytes = Convert.FromBase64String(credentials.EncryptedUsername);
                var passwordBytes = Convert.FromBase64String(credentials.EncryptedPassword);
                
                var username = DecryptString(usernameBytes);
                var password = DecryptString(passwordBytes);
                
                Console.WriteLine("[SecurityService] âœ… Credenciales desencriptadas exitosamente");
                return (username, password);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecurityService] âŒ Error desencriptando credenciales: {ex.Message}");
                throw new InvalidOperationException("No se pudieron desencriptar las credenciales", ex);
            }
        }
        
        /// <summary>
        /// Encriptar string usando DPAPI
        /// </summary>
        private static byte[] EncryptString(string plainText)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var entropyBytes = Encoding.UTF8.GetBytes(Entropy);
            
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                entropyBytes,
                DataProtectionScope.CurrentUser
            );
            
            return encryptedBytes;
        }
        
        /// <summary>
        /// Desencriptar string usando DPAPI
        /// </summary>
        private static string DecryptString(byte[] encryptedBytes)
        {
            var entropyBytes = Encoding.UTF8.GetBytes(Entropy);
            
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                entropyBytes,
                DataProtectionScope.CurrentUser
            );
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        
        /// <summary>
        /// Obtener ID Ãºnico de mÃ¡quina
        /// </summary>
        private static string GetMachineId()
        {
            try
            {
                // Usar mÃºltiples fuentes para crear ID Ãºnico
                var sources = new[]
                {
                    Environment.MachineName,
                    Environment.UserName,
                    Environment.OSVersion.ToString(),
                    GetHardwareId()
                };
                
                var combined = string.Join("|", sources);
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
                
                return Convert.ToBase64String(hash)[..16]; // Primeros 16 caracteres
            }
            catch
            {
                return Environment.MachineName + "_" + Environment.UserName;
            }
        }
        
        /// <summary>
        /// Obtener ID de hardware (fallback)
        /// </summary>
        private static string GetHardwareId()
        {
            try
            {
                // Intentar obtener ID del registro de Windows
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                
                return guid ?? "UNKNOWN_HARDWARE";
            }
            catch
            {
                return "UNKNOWN_HARDWARE";
            }
        }
        
        /// <summary>
        /// Guardar credenciales encriptadas en archivo
        /// </summary>
        public static void SaveEncryptedCredentials(string username, string password, string filePath)
        {
            try
            {
                var credentials = EncryptCredentials(username, password);
                var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(filePath, json);
                
                Console.WriteLine($"[SecurityService] ðŸ’¾ Credenciales encriptadas guardadas en: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecurityService] âŒ Error guardando credenciales: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Cargar y desencriptar credenciales desde archivo
        /// </summary>
        public static (string username, string password) LoadEncryptedCredentials(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine("[SecurityService] â„¹ï¸ Archivo de credenciales no encontrado");
                    return ("", "");
                }
                
                var json = File.ReadAllText(filePath);
                var credentials = JsonSerializer.Deserialize<EncryptedCredentials?>(json);
                
                if (!credentials.HasValue)
                {
                    Console.WriteLine("[SecurityService] âš ï¸ Archivo de credenciales vacÃ­o o invÃ¡lido");
                    return ("", "");
                }
                
                return DecryptCredentials(credentials.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecurityService] âŒ Error cargando credenciales: {ex.Message}");
                return ("", "");
            }
        }
        
        /// <summary>
        /// Verificar si las credenciales estÃ¡n encriptadas
        /// </summary>
        public static bool AreCredentialsEncrypted(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;
                
                var json = File.ReadAllText(filePath);
                var credentials = JsonSerializer.Deserialize<EncryptedCredentials?>(json);
                
                return credentials?.IsEncrypted ?? false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Migrar credenciales existentes a formato encriptado
        /// </summary>
        public static bool MigrateToEncrypted(string plainConfigPath, string encryptedConfigPath)
        {
            try
            {
                Console.WriteLine("[SecurityService] ðŸ”„ Migrando credenciales a formato encriptado");
                
                // Cargar configuraciÃ³n existente
                if (!File.Exists(plainConfigPath))
                {
                    Console.WriteLine("[SecurityService] â„¹ï¸ No existe configuraciÃ³n para migrar");
                    return false;
                }
                
                var json = File.ReadAllText(plainConfigPath);
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                
                if (config?.ContainsKey("username") == true && config?.ContainsKey("password") == true)
                {
                    var username = config["username"].ToString();
                    var password = config["password"].ToString();
                    
                    // Guardar en formato encriptado
                    SaveEncryptedCredentials(username, password, encryptedConfigPath);
                    
                    Console.WriteLine("[SecurityService] âœ… MigraciÃ³n a formato encriptado completada");
                    return true;
                }
                
                Console.WriteLine("[SecurityService] âš ï¸ No se encontraron credenciales para migrar");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecurityService] âŒ Error en migraciÃ³n: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Generar hash para verificaciÃ³n de integridad
        /// </summary>
        public static string GenerateHash(string data)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }
        
        /// <summary>
        /// Verificar integridad de datos
        /// </summary>
        public static bool VerifyIntegrity(string data, string expectedHash)
        {
            var actualHash = GenerateHash(data);
            return actualHash == expectedHash;
        }
    }
}

