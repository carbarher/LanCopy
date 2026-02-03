using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SlskDown.Services
{
    /// <summary>
    /// Utilidades para manipulación de strings
    /// </summary>
    public static class StringHelpers
    {
        /// <summary>
        /// Normaliza un string removiendo acentos y caracteres especiales
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remover acentos
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Calcula la distancia de Levenshtein entre dos strings (para búsqueda fuzzy)
        /// </summary>
        public static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return target?.Length ?? 0;
            
            if (string.IsNullOrEmpty(target))
                return source.Length;

            var n = source.Length;
            var m = target.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++)
                d[i, 0] = i;
            
            for (int j = 0; j <= m; j++)
                d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    var cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        /// <summary>
        /// Calcula la similitud entre dos strings (0-100%)
        /// </summary>
        public static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target))
                return 100.0;
            
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            var distance = LevenshteinDistance(source.ToLowerInvariant(), target.ToLowerInvariant());
            var maxLength = Math.Max(source.Length, target.Length);
            
            return (1.0 - (double)distance / maxLength) * 100.0;
        }

        /// <summary>
        /// Busca fuzzy - encuentra strings similares
        /// </summary>
        public static bool FuzzyMatch(string source, string target, double minSimilarity = 80.0)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return false;

            return CalculateSimilarity(source, target) >= minSimilarity;
        }

        /// <summary>
        /// Trunca un string a una longitud máxima
        /// </summary>
        public static string Truncate(string text, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - suffix.Length) + suffix;
        }

        /// <summary>
        /// Limpia un string removiendo caracteres no imprimibles
        /// </summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return Regex.Replace(text, @"[^\u0020-\u007E\u00A0-\u00FF]", "");
        }

        /// <summary>
        /// Convierte a Title Case (Primera Letra De Cada Palabra)
        /// </summary>
        public static string ToTitleCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var textInfo = CultureInfo.CurrentCulture.TextInfo;
            return textInfo.ToTitleCase(text.ToLower(CultureInfo.CurrentCulture));
        }

        /// <summary>
        /// Extrae números de un string
        /// </summary>
        public static List<int> ExtractNumbers(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<int>();

            var matches = Regex.Matches(text, @"\d+");
            return matches.Cast<Match>().Select(m => int.Parse(m.Value)).ToList();
        }

        /// <summary>
        /// Remueve múltiples espacios consecutivos
        /// </summary>
        public static string RemoveExtraSpaces(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return Regex.Replace(text.Trim(), @"\s+", " ");
        }

        /// <summary>
        /// Verifica si un string contiene solo caracteres alfanuméricos
        /// </summary>
        public static bool IsAlphanumeric(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return Regex.IsMatch(text, @"^[a-zA-Z0-9]+$");
        }

        /// <summary>
        /// Cuenta las ocurrencias de un substring
        /// </summary>
        public static int CountOccurrences(string text, string substring, bool ignoreCase = true)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(substring))
                return 0;

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(substring, index, comparison)) != -1)
            {
                count++;
                index += substring.Length;
            }

            return count;
        }

        /// <summary>
        /// Divide un string en chunks de tamaño específico
        /// </summary>
        public static List<string> SplitIntoChunks(string text, int chunkSize)
        {
            if (string.IsNullOrEmpty(text) || chunkSize <= 0)
                return new List<string>();

            var chunks = new List<string>();
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
            }

            return chunks;
        }

        /// <summary>
        /// Reemplaza múltiples strings de una vez
        /// </summary>
        public static string ReplaceMultiple(string text, Dictionary<string, string> replacements)
        {
            if (string.IsNullOrEmpty(text) || replacements == null || replacements.Count == 0)
                return text;

            var result = text;
            foreach (var replacement in replacements)
            {
                result = result.Replace(replacement.Key, replacement.Value);
            }

            return result;
        }

        /// <summary>
        /// Genera un slug URL-friendly
        /// </summary>
        public static string ToSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Normalizar y convertir a minúsculas
            text = Normalize(text).ToLowerInvariant();

            // Remover caracteres no válidos
            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");

            // Reemplazar espacios y múltiples guiones
            text = Regex.Replace(text, @"\s+", "-");
            text = Regex.Replace(text, @"-+", "-");

            return text.Trim('-');
        }

        /// <summary>
        /// Verifica si un string es un email válido
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enmascara información sensible (ej: contraseñas, emails)
        /// </summary>
        public static string Mask(string text, int visibleChars = 3, char maskChar = '*')
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (text.Length <= visibleChars)
                return new string(maskChar, text.Length);

            var visible = text.Substring(0, visibleChars);
            var masked = new string(maskChar, text.Length - visibleChars);
            
            return visible + masked;
        }

        /// <summary>
        /// Capitaliza la primera letra
        /// </summary>
        public static string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (text.Length == 1)
                return text.ToUpperInvariant();

            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>
        /// Invierte un string
        /// </summary>
        public static string Reverse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var charArray = text.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        /// <summary>
        /// Verifica si un string es palíndromo
        /// </summary>
        public static bool IsPalindrome(string text, bool ignoreCase = true, bool ignoreSpaces = true)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var processed = text;
            
            if (ignoreSpaces)
                processed = processed.Replace(" ", "");
            
            if (ignoreCase)
                processed = processed.ToLowerInvariant();

            return processed == Reverse(processed);
        }

        /// <summary>
        /// Obtiene las iniciales de un nombre
        /// </summary>
        public static string GetInitials(string name, int maxInitials = 3)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var initials = words
                .Take(maxInitials)
                .Select(w => char.ToUpperInvariant(w[0]))
                .ToArray();

            return new string(initials);
        }

        /// <summary>
        /// Compara dos strings ignorando case y espacios
        /// </summary>
        public static bool EqualsIgnoreCaseAndSpaces(string text1, string text2)
        {
            if (text1 == null && text2 == null)
                return true;
            
            if (text1 == null || text2 == null)
                return false;

            var normalized1 = text1.Replace(" ", "").ToLowerInvariant();
            var normalized2 = text2.Replace(" ", "").ToLowerInvariant();

            return normalized1 == normalized2;
        }
    }
}
