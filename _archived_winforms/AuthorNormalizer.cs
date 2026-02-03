using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace SlskDown.Core
{
    /// <summary>
    /// Wrapper C# para funciones de normalización de autores en Rust
    /// Proporciona normalización ultra-rápida con procesamiento paralelo
    /// </summary>
    public static class AuthorNormalizer
    {
        private const string DllName = "slskdown_core.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr normalize_author_name(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            int nameLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr normalize_authors_batch(
            IntPtr[] names,
            int namesCount,
            out int resultCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int are_authors_equivalent(
            [MarshalAs(UnmanagedType.LPStr)] string name1,
            int name1Len,
            [MarshalAs(UnmanagedType.LPStr)] string name2,
            int name2Len);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr find_canonical_authors(
            IntPtr[] authors,
            int authorsCount,
            IntPtr[] canonical,
            int canonicalCount,
            out int resultCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr find_author_duplicates(
            IntPtr[] authors,
            int authorsCount,
            double threshold,
            out int resultCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_string(IntPtr ptr);

        public sealed class AuthorDuplicateGroup
        {
            public string NormalizedKey { get; init; } = string.Empty;
            public IReadOnlyList<string> Members { get; init; } = Array.Empty<string>();
        }

        private sealed class AuthorDuplicateSuggestionDto
        {
            [JsonProperty("normalized")]
            public string Normalized { get; set; } = string.Empty;

            [JsonProperty("members")]
            public List<string> Members { get; set; } = new();
        }

        /// <summary>
        /// Normaliza un nombre de autor para matching canónico
        /// </summary>
        public static string Normalize(string authorName)
        {
            if (string.IsNullOrWhiteSpace(authorName))
                return string.Empty;

            try
            {
                var ptr = normalize_author_name(authorName, authorName.Length);
                if (ptr == IntPtr.Zero)
                    return string.Empty;

                try
                {
                    return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
                }
                finally
                {
                    free_string(ptr);
                }
            }
            catch
            {
                return NormalizeFallback(authorName);
            }
        }

        /// <summary>
        /// Detecta grupos de autores duplicados usando la normalización y similitud desde Rust.
        /// Devuelve agrupaciones de nombres que deberían fusionarse.
        /// </summary>
        public static List<AuthorDuplicateGroup> FindDuplicateGroups(
            IEnumerable<string> authors,
            double similarityThreshold = 0.9)
        {
            var authorsList = authors?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
            if (authorsList == null || authorsList.Count < 2)
            {
                return new List<AuthorDuplicateGroup>();
            }

            try
            {
                var ptrs = new IntPtr[authorsList.Count];
                for (int i = 0; i < authorsList.Count; i++)
                {
                    ptrs[i] = Marshal.StringToHGlobalAnsi(authorsList[i]);
                }

                try
                {
                    var resultPtr = find_author_duplicates(ptrs, authorsList.Count, similarityThreshold, out _);
                    if (resultPtr == IntPtr.Zero)
                    {
                        return FindDuplicateGroupsFallback(authorsList, similarityThreshold);
                    }

                    try
                    {
                        var json = Marshal.PtrToStringAnsi(resultPtr);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            return FindDuplicateGroupsFallback(authorsList, similarityThreshold);
                        }

                        var dtoList = JsonConvert.DeserializeObject<List<AuthorDuplicateSuggestionDto>>(json);
                        if (dtoList == null)
                        {
                            return FindDuplicateGroupsFallback(authorsList, similarityThreshold);
                        }

                        return dtoList
                            .Where(dto => dto.Members != null && dto.Members.Count > 1)
                            .Select(dto => new AuthorDuplicateGroup
                            {
                                NormalizedKey = dto.Normalized,
                                Members = dto.Members
                                    .Where(m => !string.IsNullOrWhiteSpace(m))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                                    .ToArray()
                            })
                            .Where(group => group.Members.Count > 1)
                            .ToList();
                    }
                    finally
                    {
                        free_string(resultPtr);
                    }
                }
                finally
                {
                    foreach (var ptr in ptrs)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
            }
            catch
            {
                return FindDuplicateGroupsFallback(authorsList, similarityThreshold);
            }
        }

        private static List<AuthorDuplicateGroup> FindDuplicateGroupsFallback(
            List<string> authors,
            double similarityThreshold)
        {
            _ = similarityThreshold;

            return authors
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .GroupBy(NormalizeFallback, StringComparer.OrdinalIgnoreCase)
                .Select(g => new AuthorDuplicateGroup
                {
                    NormalizedKey = g.Key,
                    Members = g
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                })
                .Where(group => group.Members.Count > 1)
                .ToList();
        }

        /// <summary>
        /// Normaliza un batch de autores en paralelo (10-50x más rápido que C#)
        /// </summary>
        public static List<string> NormalizeBatch(IEnumerable<string> authors)
        {
            var authorsList = authors?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
            if (authorsList == null || authorsList.Count == 0)
                return new List<string>();

            try
            {
                var ptrs = new IntPtr[authorsList.Count];
                for (int i = 0; i < authorsList.Count; i++)
                {
                    ptrs[i] = Marshal.StringToHGlobalAnsi(authorsList[i]);
                }

                try
                {
                    var resultPtr = normalize_authors_batch(ptrs, authorsList.Count, out int resultCount);
                    if (resultPtr == IntPtr.Zero)
                        return authorsList.Select(NormalizeFallback).ToList();

                    try
                    {
                        var json = Marshal.PtrToStringAnsi(resultPtr);
                        if (string.IsNullOrEmpty(json))
                            return authorsList.Select(NormalizeFallback).ToList();

                        return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                    }
                    finally
                    {
                        free_string(resultPtr);
                    }
                }
                finally
                {
                    foreach (var ptr in ptrs)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
            }
            catch
            {
                return authorsList.Select(NormalizeFallback).ToList();
            }
        }

        /// <summary>
        /// Verifica si dos nombres de autor son equivalentes
        /// </summary>
        public static bool AreEquivalent(string author1, string author2)
        {
            if (string.IsNullOrWhiteSpace(author1) || string.IsNullOrWhiteSpace(author2))
                return false;

            try
            {
                var result = are_authors_equivalent(author1, author1.Length, author2, author2.Length);
                return result == 1;
            }
            catch
            {
                return NormalizeFallback(author1).Equals(NormalizeFallback(author2), StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Encuentra autores canónicos en un batch (paralelo)
        /// Devuelve tuplas (índice_autor, nombre_autor, índice_canónico)
        /// </summary>
        public static List<(int Index, string Author, int CanonicalIndex)> FindCanonical(
            IEnumerable<string> authors,
            IEnumerable<string> canonicalList)
        {
            var authorsList = authors?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
            var canonicalArray = canonicalList?.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

            if (authorsList == null || authorsList.Count == 0 || canonicalArray == null || canonicalArray.Count == 0)
                return new List<(int, string, int)>();

            try
            {
                var authorPtrs = new IntPtr[authorsList.Count];
                var canonicalPtrs = new IntPtr[canonicalArray.Count];

                for (int i = 0; i < authorsList.Count; i++)
                {
                    authorPtrs[i] = Marshal.StringToHGlobalAnsi(authorsList[i]);
                }

                for (int i = 0; i < canonicalArray.Count; i++)
                {
                    canonicalPtrs[i] = Marshal.StringToHGlobalAnsi(canonicalArray[i]);
                }

                try
                {
                    var resultPtr = find_canonical_authors(
                        authorPtrs, authorsList.Count,
                        canonicalPtrs, canonicalArray.Count,
                        out int resultCount);

                    if (resultPtr == IntPtr.Zero)
                        return new List<(int, string, int)>();

                    try
                    {
                        var json = Marshal.PtrToStringAnsi(resultPtr);
                        if (string.IsNullOrEmpty(json))
                            return new List<(int, string, int)>();

                        var matches = JsonConvert.DeserializeObject<List<(int, string, int)>>(json);
                        return matches ?? new List<(int, string, int)>();
                    }
                    finally
                    {
                        free_string(resultPtr);
                    }
                }
                finally
                {
                    foreach (var ptr in authorPtrs)
                        Marshal.FreeHGlobal(ptr);
                    foreach (var ptr in canonicalPtrs)
                        Marshal.FreeHGlobal(ptr);
                }
            }
            catch
            {
                return new List<(int, string, int)>();
            }
        }

        /// <summary>
        /// Implementación fallback en C# (más lenta pero funcional)
        /// </summary>
        private static string NormalizeFallback(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var normalized = name.ToLowerInvariant();
            normalized = RemoveAccents(normalized);

            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                    sb.Append(c);
            }
            normalized = sb.ToString();

            var tokens = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 1)
                .OrderBy(t => t)
                .ToArray();

            return string.Join(" ", tokens);
        }

        private static string RemoveAccents(string text)
        {
            var sb = new StringBuilder();
            foreach (var c in text)
            {
                sb.Append(c switch
                {
                    'á' or 'à' or 'ä' or 'â' => 'a',
                    'é' or 'è' or 'ë' or 'ê' => 'e',
                    'í' or 'ì' or 'ï' or 'î' => 'i',
                    'ó' or 'ò' or 'ö' or 'ô' => 'o',
                    'ú' or 'ù' or 'ü' or 'û' => 'u',
                    'ñ' => 'n',
                    _ => c
                });
            }
            return sb.ToString();
        }

        /// <summary>
        /// Verifica si la DLL de Rust está disponible
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                var test = Normalize("Test Author");
                return !string.IsNullOrEmpty(test);
            }
            catch
            {
                return false;
            }
        }
    }
}
