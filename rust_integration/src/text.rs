// Fast text normalization with compiled regex
use once_cell::sync::Lazy;
use regex::Regex;
use std::os::raw::c_int;

// Pre-compiled regex patterns
static SPECIAL_CHARS: Lazy<Regex> = Lazy::new(|| Regex::new(r"[^\w\s]").unwrap());
static YEAR_PATTERN: Lazy<Regex> = Lazy::new(|| Regex::new(r"[\[\(]\d{4}[\]\)]").unwrap());
static WHITESPACE: Lazy<Regex> = Lazy::new(|| Regex::new(r"\s+").unwrap());

static STOP_WORDS: &[&str] = &[
    "proper", "repack", "internal", "limited", "festival",
    "retail", "dvdrip", "brrip", "bluray", "1080p", "720p", "480p",
    "x264", "x265", "h264", "h265", "hevc",
    "aac", "mp3", "flac", "wav", "ogg",
    "epub", "mobi", "pdf", "azw3",
];

/// Normalize a filename for comparison
/// Removes extension, special chars, stop words, years, etc.
pub fn normalize_filename(input: &str) -> String {
    let mut normalized = input.to_lowercase();
    
    // Remove extension
    if let Some(pos) = normalized.rfind('.') {
        normalized.truncate(pos);
    }
    
    // Remove special characters
    normalized = SPECIAL_CHARS.replace_all(&normalized, " ").to_string();
    
    // Remove stop words
    for word in STOP_WORDS {
        let pattern = format!(r"\b{}\b", regex::escape(word));
        if let Ok(re) = Regex::new(&pattern) {
            normalized = re.replace_all(&normalized, " ").to_string();
        }
    }
    
    // Remove years in brackets/parentheses
    normalized = YEAR_PATTERN.replace_all(&normalized, " ").to_string();
    
    // Normalize whitespace
    normalized = WHITESPACE.replace_all(&normalized, " ").trim().to_string();
    
    normalized
}

/// Normalize filename (C FFI version)
#[no_mangle]
pub extern "C" fn normalize_filename_ffi(
    input: *const u8,
    input_len: usize,
    output: *mut u8,
    output_capacity: usize,
) -> c_int {
    if input.is_null() || output.is_null() {
        return -1;
    }
    
    unsafe {
        let input_slice = std::slice::from_raw_parts(input, input_len);
        let text = match std::str::from_utf8(input_slice) {
            Ok(s) => s,
            Err(_) => return -2, // Invalid UTF-8
        };
        
        let normalized = normalize_filename(text);
        let output_bytes = normalized.as_bytes();
        
        if output_bytes.len() > output_capacity {
            return -3; // Buffer too small
        }
        
        std::ptr::copy_nonoverlapping(
            output_bytes.as_ptr(),
            output,
            output_bytes.len()
        );
        
        output_bytes.len() as c_int
    }
}

/// Calculate string similarity using Jaro-Winkler distance
/// Returns value between 0.0 (completely different) and 1.0 (identical)
pub fn jaro_winkler_similarity(s1: &str, s2: &str) -> f64 {
    if s1 == s2 {
        return 1.0;
    }
    
    if s1.is_empty() || s2.is_empty() {
        return 0.0;
    }
    
    let len1 = s1.len();
    let len2 = s2.len();
    let match_window = (len1.max(len2) / 2).saturating_sub(1);
    
    let s1_chars: Vec<char> = s1.chars().collect();
    let s2_chars: Vec<char> = s2.chars().collect();
    
    let mut s1_matches = vec![false; len1];
    let mut s2_matches = vec![false; len2];
    
    let mut matches = 0;
    
    // Find matches
    for i in 0..len1 {
        let start = i.saturating_sub(match_window);
        let end = (i + match_window + 1).min(len2);
        
        for j in start..end {
            if !s2_matches[j] && s1_chars[i] == s2_chars[j] {
                s1_matches[i] = true;
                s2_matches[j] = true;
                matches += 1;
                break;
            }
        }
    }
    
    if matches == 0 {
        return 0.0;
    }
    
    // Count transpositions
    let mut transpositions = 0;
    let mut k = 0;
    
    for i in 0..len1 {
        if !s1_matches[i] {
            continue;
        }
        
        while !s2_matches[k] {
            k += 1;
        }
        
        if s1_chars[i] != s2_chars[k] {
            transpositions += 1;
        }
        
        k += 1;
    }
    
    let jaro = (matches as f64 / len1 as f64 +
                matches as f64 / len2 as f64 +
                (matches as f64 - transpositions as f64 / 2.0) / matches as f64) / 3.0;
    
    // Winkler modification
    let prefix_len = s1_chars.iter()
        .zip(s2_chars.iter())
        .take(4)
        .take_while(|(a, b)| a == b)
        .count();
    
    jaro + (prefix_len as f64 * 0.1 * (1.0 - jaro))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_normalize_basic() {
        let input = "Isaac Asimov - Foundation (2008).epub";
        let normalized = normalize_filename(input);
        assert_eq!(normalized, "isaac asimov foundation");
    }

    #[test]
    fn test_normalize_stop_words() {
        let input = "Movie.Title.2020.1080p.BluRay.x264.AAC.mkv";
        let normalized = normalize_filename(input);
        assert!(!normalized.contains("1080p"));
        assert!(!normalized.contains("bluray"));
        assert!(!normalized.contains("x264"));
    }

    #[test]
    fn test_jaro_winkler_identical() {
        let similarity = jaro_winkler_similarity("test", "test");
        assert_eq!(similarity, 1.0);
    }

    #[test]
    fn test_jaro_winkler_similar() {
        let similarity = jaro_winkler_similarity("martha", "marhta");
        assert!(similarity > 0.9);
    }

    #[test]
    fn test_jaro_winkler_different() {
        let similarity = jaro_winkler_similarity("hello", "world");
        assert!(similarity < 0.5);
    }
}
