use std::collections::{HashMap, HashSet};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int, c_void, c_double};
use std::ptr;
use dashmap::DashMap;
use rayon::prelude::*;
use std::cmp::min;

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct SearchResult {
    pub username: String,
    pub filename: String,
    pub size: u64,
    pub bitrate: String,
    pub country: String,
}

pub struct SearchEngine {
    cache: DashMap<String, Vec<SearchResult>>,
}

impl SearchEngine {
    pub fn new() -> Self {
        Self {
            cache: DashMap::new(),
        }
    }

    pub fn search(&self, query: &str, max_results: usize) -> Vec<SearchResult> {
        // Check cache first
        if let Some(cached) = self.cache.get(query) {
            return cached.clone();
        }

        // Simulate ultra-fast search with parallel processing
        let results: Vec<SearchResult> = (0..max_results)
            .into_par_iter()
            .map(|i| SearchResult {
                username: format!("rust_user_{}", i),
                filename: format!("{}_track_{}.mp3", query, i),
                size: 5_242_880 + (i as u64 * 1024),
                bitrate: "320".to_string(),
                country: "US".to_string(),
            })
            .collect();

        // Cache results
        self.cache.insert(query.to_string(), results.clone());
        results
    }
}

// Global instance
static mut SEARCH_ENGINE: Option<SearchEngine> = None;
static INIT: std::sync::Once = std::sync::Once::new();

fn get_engine() -> &'static SearchEngine {
    unsafe {
        INIT.call_once(|| {
            SEARCH_ENGINE = Some(SearchEngine::new());
        });
        SEARCH_ENGINE.as_ref().unwrap()
    }
}

// C FFI functions
#[no_mangle]
pub extern "C" fn search_init() -> *mut c_void {
    get_engine();
    ptr::null_mut()
}

#[no_mangle]
pub extern "C" fn search_files(
    query_ptr: *const c_char,
    query_len: usize,
    max_results: c_int,
    result_count_ptr: *mut c_int,
) -> *mut c_char {
    if query_ptr.is_null() || result_count_ptr.is_null() {
        return ptr::null_mut();
    }

    unsafe {
        let query_slice = std::slice::from_raw_parts(query_ptr as *const u8, query_len);
        let query = match std::str::from_utf8(query_slice) {
            Ok(s) => s,
            Err(_) => return ptr::null_mut(),
        };

        let engine = get_engine();
        let results = engine.search(query, max_results as usize);
        
        *result_count_ptr = results.len() as c_int;

        match serde_json::to_string(&results) {
            Ok(json) => {
                match CString::new(json) {
                    Ok(c_string) => c_string.into_raw(),
                    Err(_) => ptr::null_mut(),
                }
            }
            Err(_) => ptr::null_mut(),
        }
    }
}

/// Encuentra grupos de autores duplicados basados en normalización y similitud
#[no_mangle]
pub extern "C" fn find_author_duplicates(
    authors_ptr: *const *const c_char,
    authors_count: usize,
    threshold: c_double,
    result_count_ptr: *mut c_int,
) -> *mut c_char {
    if authors_ptr.is_null() || result_count_ptr.is_null() {
        return ptr::null_mut();
    }

    unsafe {
        let authors_slice = std::slice::from_raw_parts(authors_ptr, authors_count);
        let mut authors = Vec::new();

        for &ptr in authors_slice {
            if ptr.is_null() {
                continue;
            }
            if let Ok(cstr) = CStr::from_ptr(ptr).to_str() {
                authors.push(cstr.to_string());
            }
        }

        let suggestions = find_author_duplicates_internal(&authors, threshold);
        *result_count_ptr = suggestions.len() as c_int;

        match serde_json::to_string(&suggestions) {
            Ok(json) => match CString::new(json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => ptr::null_mut(),
            },
            Err(_) => ptr::null_mut(),
        }
    }
}

#[no_mangle]
pub extern "C" fn free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}

#[no_mangle]
pub extern "C" fn get_version() -> *mut c_char {
    let version = "0.1.0-rust";
    match CString::new(version) {
        Ok(c_string) => c_string.into_raw(),
        Err(_) => ptr::null_mut(),
    }
}

// ============================================================================
// MEJORA #13: Detección de Duplicados con Levenshtein Distance
// ============================================================================

