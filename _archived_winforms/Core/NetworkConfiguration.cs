using System;
using System.IO;
using System.Text.Json;

namespace SlskDown.Core
{
    /// <summary>
    /// Configuración de red Soulseek
    /// </summary>
    public class NetworkConfiguration
    {
        public bool SoulseekEnabled { get; set; } = true;
        
        // Configuración Soulseek
        public string SoulseekUsername { get; set; } = "";
        public string SoulseekPassword { get; set; } = "";
        public bool SoulseekAutoConnect { get; set; } = false;
        
        
        // Configuración de búsqueda
        public int SearchTimeoutSeconds { get; set; } = 30;
        public bool UseCache { get; set; } = true;
        public int CacheExpirationMinutes { get; set; } = 30;

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlskDown",
            "network_config.json"
        );

        /// <summary>
        /// Carga la configuración desde archivo
        /// </summary>
        public static NetworkConfiguration Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<NetworkConfiguration>(json) 
                                 ?? new NetworkConfiguration();
                    
                    return config;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading network config: {ex.Message}");
            }

            return new NetworkConfiguration();
        }

        /// <summary>
        /// Guarda la configuración en archivo
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving network config: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene las redes activas según configuración
        /// </summary>
        public string[] GetActiveNetworks()
        {
            var networks = new System.Collections.Generic.List<string>();
            
            if (SoulseekEnabled)
                networks.Add("Soulseek");
            
            return networks.ToArray();
        }

        /// <summary>
        /// Verifica si hay al menos una red habilitada
        /// </summary>
        public bool HasActiveNetworks()
        {
            return SoulseekEnabled;
        }

        /// <summary>
        /// Obtiene descripción del modo actual
        /// </summary>
        public string GetModeDescription()
        {
            var networks = new System.Collections.Generic.List<string>();
            
            if (SoulseekEnabled)
                return "🔵 Soulseek";
            
            return "Ninguna red habilitada";
        }

        /// <summary>
        /// Valida la configuración
        /// </summary>
        public (bool isValid, string error) Validate()
        {
            if (!HasActiveNetworks())
                return (false, "Debe habilitar Soulseek");

            if (SoulseekEnabled && string.IsNullOrWhiteSpace(SoulseekUsername))
                return (false, "Soulseek requiere un nombre de usuario");

            return (true, "");
        }
    }
}
