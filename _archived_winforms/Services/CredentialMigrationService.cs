using System;
using System.IO;
using System.Text.Json;

namespace SlskDown.Services
{
    /// <summary>
    /// Handles migration of credentials from old config.json to secure storage.
    /// </summary>
    public class CredentialMigrationService
    {
        private readonly SecureCredentialsService secureCredentials;
        private readonly string configPath;

        public CredentialMigrationService(SecureCredentialsService credentialService, string configFilePath)
        {
            secureCredentials = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            configPath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));
        }

        /// <summary>
        /// Attempts to migrate credentials from old config.json format.
        /// Returns true if migration was successful or if already migrated.
        /// </summary>
        public bool MigrateFromConfig()
        {
            try
            {
                // Check if already migrated
                if (secureCredentials.HasSavedCredentials())
                {
                    return true;
                }

                // Check if config file exists
                if (!File.Exists(configPath))
                {
                    return false;
                }

                // Read config file
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Extract credentials
                if (root.TryGetProperty("username", out var usernameElement) &&
                    root.TryGetProperty("password", out var passwordElement))
                {
                    var username = usernameElement.GetString();
                    var password = passwordElement.GetString();

                    // Only migrate if both are present
                    if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                    {
                        // Save to secure storage
                        if (secureCredentials.SaveCredentials(username, password))
                        {
                            // Migration successful - optionally remove from config
                            RemoveCredentialsFromConfig();
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error migrating credentials: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes plaintext credentials from config.json for security.
        /// Keeps a backup just in case.
        /// </summary>
        private void RemoveCredentialsFromConfig()
        {
            try
            {
                if (!File.Exists(configPath))
                    return;

                // Create backup
                var backupPath = configPath + ".backup";
                File.Copy(configPath, backupPath, true);

                // Read config
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                
                // Create new config without credentials
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        // Skip username and password
                        if (property.Name == "username" || property.Name == "password")
                            continue;
                            
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                    
                    writer.WriteEndObject();
                }

                // Write updated config
                var updatedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(configPath, updatedJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing credentials from config: {ex.Message}");
                // Don't throw - migration already succeeded
            }
        }

        /// <summary>
        /// Check if credentials need to be prompted from user.
        /// </summary>
        public bool NeedsCredentialPrompt()
        {
            return !secureCredentials.HasSavedCredentials();
        }
    }
}