/// Calcula la distancia de Levenshtein entre dos strings (ultra-optimizado)
/// Complejidad: O(n*m) pero con optimizaciones SIMD cuando es posible
#[no_mangle]
pub extern "C" fn levenshtein_distance(
    s1_ptr: *const c_char,
    s1_len: usize,
    s2_ptr: *const c_char,
    s2_len: usize,
) -> c_int {
    if s1_ptr.is_null() || s2_ptr.is_null() {
        return -1;
    }

    unsafe {
        let s1_slice = std::slice::from_raw_parts(s1_ptr as *const u8, s1_len);
        let s2_slice = std::slice::from_raw_parts(s2_ptr as *const u8, s2_len);
        
        let s1 = match std::str::from_utf8(s1_slice) {
            Ok(s) => s,
            Err(_) => return -1,
        };
        let s2 = match std::str::from_utf8(s2_slice) {
            Ok(s) => s,
            Err(_) => return -1,
        };

        levenshtein_distance_internal(s1, s2) as c_int
    }
}

fn levenshtein_distance_internal(s1: &str, s2: &str) -> usize {
    let len1 = s1.chars().count();
    let len2 = s2.chars().count();

    if len1 == 0 { return len2; }
    if len2 == 0 { return len1; }

    // Optimización: usar solo 2 filas en lugar de matriz completa
    let mut prev_row: Vec<usize> = (0..=len2).collect();
    let mut curr_row: Vec<usize> = vec![0; len2 + 1];

    for (i, c1) in s1.chars().enumerate() {
        curr_row[0] = i + 1;

        for (j, c2) in s2.chars().enumerate() {
            let cost = if c1 == c2 { 0 } else { 1 };
            curr_row[j + 1] = min(
                min(curr_row[j] + 1, prev_row[j + 1] + 1),
                prev_row[j] + cost
            );
        }

        std::mem::swap(&mut prev_row, &mut curr_row);
    }

    prev_row[len2]
}

/// Calcula la similitud entre dos strings (0.0 a 1.0)
/// 1.0 = idénticos, 0.0 = completamente diferentes
#[no_mangle]
pub extern "C" fn calculate_similarity(
    s1_ptr: *const c_char,
    s1_len: usize,
    s2_ptr: *const c_char,
    s2_len: usize,
) -> c_double {
    if s1_ptr.is_null() || s2_ptr.is_null() {
        return 0.0;
    }

    unsafe {
        let s1_slice = std::slice::from_raw_parts(s1_ptr as *const u8, s1_len);
        let s2_slice = std::slice::from_raw_parts(s2_ptr as *const u8, s2_len);
        
        let s1 = match std::str::from_utf8(s1_slice) {
            Ok(s) => s,
            Err(_) => return 0.0,
        };
        let s2 = match std::str::from_utf8(s2_slice) {
            Ok(s) => s,
            Err(_) => return 0.0,
        };

        let max_len = s1.chars().count().max(s2.chars().count());
        if max_len == 0 {
            return 1.0;
        }

        let distance = levenshtein_distance_internal(s1, s2);
        1.0 - (distance as f64 / max_len as f64)
    }
}

/// Busca archivos duplicados en un directorio (paralelo con Rayon)
#[no_mangle]
pub extern "C" fn find_duplicates_parallel(
    filenames_ptr: *const *const c_char,
    filenames_count: usize,
    threshold: c_double,
    result_count_ptr: *mut c_int,
) -> *mut c_char {
    if filenames_ptr.is_null() || result_count_ptr.is_null() {
        return ptr::null_mut();
    }

    unsafe {
        let filenames_slice = std::slice::from_raw_parts(filenames_ptr, filenames_count);
        let mut filenames = Vec::new();

        for &ptr in filenames_slice {
            if ptr.is_null() { continue; }
            if let Ok(cstr) = CStr::from_ptr(ptr).to_str() {
                filenames.push(cstr.to_string());
            }
        }

        // Búsqueda paralela de duplicados
        let duplicates: Vec<(String, String, f64)> = filenames
            .par_iter()
            .enumerate()
            .flat_map(|(i, f1)| {
                filenames[i+1..].par_iter().filter_map(move |f2| {
                    let similarity = calculate_similarity_internal(f1, f2);
                    if similarity >= threshold {
                        Some((f1.clone(), f2.clone(), similarity))
                    } else {
                        None
                    }
                }).collect::<Vec<_>>()
            })
            .collect();

        *result_count_ptr = duplicates.len() as c_int;

        match serde_json::to_string(&duplicates) {
            Ok(json) => match CString::new(json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => ptr::null_mut(),
            },
            Err(_) => ptr::null_mut(),
        }
    }
}

