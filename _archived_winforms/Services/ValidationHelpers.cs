using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SlskDown.Core;

namespace SlskDown.Services
{
    /// <summary>
    /// Utilidades para validación de datos
    /// </summary>
    public static class ValidationHelpers
    {
        private static readonly string[] SpanishKeywords = new[]
        {
            "español", "spanish", "castellano", "spa", "es", "latino", "latinoamerica",
            "argentina", "mexico", "españa", "chile", "colombia", "peru", "venezuela"
        };

        private static readonly (string Language, Regex Pattern)[] NonSpanishIndicators = new[]
        {
            ("english", new Regex(@"\b(the|a|an|of|with|from|into|through|about|against|between|without)\b|\b\w+(ing|tion|ness|ment|ship|hood)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)),
            ("italian", new Regex(@"\b(il|della|degli|delle|lo|gli|sono|è|era|che|di)\b|\b\w+(zione|zioni|aggio|eggio)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)),
            ("french", new Regex(@"\b(le|la|les|des|une|un)\b|\b(l'|d'|c')\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)),
            ("german", new Regex(@"\b(der|die|das|den|dem|des|ein|eine|und|oder|aber|von|zu|mit|für|auf|aus)\b|ß", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)),
            ("portuguese", new Regex(@"\b(não|dos|das|uma|com|para|também|você)\b|[ãõç]", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant))
        };

        private static readonly string[] DocumentExtensions = new[]
        {
            ".pdf", ".epub", ".mobi", ".azw", ".azw3", ".djvu",
            ".doc", ".docx", ".rtf", ".txt", ".odt"
        };

        /// <summary>
        /// Verifica si un nombre de archivo contiene indicadores de idioma español
        /// </summary>
        public static bool IsSpanishText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var lowerText = text.ToLowerInvariant();
            
            // Buscar palabras clave en español
            if (SpanishKeywords.Any(keyword => lowerText.Contains(keyword)))
                return true;

            // Buscar patrones de texto en español (ñ, acentos)
            if (Regex.IsMatch(text, @"[ñáéíóúü]", RegexOptions.IgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// Determina si el texto pertenece claramente a otro idioma distinto al español
        /// </summary>
        public static bool IsClearlyNonSpanish(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (var indicator in NonSpanishIndicators)
            {
                if (indicator.Pattern.IsMatch(text))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Verifica si un archivo está en la blacklist
        /// </summary>
        public static bool IsFileBlacklisted(string fileName, string username, System.Collections.Generic.List<string> blacklist)
        {
            if (string.IsNullOrWhiteSpace(fileName) || blacklist == null)
                return false;

            // Verificar usuario en blacklist
            if (!string.IsNullOrWhiteSpace(username) && 
                blacklist.Any(b => b.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Verificar nombre de archivo en blacklist
            if (blacklist.Any(b => fileName.Contains(b, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Verifica si un nombre de usuario es válido
        /// </summary>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            // Longitud mínima y máxima
            if (username.Length < 3 || username.Length > 30)
                return false;

            // Solo caracteres alfanuméricos, guiones y guiones bajos
            return Regex.IsMatch(username, @"^[a-zA-Z0-9_-]+$");
        }

        /// <summary>
        /// Verifica si una contraseña es válida
        /// </summary>
        public static bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            // Longitud mínima
            return password.Length >= 6;
        }

        /// <summary>
        /// Verifica si una ruta es válida y accesible
        /// </summary>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Verificar que la ruta es válida
                var fullPath = Path.GetFullPath(path);
                
                // Verificar que el directorio padre existe o se puede crear
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    // Intentar crear el directorio
                    Directory.CreateDirectory(directory);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si un tamaño de archivo es válido
        /// </summary>
        public static bool IsValidFileSize(long sizeBytes, long minBytes = 0, long maxBytes = long.MaxValue)
        {
            return sizeBytes >= minBytes && sizeBytes <= maxBytes;
        }

        /// <summary>
        /// Verifica si una extensión de archivo es válida
        /// </summary>
        public static bool IsValidExtension(string extension, string[] allowedExtensions = null)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            // Normalizar extensión
            extension = extension.ToLowerInvariant();
            if (!extension.StartsWith("."))
                extension = "." + extension;

            // Si no hay lista de permitidas, aceptar todas excepto ejecutables
            if (allowedExtensions == null || allowedExtensions.Length == 0)
            {
                var dangerousExtensions = new[] { ".exe", ".dll", ".bat", ".cmd", ".scr", ".vbs", ".js" };
                return !dangerousExtensions.Contains(extension);
            }

            return allowedExtensions.Any(ext => 
                ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verifica si un puerto es válido
        /// </summary>
        public static bool IsValidPort(int port)
        {
            return port >= 1024 && port <= 65535;
        }

        /// <summary>
        /// Verifica si un timeout es válido
        /// </summary>
        public static bool IsValidTimeout(int timeoutSeconds)
        {
            return timeoutSeconds >= 0 && timeoutSeconds <= 3600; // Max 1 hora
        }

        /// <summary>
        /// Verifica si un número de reintentos es válido
        /// </summary>
        public static bool IsValidRetryCount(int retries)
        {
            return retries >= 0 && retries <= 20;
        }

        /// <summary>
        /// Verifica si un archivo es un documento (ebook, PDF, etc.)
        /// </summary>
        public static bool IsDocumentFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return DocumentExtensions.Contains(extension);
        }

        // OPT #1: Caché de normalización de autores (evita recalcular)
        private static readonly Dictionary<string, string> authorNormalizationCache = 
            new Dictionary<string, string>(10000, StringComparer.Ordinal);
        private const int MAX_AUTHOR_CACHE_SIZE = 10000;
        
        // OPT #5: Pool de StringBuilder para reducir allocaciones
        private static readonly Stack<StringBuilder> stringBuilderPool = new Stack<StringBuilder>(16);
        private static readonly object poolLock = new object();

        private static int rustCoreAvailability = 0;

        private static bool IsRustCoreAvailable()
        {
            var snapshot = Volatile.Read(ref rustCoreAvailability);
            if (snapshot == 1)
            {
                return true;
            }
            if (snapshot == -1)
            {
                return false;
            }

            bool available = false;
            try
            {
                available = RustCore.IsAvailable();
            }
            catch
            {
                available = false;
            }

            Volatile.Write(ref rustCoreAvailability, available ? 1 : -1);
            return available;
        }
        
        /// <summary>
        /// Normaliza nombres de autores para tratar variaciones como el mismo autor
        /// Ejemplos: "A. E. Pepito", "A E Pepito", "A.E. Pepito", "AE Pepito" → "ae pepito"
        /// OPT #1: Con caché para evitar recalcular
        /// OPT #2: Usa StringBuilder para mejor rendimiento
        /// OPT #5: Pool de StringBuilder para reducir allocaciones
        /// OPT #6: Usa Rust si está disponible (5-10x más rápido)
        /// </summary>
        public static string NormalizeAuthorName(string authorName)
        {
            if (string.IsNullOrWhiteSpace(authorName))
                return string.Empty;

            // OPT #1: Verificar caché primero
            if (authorNormalizationCache.TryGetValue(authorName, out string cached))
                return cached;

            // OPT #6: Intentar usar Rust primero (5-10x más rápido)
            string normalized;
            try
            {
                if (RustOptimizer.IsAvailable)
                {
                    normalized = RustOptimizer.NormalizeAuthorName(authorName);
                    
                    // Guardar en caché
                    if (authorNormalizationCache.Count >= MAX_AUTHOR_CACHE_SIZE)
                    {
                        var toRemove = authorNormalizationCache.Keys.Take(MAX_AUTHOR_CACHE_SIZE / 5).ToList();
                        foreach (var key in toRemove)
                            authorNormalizationCache.Remove(key);
                    }
                    
                    authorNormalizationCache[authorName] = normalized;
                    return normalized;
                }
            }
            catch
            {
                // Fallback a C# si Rust falla
            }

            // Segundo intento: RustCore (si existe) + post-procesado rápido
            try
            {
                if (IsRustCoreAvailable())
                {
                    var rustNormalized = RustCore.NormalizeText(authorName);
                    if (!string.IsNullOrWhiteSpace(rustNormalized))
                    {
                        StringBuilder sbRust = null;
                        lock (poolLock)
                        {
                            if (stringBuilderPool.Count > 0)
                                sbRust = stringBuilderPool.Pop();
                        }

                        if (sbRust == null)
                            sbRust = new StringBuilder(100);
                        else
                            sbRust.Clear();

                        bool lastWasSpaceRust = false;
                        foreach (char c in rustNormalized)
                        {
                            if (c == '.')
                                continue;

                            if (char.IsWhiteSpace(c))
                            {
                                if (!lastWasSpaceRust && sbRust.Length > 0)
                                {
                                    sbRust.Append(' ');
                                    lastWasSpaceRust = true;
                                }
                            }
                            else
                            {
                                sbRust.Append(c);
                                lastWasSpaceRust = false;
                            }
                        }

                        if (sbRust.Length > 0 && sbRust[sbRust.Length - 1] == ' ')
                            sbRust.Length--;

                        normalized = sbRust.ToString();

                        lock (poolLock)
                        {
                            if (stringBuilderPool.Count < 16)
                                stringBuilderPool.Push(sbRust);
                        }

                        if (authorNormalizationCache.Count >= MAX_AUTHOR_CACHE_SIZE)
                        {
                            var toRemove = authorNormalizationCache.Keys.Take(MAX_AUTHOR_CACHE_SIZE / 5).ToList();
                            foreach (var key in toRemove)
                                authorNormalizationCache.Remove(key);
                        }

                        authorNormalizationCache[authorName] = normalized;
                        return normalized;
                    }
                }
            }
            catch
            {
            }

            // Fallback: OPT #5: Obtener StringBuilder del pool
            StringBuilder sb = null;
            lock (poolLock)
            {
                if (stringBuilderPool.Count > 0)
                    sb = stringBuilderPool.Pop();
            }
            
            if (sb == null)
                sb = new StringBuilder(100); // Crear nuevo si pool vacío
            else
                sb.Clear(); // Limpiar si viene del pool
            
            // OPT #2: Procesar carácter por carácter
            bool lastWasSpace = false;
            
            foreach (char c in authorName)
            {
                if (c == '.')
                    continue; // Ignorar puntos
                    
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace && sb.Length > 0)
                    {
                        sb.Append(' ');
                        lastWasSpace = true;
                    }
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasSpace = false;
                }
            }
            
            // Trim final (remover espacio al final si existe)
            if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
                sb.Length--;
            
            normalized = sb.ToString();
            
            // OPT #5: Devolver StringBuilder al pool
            lock (poolLock)
            {
                if (stringBuilderPool.Count < 16) // Límite de pool
                    stringBuilderPool.Push(sb);
            }
            
            // OPT #1: Guardar en caché con límite
            if (authorNormalizationCache.Count >= MAX_AUTHOR_CACHE_SIZE)
            {
                // Limpiar 20% más antiguo
                var toRemove = authorNormalizationCache.Keys.Take(MAX_AUTHOR_CACHE_SIZE / 5).ToList();
                foreach (var key in toRemove)
                    authorNormalizationCache.Remove(key);
            }
            
            authorNormalizationCache[authorName] = normalized;
            return normalized;
        }

        /// <summary>
        /// Compara dos nombres de autores ignorando variaciones de formato
        /// </summary>
        public static bool AreAuthorNamesEquivalent(string name1, string name2)
        {
            if (string.IsNullOrWhiteSpace(name1) && string.IsNullOrWhiteSpace(name2))
                return true;

            if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
                return false;

            return NormalizeAuthorName(name1) == NormalizeAuthorName(name2);
        }

        /// <summary>
        /// Valida una configuración completa
        /// </summary>
        public static (bool isValid, string errorMessage) ValidateConfiguration(
            string username, 
            string password, 
            string downloadDir, 
            int listenPort)
        {
            if (!IsValidUsername(username))
                return (false, "Usuario inválido. Debe tener 3-30 caracteres alfanuméricos.");

            if (!IsValidPassword(password))
                return (false, "Contraseña inválida. Debe tener al menos 6 caracteres.");

            if (!IsValidPath(downloadDir))
                return (false, "Ruta de descargas inválida o inaccesible.");

            if (!IsValidPort(listenPort))
                return (false, "Puerto inválido. Debe estar entre 1024 y 65535.");

            return (true, string.Empty);
        }
    }
}
