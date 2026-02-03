using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SlskDown
{
    // Sistema de frases prohibidas (Banned Phrases)
    public class BannedPhrasesFilter
    {
        private HashSet<string> serverBannedPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> userBannedPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool enableBannedPhrases = true;
        private string bannedPhrasesPath;
        
        public BannedPhrasesFilter(string dataDir)
        {
            bannedPhrasesPath = Path.Combine(dataDir, "banned_phrases.json");
            LoadUserBannedPhrases();
        }
        
        public void LoadServerBannedPhrases(List<string> phrases)
        {
            serverBannedPhrases.Clear();
            foreach (var phrase in phrases)
            {
                serverBannedPhrases.Add(phrase.ToLower());
            }
        }
        
        public void AddUserBannedPhrase(string phrase)
        {
            userBannedPhrases.Add(phrase.ToLower());
            SaveUserBannedPhrases();
        }
        
        public void RemoveUserBannedPhrase(string phrase)
        {
            userBannedPhrases.Remove(phrase.ToLower());
            SaveUserBannedPhrases();
        }
        
        public bool IsFileBanned(string filename)
        {
            if (!enableBannedPhrases) return false;
            
            string filenameLower = filename.ToLower();
            
            // Verificar frases del servidor
            foreach (var phrase in serverBannedPhrases)
            {
                if (filenameLower.Contains(phrase))
                {
                    return true;
                }
            }
            
            // Verificar frases del usuario
            foreach (var phrase in userBannedPhrases)
            {
                if (filenameLower.Contains(phrase))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        public List<T> FilterResults<T>(List<T> results, Func<T, string> getFilename)
        {
            if (!enableBannedPhrases) return results;
            
            var filtered = results.Where(r => !IsFileBanned(getFilename(r))).ToList();
            
            int blocked = results.Count - filtered.Count;
            return filtered;
        }
        
        public int GetBlockedCount<T>(List<T> results, Func<T, string> getFilename)
        {
            if (!enableBannedPhrases) return 0;
            return results.Count(r => IsFileBanned(getFilename(r)));
        }
        
        public List<string> GetServerBannedPhrases()
        {
            return serverBannedPhrases.ToList();
        }
        
        public List<string> GetUserBannedPhrases()
        {
            return userBannedPhrases.ToList();
        }
        
        public void SetEnabled(bool enabled)
        {
            enableBannedPhrases = enabled;
        }
        
        private void LoadUserBannedPhrases()
        {
            try
            {
                if (File.Exists(bannedPhrasesPath))
                {
                    var json = File.ReadAllText(bannedPhrasesPath);
                    var phrases = JsonSerializer.Deserialize<List<string>>(json);
                    
                    if (phrases != null)
                    {
                        userBannedPhrases = new HashSet<string>(phrases, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch { }
        }
        
        private void SaveUserBannedPhrases()
        {
            try
            {
                var json = JsonSerializer.Serialize(userBannedPhrases.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(bannedPhrasesPath, json);
            }
            catch { }
        }
    }
    
    // Sistema de filtrado por país/IP (GeoIP)
    public class GeoIPFilter
    {
        private HashSet<string> bannedCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> bannedIPRanges = new HashSet<string>();
        private Dictionary<string, string> allowedUsers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool enableGeoFiltering = false;
        private string geoFilterPath;
        
        public GeoIPFilter(string dataDir)
        {
            geoFilterPath = Path.Combine(dataDir, "geo_filter.json");
            LoadGeoFilter();
        }
        
        public void BanCountry(string countryCode)
        {
            bannedCountries.Add(countryCode.ToUpper());
            SaveGeoFilter();
        }
        
        public void UnbanCountry(string countryCode)
        {
            bannedCountries.Remove(countryCode.ToUpper());
            SaveGeoFilter();
        }
        
        public void BanIPRange(string ipRange)
        {
            bannedIPRanges.Add(ipRange);
            SaveGeoFilter();
        }
        
        public void UnbanIPRange(string ipRange)
        {
            bannedIPRanges.Remove(ipRange);
            SaveGeoFilter();
        }
        
        public void AddAllowedUser(string username, string reason = "")
        {
            allowedUsers[username] = reason;
            SaveGeoFilter();
        }
        
        public bool IsUserBanned(string username, string ipAddress)
        {
            if (!enableGeoFiltering) return false;
            
            // Usuarios en lista blanca siempre permitidos
            if (allowedUsers.ContainsKey(username))
            {
                return false;
            }
            
            // Verificar rango IP
            foreach (var range in bannedIPRanges)
            {
                if (IsIPInRange(ipAddress, range))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private bool IsIPInRange(string ipAddress, string range)
        {
            try
            {
                // Formato: 192.168.1.0/24 o 192.168.1.*
                if (range.Contains("*"))
                {
                    string pattern = range.Replace(".", "\\.").Replace("*", ".*");
                    return System.Text.RegularExpressions.Regex.IsMatch(ipAddress, "^" + pattern + "$");
                }
                
                // Implementación básica de CIDR
                if (range.Contains("/"))
                {
                    var parts = range.Split('/');
                    if (parts.Length == 2)
                    {
                        string networkIP = parts[0];
                        int prefixLength = int.Parse(parts[1]);
                        
                        // Simplificación: comparar primeros octetos
                        var networkParts = networkIP.Split('.');
                        var ipParts = ipAddress.Split('.');
                        
                        int octetsToCompare = prefixLength / 8;
                        for (int i = 0; i < octetsToCompare && i < 4; i++)
                        {
                            if (networkParts[i] != ipParts[i])
                                return false;
                        }
                        return true;
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        public void SetEnabled(bool enabled)
        {
            enableGeoFiltering = enabled;
        }
        
        public List<string> GetBannedCountries()
        {
            return bannedCountries.ToList();
        }
        
        public List<string> GetBannedIPRanges()
        {
            return bannedIPRanges.ToList();
        }
        
        private void LoadGeoFilter()
        {
            try
            {
                if (File.Exists(geoFilterPath))
                {
                    var json = File.ReadAllText(geoFilterPath);
                    var data = JsonSerializer.Deserialize<GeoFilterData>(json);
                    
                    if (data != null)
                    {
                        bannedCountries = new HashSet<string>(data.BannedCountries ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                        bannedIPRanges = new HashSet<string>(data.BannedIPRanges ?? new List<string>());
                        allowedUsers = data.AllowedUsers ?? new Dictionary<string, string>();
                        enableGeoFiltering = data.Enabled;
                    }
                }
            }
            catch { }
        }
        
        private void SaveGeoFilter()
        {
            try
            {
                var data = new GeoFilterData
                {
                    BannedCountries = bannedCountries.ToList(),
                    BannedIPRanges = bannedIPRanges.ToList(),
                    AllowedUsers = allowedUsers,
                    Enabled = enableGeoFiltering
                };
                
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(geoFilterPath, json);
            }
            catch { }
        }
        
        private class GeoFilterData
        {
            public List<string> BannedCountries { get; set; }
            public List<string> BannedIPRanges { get; set; }
            public Dictionary<string, string> AllowedUsers { get; set; }
            public bool Enabled { get; set; }
        }
    }
}