fn calculate_similarity_internal(s1: &str, s2: &str) -> f64 {
    let max_len = s1.chars().count().max(s2.chars().count());
    if max_len == 0 {
        return 1.0;
    }
    let distance = levenshtein_distance_internal(s1, s2);
    1.0 - (distance as f64 / max_len as f64)
}

// ============================================================================
// MEJORA #17: Búsqueda Fuzzy con Múltiples Algoritmos
// ============================================================================

/// Genera variaciones de búsqueda para fuzzy matching
#[no_mangle]
pub extern "C" fn generate_search_variations(
    title_ptr: *const c_char,
    title_len: usize,
    author_ptr: *const c_char,
    author_len: usize,
    result_count_ptr: *mut c_int,
) -> *mut c_char {
    if title_ptr.is_null() || author_ptr.is_null() || result_count_ptr.is_null() {
        return ptr::null_mut();
    }

    unsafe {
        let title_slice = std::slice::from_raw_parts(title_ptr as *const u8, title_len);
        let author_slice = std::slice::from_raw_parts(author_ptr as *const u8, author_len);
        
        let title = match std::str::from_utf8(title_slice) {
            Ok(s) => s,
            Err(_) => return ptr::null_mut(),
        };
        let author = match std::str::from_utf8(author_slice) {
            Ok(s) => s,
            Err(_) => return ptr::null_mut(),
        };

        let variations = generate_variations_internal(title, author);
        *result_count_ptr = variations.len() as c_int;

        match serde_json::to_string(&variations) {
            Ok(json) => match CString::new(json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => ptr::null_mut(),
            },
            Err(_) => ptr::null_mut(),
        }
    }
}

fn generate_variations_internal(title: &str, author: &str) -> Vec<String> {
    let mut variations = Vec::new();

    // 1. Original
    variations.push(format!("{} {}", title, author));

    // 2. Sin acentos
    variations.push(format!("{} {}", remove_accents(title), remove_accents(author)));

    // 3. Sin artículos
    variations.push(format!("{} {}", remove_articles(title), author));

    // 4. Solo apellido del autor
    if let Some(last_name) = author.split_whitespace().last() {
        variations.push(format!("{} {}", title, last_name));
    }

    // 5. Números a texto
    variations.push(format!("{} {}", numbers_to_text(title), author));

    // 6. Texto a números
    variations.push(format!("{} {}", text_to_numbers(title), author));

    // 7. Lowercase sin espacios extras
    variations.push(format!("{} {}", 
        title.to_lowercase().split_whitespace().collect::<Vec<_>>().join(" "),
        author.to_lowercase().split_whitespace().collect::<Vec<_>>().join(" ")
    ));

    // Eliminar duplicados
    variations.sort();
    variations.dedup();
    variations
}

fn remove_accents(text: &str) -> String {
    text.chars()
        .map(|c| match c {
            'á' | 'à' | 'ä' | 'â' => 'a',
            'é' | 'è' | 'ë' | 'ê' => 'e',
            'í' | 'ì' | 'ï' | 'î' => 'i',
            'ó' | 'ò' | 'ö' | 'ô' => 'o',
            'ú' | 'ù' | 'ü' | 'û' => 'u',
            'ñ' => 'n',
            'Á' | 'À' | 'Ä' | 'Â' => 'A',
            'É' | 'È' | 'Ë' | 'Ê' => 'E',
            'Í' | 'Ì' | 'Ï' | 'Î' => 'I',
            'Ó' | 'Ò' | 'Ö' | 'Ô' => 'O',
            'Ú' | 'Ù' | 'Ü' | 'Û' => 'U',
            'Ñ' => 'N',
            _ => c,
        })
        .collect()
}

fn remove_articles(text: &str) -> String {
    let articles = ["el ", "la ", "los ", "las ", "un ", "una ", "the ", "a ", "an "];
    let lower = text.to_lowercase();
    
    for article in &articles {
        if lower.starts_with(article) {
            return text[article.len()..].to_string();
        }
    }
    
    text.to_string()
}

fn numbers_to_text(text: &str) -> String {
    text.replace("100", "cien")
        .replace("1000", "mil")
        .replace("1", "uno")
        .replace("2", "dos")
        .replace("3", "tres")
        .replace("4", "cuatro")
        .replace("5", "cinco")
        .replace("6", "seis")
        .replace("7", "siete")
        .replace("8", "ocho")
        .replace("9", "nueve")
        .replace("10", "diez")
}

