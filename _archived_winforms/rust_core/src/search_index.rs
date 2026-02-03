use std::collections::HashMap;
use std::sync::{Arc, RwLock};
use serde::{Deserialize, Serialize};

// ==================== ÍNDICE INVERTIDO PARA BÚSQUEDA FULL-TEXT ====================

#[derive(Clone)]
pub struct InvertedIndex {
    index: Arc<RwLock<HashMap<String, Vec<usize>>>>,
    documents: Arc<RwLock<Vec<String>>>,
}

impl InvertedIndex {
    pub fn new() -> Self {
        InvertedIndex {
            index: Arc::new(RwLock::new(HashMap::new())),
            documents: Arc::new(RwLock::new(Vec::new())),
        }
    }

    pub fn add_document(&mut self, doc_id: usize, text: &str) {
        let mut documents = self.documents.write().unwrap();
        
        // Expandir vector si es necesario
        while documents.len() <= doc_id {
            documents.push(String::new());
        }
        documents[doc_id] = text.to_string();
        drop(documents);

        // Tokenizar y agregar al índice
        let tokens = tokenize_for_search(text);
        let mut index = self.index.write().unwrap();
        
        for token in tokens {
            index.entry(token)
                .or_insert_with(Vec::new)
                .push(doc_id);
        }
    }

    pub fn search(&self, query: &str) -> Vec<usize> {
        let tokens = tokenize_for_search(query);
        let index = self.index.read().unwrap();
        
        if tokens.is_empty() {
            return Vec::new();
        }

        // Intersección de resultados (AND logic)
        let mut result_sets: Vec<Vec<usize>> = Vec::new();
        
        for token in tokens {
            if let Some(doc_ids) = index.get(&token) {
                result_sets.push(doc_ids.clone());
            } else {
                return Vec::new(); // Si algún token no existe, no hay resultados
            }
        }

        if result_sets.is_empty() {
            return Vec::new();
        }

        // Intersección
        let mut result = result_sets[0].clone();
        for set in &result_sets[1..] {
            result.retain(|id| set.contains(id));
        }

        result.sort_unstable();
        result.dedup();
        result
    }

    pub fn clear(&mut self) {
        let mut index = self.index.write().unwrap();
        let mut documents = self.documents.write().unwrap();
        
        index.clear();
        documents.clear();
    }
}

fn tokenize_for_search(text: &str) -> Vec<String> {
    text.to_lowercase()
        .split(|c: char| !c.is_alphanumeric())
        .filter(|s| s.len() >= 2) // Ignorar tokens muy cortos
        .map(String::from)
        .collect()
}

// ==================== FFI para C# ====================

use std::sync::Mutex;
use once_cell::sync::Lazy;

static SEARCH_INDEXES: Lazy<Mutex<Vec<InvertedIndex>>> = 
    Lazy::new(|| Mutex::new(Vec::new()));

/// Crea un nuevo índice de búsqueda
/// Retorna ID del índice
#[no_mangle]
pub extern "C" fn create_search_index() -> i32 {
    let mut indexes = SEARCH_INDEXES.lock().unwrap();
    let index = InvertedIndex::new();
    indexes.push(index);
    (indexes.len() - 1) as i32
}

/// Agrega documento al índice
#[no_mangle]
pub extern "C" fn index_add_document(
    index_id: i32,
    doc_id: i32,
    text: *const libc::c_char,
) -> i32 {
    let text_str = unsafe {
        if text.is_null() {
            return 0;
        }
        std::ffi::CStr::from_ptr(text).to_str().unwrap_or("")
    };

    let mut indexes = SEARCH_INDEXES.lock().unwrap();
    
    if let Some(index) = indexes.get_mut(index_id as usize) {
        index.add_document(doc_id as usize, text_str);
        1
    } else {
        0
    }
}

/// Busca en el índice
/// Retorna JSON array de IDs de documentos
#[no_mangle]
pub extern "C" fn index_search(
    index_id: i32,
    query: *const libc::c_char,
) -> *mut libc::c_char {
    let query_str = unsafe {
        if query.is_null() {
            return std::ptr::null_mut();
        }
        std::ffi::CStr::from_ptr(query).to_str().unwrap_or("")
    };

    let indexes = SEARCH_INDEXES.lock().unwrap();
    
    if let Some(index) = indexes.get(index_id as usize) {
        let results = index.search(query_str);
        
        match serde_json::to_string(&results) {
            Ok(json) => std::ffi::CString::new(json).unwrap().into_raw(),
            Err(_) => std::ptr::null_mut(),
        }
    } else {
        std::ptr::null_mut()
    }
}

/// Limpia índice
#[no_mangle]
pub extern "C" fn index_clear(index_id: i32) -> i32 {
    let mut indexes = SEARCH_INDEXES.lock().unwrap();
    
    if let Some(index) = indexes.get_mut(index_id as usize) {
        index.clear();
        1
    } else {
        0
    }
}

/// Destruye índice
#[no_mangle]
pub extern "C" fn index_destroy(index_id: i32) -> i32 {
    let mut indexes = SEARCH_INDEXES.lock().unwrap();
    
    if (index_id as usize) < indexes.len() {
        indexes.remove(index_id as usize);
        1
    } else {
        0
    }
}

