using System;

namespace SlskDown.Services
{
    /// <summary>
    /// Servicio para gestiÃ³n de configuraciÃ³n con credenciales encriptadas
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Carga la configuraciÃ³n desde disco
        /// </summary>
        AppConfig LoadConfig();
        
        /// <summary>
        /// Guarda la configuraciÃ³n en disco (encriptando credenciales)
        /// </summary>
        void SaveConfig(AppConfig config);
        
        /// <summary>
        /// Obtiene las credenciales desencriptadas
        /// </summary>
        (string username, string password) GetCredentials();
        
        /// <summary>
        /// Guarda las credenciales encriptadas
        /// </summary>
        void SaveCredentials(string username, string password);

        void ClearCredentials();
    }

    /// <summary>
    /// ConfiguraciÃ³n de la aplicaciÃ³n
    /// </summary>
    public class AppConfig
    {
        public string DownloadDirectory { get; set; } = @"c:\p2p\downloads";
        public int SearchTimeout { get; set; } = 450;
        public int ResponseLimit { get; set; } = 50;
        public int FileLimit { get; set; } = 1000;
        public bool AutoConnect { get; set; } = true;
        
        // Credenciales encriptadas (no en texto plano)
        public byte[]? EncryptedUsername { get; set; }
        public byte[]? EncryptedPassword { get; set; }
    }
}