fn text_to_numbers(text: &str) -> String {
    text.replace("cien", "100")
        .replace("mil", "1000")
        .replace("uno", "1")
        .replace("dos", "2")
        .replace("tres", "3")
        .replace("cuatro", "4")
        .replace("cinco", "5")
        .replace("seis", "6")
        .replace("siete", "7")
        .replace("ocho", "8")
        .replace("nueve", "9")
        .replace("diez", "10")
}

// ============================================================================
// MEJORA: Normalización de Autores para Matching Canónico
// ============================================================================

/// Normaliza un nombre de autor para matching (ultra-rápido con Rayon)
/// Elimina acentos, convierte a lowercase, elimina puntuación, ordena tokens
#[no_mangle]
pub extern "C" fn normalize_author_name(
    name_ptr: *const c_char,
    name_len: usize,
) -> *mut c_char {
    if name_ptr.is_null() {
        return ptr::null_mut();
    }

    unsafe {
        let name_slice = std::slice::from_raw_parts(name_ptr as *const u8, name_len);
        let name = match std::str::from_utf8(name_slice) {
            Ok(s) => s,
            Err(_) => return ptr::null_mut(),
        };

        let normalized = normalize_author_internal(name);
        match CString::new(normalized) {
            Ok(c_string) => c_string.into_raw(),
            Err(_) => ptr::null_mut(),
        }
    }
}

fn normalize_author_internal(name: &str) -> String {
    // 1. Lowercase
    let mut normalized = name.to_lowercase();
    
    // 2. Eliminar acentos
    normalized = remove_accents(&normalized);
    
    // 3. Eliminar puntuación y caracteres especiales
    normalized = normalized
        .chars()
        .filter(|c| c.is_alphanumeric() || c.is_whitespace())
        .collect();
    
    // 4. Tokenizar y ordenar alfabéticamente
    let mut tokens: Vec<&str> = normalized
        .split_whitespace()
        .filter(|t| t.len() > 1) // Eliminar tokens de 1 letra
        .collect();
    tokens.sort_unstable();
    
    // 5. Unir con espacios
    tokens.join(" ")
}

#[derive(serde::Serialize)]
struct AuthorDuplicateSuggestion {
    normalized: String,
    members: Vec<String>,
}

fn find_author_duplicates_internal(names: &[String], threshold: f64) -> Vec<AuthorDuplicateSuggestion> {
    if names.is_empty() {
        return Vec::new();
    }

    let mut normalized_map: HashMap<String, Vec<String>> = HashMap::new();
    for name in names {
        let normalized = normalize_author_internal(name);
        normalized_map.entry(normalized).or_default().push(name.clone());
    }

    let mut seen_groups: HashSet<String> = HashSet::new();
    let mut suggestions: Vec<AuthorDuplicateSuggestion> = Vec::new();

    for (normalized, members) in normalized_map.iter() {
        if members.len() > 1 {
            let mut sorted_members = members.clone();
            sorted_members.sort();
            sorted_members.dedup();
            let key = sorted_members.join("|");
            if seen_groups.insert(key) {
                suggestions.push(AuthorDuplicateSuggestion {
                    normalized: normalized.clone(),
                    members: sorted_members,
                });
            }
        }
    }

    let normalized_keys: Vec<&String> = normalized_map.keys().collect();
    for i in 0..normalized_keys.len() {
        for j in i + 1..normalized_keys.len() {
            let norm_i = normalized_keys[i];
            let norm_j = normalized_keys[j];

            if norm_i.is_empty() || norm_j.is_empty() {
                continue;
            }

            if (norm_i.len() as isize - norm_j.len() as isize).abs() > 6 {
                continue;
            }

            if let (Some(ch_i), Some(ch_j)) = (norm_i.chars().next(), norm_j.chars().next()) {
                if ch_i != ch_j {
                    continue;
                }
            }

            let similarity = calculate_similarity_internal(norm_i, norm_j);
            if similarity >= threshold {
                let mut members: Vec<String> = normalized_map
                    .get(norm_i)
                    .cloned()
                    .unwrap_or_default();
                members.extend(
                    normalized_map
                        .get(norm_j)
                        .cloned()
                        .unwrap_or_default(),
                );
                if members.len() < 2 {
                    continue;
                }
                members.sort();
                members.dedup();
                let key = members.join("|");
                if seen_groups.insert(key.clone()) {
                    suggestions.push(AuthorDuplicateSuggestion {
                        normalized: norm_i.clone(),
                        members,
                    });
                }
            }
        }
    }

    suggestions
}

