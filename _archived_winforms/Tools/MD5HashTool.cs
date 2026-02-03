using System;
using System.Security.Cryptography;
using System.Text;

namespace SlskDown.Tools
{
    /// <summary>
    /// Herramienta para generar hashes MD5 de contraseñas de eMule
    /// </summary>
    public static class MD5HashTool
    {
        /// <summary>
        /// Genera el hash MD5 de una contraseña en formato hexadecimal
        /// </summary>
        public static string GenerateMD5Hex(string password)
        {
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(password);
                var hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Genera el hash MD5 de una contraseña como array de bytes
        /// </summary>
        public static byte[] GenerateMD5Bytes(string password)
        {
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(password);
                return md5.ComputeHash(inputBytes);
            }
        }

        /// <summary>
        /// Muestra el hash MD5 de una contraseña para debugging
        /// </summary>
        public static void ShowMD5Hash(string password)
        {
            var hexHash = GenerateMD5Hex(password);
            var bytesHash = GenerateMD5Bytes(password);
            
            Console.WriteLine($"Password: {password}");
            Console.WriteLine($"MD5 (hex): {hexHash}");
            Console.Write("MD5 (bytes): ");
            foreach (var b in bytesHash)
            {
                Console.Write($"{b:X2} ");
            }
            Console.WriteLine();
        }
    }
}