// ==================== BÚSQUEDA FUZZY ====================

use std::cmp::min;

/// Búsqueda fuzzy con distancia de Levenshtein
/// Retorna JSON de coincidencias con score
#[no_mangle]
pub extern "C" fn fuzzy_search(
    index_id: i32,
    query: *const libc::c_char,
    max_distance: i32,
) -> *mut libc::c_char {
    let query_str = unsafe {
        if query.is_null() {
            return std::ptr::null_mut();
        }
        std::ffi::CStr::from_ptr(query).to_str().unwrap_or("")
    };

    let indexes = SEARCH_INDEXES.lock().unwrap();
    
    if let Some(index) = indexes.get(index_id as usize) {
        let documents = index.documents.read().unwrap();
        let query_tokens = tokenize_for_search(query_str);
        
        let mut results: Vec<(usize, i32)> = Vec::new();
        let max_distance_u: usize = if max_distance < 0 { 0 } else { max_distance as usize };
        
        for (doc_id, doc_text) in documents.iter().enumerate() {
            let doc_tokens = tokenize_for_search(doc_text);
            
            let mut min_distance: usize = usize::MAX;
            
            for query_token in &query_tokens {
                for doc_token in &doc_tokens {
                    let distance = levenshtein_distance(query_token, doc_token);
                    if distance < min_distance {
                        min_distance = distance;
                    }
                }
            }
            
            if min_distance <= max_distance_u {
                results.push((doc_id, min_distance as i32));
            }
        }
        
        // Ordenar por score (menor distancia = mejor)
        results.sort_by_key(|&(_, dist)| dist);
        
        match serde_json::to_string(&results) {
            Ok(json) => std::ffi::CString::new(json).unwrap().into_raw(),
            Err(_) => std::ptr::null_mut(),
        }
    } else {
        std::ptr::null_mut()
    }
}

fn levenshtein_distance(a: &str, b: &str) -> usize {
    let a_chars: Vec<char> = a.chars().collect();
    let b_chars: Vec<char> = b.chars().collect();
    let a_len = a_chars.len();
    let b_len = b_chars.len();
    
    if a_len == 0 { return b_len; }
    if b_len == 0 { return a_len; }
    
    let mut matrix = vec![vec![0; b_len + 1]; a_len + 1];
    
    for i in 0..=a_len {
        matrix[i][0] = i;
    }
    for j in 0..=b_len {
        matrix[0][j] = j;
    }
    
    for i in 1..=a_len {
        for j in 1..=b_len {
            let cost = if a_chars[i-1] == b_chars[j-1] { 0 } else { 1 };
            
            matrix[i][j] = min(
                min(matrix[i-1][j] + 1, matrix[i][j-1] + 1),
                matrix[i-1][j-1] + cost
            );
        }
    }
    
    matrix[a_len][b_len]
}

// ==================== RANKING DE RESULTADOS (TF-IDF simplificado) ====================

#[derive(Serialize, Deserialize)]
pub struct ScoredResult {
    pub doc_id: usize,
    pub score: f64,
    pub snippet: String,
}

/// Busca y rankea resultados por relevancia
#[no_mangle]
pub extern "C" fn ranked_search(
    index_id: i32,
    query: *const libc::c_char,
    top_n: i32,
) -> *mut libc::c_char {
    let query_str = unsafe {
        if query.is_null() {
            return std::ptr::null_mut();
        }
        std::ffi::CStr::from_ptr(query).to_str().unwrap_or("")
    };

    let indexes = SEARCH_INDEXES.lock().unwrap();
    
    if let Some(index) = indexes.get(index_id as usize) {
        let doc_ids = index.search(query_str);
        let documents = index.documents.read().unwrap();
        
        let query_tokens = tokenize_for_search(query_str);
        let mut scored_results: Vec<ScoredResult> = Vec::new();
        
        for doc_id in doc_ids {
            if doc_id >= documents.len() {
                continue;
            }
            
            let doc_text = &documents[doc_id];
            let doc_tokens = tokenize_for_search(doc_text);
            
            // Calcular score simple (TF)
            let mut score = 0.0;
            for query_token in &query_tokens {
                let tf = doc_tokens.iter().filter(|t| *t == query_token).count() as f64;
                score += tf;
            }
            
            // Snippet (primeros 100 chars)
            let snippet = if doc_text.len() > 100 {
                format!("{}...", &doc_text[..100])
            } else {
                doc_text.clone()
            };
            
            scored_results.push(ScoredResult {
                doc_id,
                score,
                snippet,
            });
        }
        
        // Ordenar por score descendente
        scored_results.sort_by(|a, b| b.score.partial_cmp(&a.score).unwrap());
        
        // Tomar top N
        scored_results.truncate(top_n as usize);
        
        match serde_json::to_string(&scored_results) {
            Ok(json) => std::ffi::CString::new(json).unwrap().into_raw(),
            Err(_) => std::ptr::null_mut(),
        }
    } else {
        std::ptr::null_mut()
    }
}