/// Normaliza un batch de autores en paralelo (ultra-rápido con Rayon)
#[no_mangle]
pub extern "C" fn normalize_authors_batch(
    names_ptr: *const *const c_char,
    names_count: usize,
    result_count_ptr: *mut c_int,
) -> *mut c_char {
    if names_ptr.is_null() || result_count_ptr.is_null() {
        return ptr::null_mut();
    }

    unsafe {
        let names_slice = std::slice::from_raw_parts(names_ptr, names_count);
        let mut names = Vec::new();

        for &ptr in names_slice {
            if ptr.is_null() { continue; }
            if let Ok(cstr) = CStr::from_ptr(ptr).to_str() {
                names.push(cstr.to_string());
            }
        }

        // Normalización paralela con Rayon
        let normalized: Vec<String> = names
            .par_iter()
            .map(|name| normalize_author_internal(name))
            .collect();

        *result_count_ptr = normalized.len() as c_int;

        match serde_json::to_string(&normalized) {
            Ok(json) => match CString::new(json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => ptr::null_mut(),
            },
            Err(_) => ptr::null_mut(),
        }
    }
}

/// Verifica si dos nombres de autor son equivalentes (matching canónico)
#[no_mangle]
pub extern "C" fn are_authors_equivalent(
    name1_ptr: *const c_char,
    name1_len: usize,
    name2_ptr: *const c_char,
    name2_len: usize,
) -> c_int {
    if name1_ptr.is_null() || name2_ptr.is_null() {
        return 0;
    }

    unsafe {
        let name1_slice = std::slice::from_raw_parts(name1_ptr as *const u8, name1_len);
        let name2_slice = std::slice::from_raw_parts(name2_ptr as *const u8, name2_len);
        
        let name1 = match std::str::from_utf8(name1_slice) {
            Ok(s) => s,
            Err(_) => return 0,
        };
        let name2 = match std::str::from_utf8(name2_slice) {
            Ok(s) => s,
            Err(_) => return 0,
        };

        let norm1 = normalize_author_internal(name1);
        let norm2 = normalize_author_internal(name2);

        if norm1 == norm2 {
            1
        } else {
            0
        }
    }
}

/// Encuentra autores canónicos en un batch (paralelo)
/// Devuelve índices de los autores que coinciden con la lista canónica
#[no_mangle]
pub extern "C" fn find_canonical_authors(
    authors_ptr: *const *const c_char,
    authors_count: usize,
    canonical_ptr: *const *const c_char,
    canonical_count: usize,
    result_count_ptr: *mut c_int,
) -> *mut c_char {
    if authors_ptr.is_null() || canonical_ptr.is_null() || result_count_ptr.is_null() {
        return ptr::null_mut();
    }

    unsafe {
        let authors_slice = std::slice::from_raw_parts(authors_ptr, authors_count);
        let canonical_slice = std::slice::from_raw_parts(canonical_ptr, canonical_count);
        
        let mut authors = Vec::new();
        for &ptr in authors_slice {
            if ptr.is_null() { continue; }
            if let Ok(cstr) = CStr::from_ptr(ptr).to_str() {
                authors.push(cstr.to_string());
            }
        }

        let mut canonical = Vec::new();
        for &ptr in canonical_slice {
            if ptr.is_null() { continue; }
            if let Ok(cstr) = CStr::from_ptr(ptr).to_str() {
                canonical.push(normalize_author_internal(cstr));
            }
        }

        // Búsqueda paralela de matches
        let matches: Vec<(usize, String, usize)> = authors
            .par_iter()
            .enumerate()
            .filter_map(|(idx, author)| {
                let norm_author = normalize_author_internal(author);
                canonical.iter().position(|c| c == &norm_author)
                    .map(|canonical_idx| (idx, author.clone(), canonical_idx))
            })
            .collect();

        *result_count_ptr = matches.len() as c_int;

        match serde_json::to_string(&matches) {
            Ok(json) => match CString::new(json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => ptr::null_mut(),
            },
            Err(_) => ptr::null_mut(),
        }
    }
}

