use rayon::prelude::*;
use serde::{Deserialize, Serialize};
use std::ffi::{CStr, CString};
use std::os::raw::c_char;

#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct SearchResult {
    pub filename: String,
    pub size: i64,
    pub extension: String,
    pub username: String,
    pub quality: i32,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct FilterRequest {
    pub results: Vec<SearchResult>,
    pub min_size: i64,
    pub max_size: i64,
    pub extensions: Vec<String>,
    pub spanish_only: bool,
    pub min_quality: i32,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct FilterResponse {
    pub results: Vec<SearchResult>,
    pub filtered_count: usize,
    pub original_count: usize,
}

pub struct SearchFilter {
    pub min_size: i64,
    pub max_size: i64,
    pub extensions: Vec<String>,
    pub spanish_only: bool,
    pub min_quality: i32,
}

impl SearchFilter {
    pub fn new(min_size: i64, max_size: i64, extensions: Vec<String>, spanish_only: bool, min_quality: i32) -> Self {
        SearchFilter {
            min_size,
            max_size,
            extensions,
            spanish_only,
            min_quality,
        }
    }

    pub fn matches(&self, result: &SearchResult) -> bool {
        // Filtro de tamaño
        if result.size < self.min_size || result.size > self.max_size {
            return false;
        }

        // Filtro de extensión
        if !self.extensions.is_empty() {
            let ext_lower = result.extension.to_lowercase();
            if !self.extensions.iter().any(|e| e.eq_ignore_ascii_case(&ext_lower)) {
                return false;
            }
        }

        // Filtro de calidad
        if result.quality < self.min_quality {
            return false;
        }

        // Filtro de español
        if self.spanish_only && !is_spanish(&result.filename) {
            return false;
        }

        true
    }

    pub fn filter_parallel(&self, results: &[SearchResult]) -> Vec<SearchResult> {
        results
            .par_iter()
            .filter(|r| self.matches(r))
            .cloned()
            .collect()
    }

    pub fn filter_sequential(&self, results: &[SearchResult]) -> Vec<SearchResult> {
        results
            .iter()
            .filter(|r| self.matches(r))
            .cloned()
            .collect()
    }
}

fn is_spanish(text: &str) -> bool {
    // Detección rápida de caracteres españoles
    if text.chars().any(|c| matches!(c, 'á' | 'é' | 'í' | 'ó' | 'ú' | 'ñ' | 'ü' | 'Á' | 'É' | 'Í' | 'Ó' | 'Ú' | 'Ñ' | 'Ü')) {
        return true;
    }

    let lower = text.to_lowercase();
    
    // Palabras clave españolas
    if lower.contains("español") || lower.contains("espanol") || lower.contains("spanish") 
        || lower.contains("castellano") || lower.contains("[esp]") || lower.contains("(esp)")
        || lower.contains("_esp") || lower.contains("-esp") || lower.contains(" esp ")
        || lower.contains(" spa ") || lower.contains("[spa]") || lower.contains("(spa)") {
        return true;
    }

    false
}

// FFI para C#
#[no_mangle]
pub extern "C" fn filter_search_results(json_input: *const c_char) -> *mut c_char {
    let json_str = match unsafe { CStr::from_ptr(json_input).to_str() } {
        Ok(s) => s,
        Err(_) => return std::ptr::null_mut(),
    };

    let request: FilterRequest = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(_) => return std::ptr::null_mut(),
    };

    let original_count = request.results.len();
    
    let filter = SearchFilter::new(
        request.min_size,
        request.max_size,
        request.extensions,
        request.spanish_only,
        request.min_quality,
    );

    // Usar filtrado paralelo si hay muchos resultados
    let filtered = if original_count > 1000 {
        filter.filter_parallel(&request.results)
    } else {
        filter.filter_sequential(&request.results)
    };

    let filtered_count = original_count - filtered.len();

    let response = FilterResponse {
        results: filtered,
        filtered_count,
        original_count,
    };

    match serde_json::to_string(&response) {
        Ok(out_json) => match CString::new(out_json) {
            Ok(c_str) => c_str.into_raw(),
            Err(_) => std::ptr::null_mut(),
        },
        Err(_) => std::ptr::null_mut(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_spanish_detection() {
        assert!(is_spanish("García Márquez"));
        assert!(is_spanish("Español para todos"));
        assert!(is_spanish("libro [ESP]"));
        assert!(!is_spanish("English book"));
    }

    #[test]
    fn test_filter_by_size() {
        let filter = SearchFilter::new(1000, 10000, vec![], false, 0);
        
        let result = SearchResult {
            filename: "test.pdf".to_string(),
            size: 5000,
            extension: "pdf".to_string(),
            username: "user1".to_string(),
            quality: 80,
        };

        assert!(filter.matches(&result));

        let result_too_small = SearchResult {
            filename: "test.pdf".to_string(),
            size: 500,
            extension: "pdf".to_string(),
            username: "user1".to_string(),
            quality: 80,
        };

        assert!(!filter.matches(&result_too_small));
    }

    #[test]
    fn test_filter_by_extension() {
        let filter = SearchFilter::new(0, i64::MAX, vec!["pdf".to_string(), "epub".to_string()], false, 0);
        
        let pdf_result = SearchResult {
            filename: "test.pdf".to_string(),
            size: 5000,
            extension: "pdf".to_string(),
            username: "user1".to_string(),
            quality: 80,
        };

        assert!(filter.matches(&pdf_result));

        let mp3_result = SearchResult {
            filename: "test.mp3".to_string(),
            size: 5000,
            extension: "mp3".to_string(),
            username: "user1".to_string(),
            quality: 80,
        };

        assert!(!filter.matches(&mp3_result));
    }
}
