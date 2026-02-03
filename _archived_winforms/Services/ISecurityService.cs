using System;

namespace SlskDown.Services
{
    /// <summary>
    /// Servicio para encriptaciÃ³n y seguridad de credenciales
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// Encripta datos usando DPAPI de Windows
        /// </summary>
        byte[] Protect(string data);
        
        /// <summary>
        /// Desencripta datos usando DPAPI de Windows
        /// </summary>
        string Unprotect(byte[] encryptedData);
        
        /// <summary>
        /// Valida que una query de bÃºsqueda sea segura
        /// </summary>
        bool ValidateSearchQuery(string query, out string? errorMessage);
        
        /// <summary>
        /// Sanitiza un path de archivo
        /// </summary>
        string SanitizePath(string path);
    }
}