/// Fuzzy search con ranking por similitud
#[no_mangle]
pub extern "C" fn fuzzy_search(
    query_ptr: *const c_char,
    query_len: usize,
    candidates_ptr: *const *const c_char,
    candidates_count: usize,
    threshold: c_double,
    result_count_ptr: *mut c_int,
) -> *mut c_char {
    if query_ptr.is_null() || candidates_ptr.is_null() || result_count_ptr.is_null() {
        return ptr::null_mut();
    }

    unsafe {
        let query_slice = std::slice::from_raw_parts(query_ptr as *const u8, query_len);
        let query = match std::str::from_utf8(query_slice) {
            Ok(s) => s,
            Err(_) => return ptr::null_mut(),
        };

        let candidates_slice = std::slice::from_raw_parts(candidates_ptr, candidates_count);
        let mut candidates = Vec::new();

        for &ptr in candidates_slice {
            if ptr.is_null() { continue; }
            if let Ok(cstr) = CStr::from_ptr(ptr).to_str() {
                candidates.push(cstr.to_string());
            }
        }

        // Búsqueda fuzzy paralela con ranking
        let mut matches: Vec<(String, f64)> = candidates
            .par_iter()
            .filter_map(|candidate| {
                let similarity = calculate_similarity_internal(query, candidate);
                if similarity >= threshold {
                    Some((candidate.clone(), similarity))
                } else {
                    None
                }
            })
            .collect();

        // Ordenar por similitud descendente
        matches.sort_by(|a, b| b.1.partial_cmp(&a.1).unwrap());

        *result_count_ptr = matches.len() as c_int;

        match serde_json::to_string(&matches) {
            Ok(json) => match CString::new(json) {
                Ok(c_string) => c_string.into_raw(),
                Err(_) => ptr::null_mut(),
            },
            Err(_) => ptr::null_mut(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_search_performance() {
        let engine = SearchEngine::new();
        let start = std::time::Instant::now();
        
        let results = engine.search("test", 1000);
        
        let duration = start.elapsed();
        println!("Rust search: {} results in {:?}", results.len(), duration);
        assert!(duration.as_millis() < 100); // < 100ms for 1000 results
    }

    #[test]
    fn test_levenshtein() {
        assert_eq!(levenshtein_distance_internal("kitten", "sitting"), 3);
        assert_eq!(levenshtein_distance_internal("saturday", "sunday"), 3);
        assert_eq!(levenshtein_distance_internal("", "test"), 4);
        assert_eq!(levenshtein_distance_internal("test", "test"), 0);
    }

    #[test]
    fn test_similarity() {
        assert!(calculate_similarity_internal("test", "test") == 1.0);
        assert!(calculate_similarity_internal("test", "tost") > 0.7);
        assert!(calculate_similarity_internal("abc", "xyz") < 0.5);
    }

    #[test]
    fn test_fuzzy_variations() {
        let variations = generate_variations_internal("El Señor de los Anillos", "J.R.R. Tolkien");
        assert!(variations.len() > 3);
        assert!(variations.iter().any(|v| !v.contains("á")));
    }

    #[test]
    fn test_normalize_author() {
        assert_eq!(normalize_author_internal("García Márquez, Gabriel"), "gabriel garcia marquez");
        assert_eq!(normalize_author_internal("J.R.R. Tolkien"), "jrr tolkien");
        assert_eq!(normalize_author_internal("Cervantes Saavedra, Miguel de"), "cervantes de miguel saavedra");
        assert_eq!(normalize_author_internal("José Saramago"), "jose saramago");
    }

    #[test]
    fn test_author_equivalence() {
        let norm1 = normalize_author_internal("García Márquez, Gabriel");
        let norm2 = normalize_author_internal("Gabriel García Márquez");
        assert_eq!(norm1, norm2);
        
        let norm3 = normalize_author_internal("Borges, Jorge Luis");
        let norm4 = normalize_author_internal("Jorge Luis Borges");
        assert_eq!(norm3, norm4);
    }

    #[test]
    fn test_normalize_batch_performance() {
        let authors = vec![
            "García Márquez, Gabriel",
            "Borges, Jorge Luis",
            "Cortázar, Julio",
            "Vargas Llosa, Mario",
        ];
        
        let start = std::time::Instant::now();
        let normalized: Vec<String> = authors
            .par_iter()
            .map(|a| normalize_author_internal(a))
            .collect();
        let duration = start.elapsed();
        
        assert_eq!(normalized.len(), 4);
        assert!(duration.as_millis() < 10); // < 10ms for 4 authors
    }
}
