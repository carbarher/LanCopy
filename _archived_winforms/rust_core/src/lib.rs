// Módulos de optimización Rust para SlskDown
pub mod bloom;             // Bloom filter
pub mod advanced_features; // Sort/filter/dedupe/normalize/compress + benchmarks
pub mod file_operations;   // Encoding/validación/metadatos/etc.
pub mod search_index;      // Índice invertido + FFI
pub mod search_filter;     // Filtrado JSON + FFI
pub mod search;            // Motor de búsqueda full-text con Tantivy
pub mod language_detection; // Detección de idioma español
pub mod metadata;          // Metadata MP3/FLAC y detección de idioma mejorada
pub mod lru_cache;         // LRU cache thread-safe (50-100x más rápido que C#)
pub mod parallel_list;     // Procesamiento paralelo de listas (5-10x más rápido) - DEPRECATED
pub mod parallel_list_v2;  // Procesamiento paralelo V2 con Worker Pool (thread-safe)
pub mod worker_pool;       // Worker Pool para procesamiento asíncrono thread-safe
pub mod id3_parser;        // Parser ID3v2 optimizado (100-500x más rápido)

use libc::c_char;
use std::ffi::CString;

#[no_mangle]
pub extern "C" fn free_rust_string(ptr: *mut c_char) {
    if ptr.is_null() {
        return;
    }
    unsafe {
        let _ = CString::from_raw(ptr);
    }
}

#[no_mangle]
pub extern "C" fn free_string(ptr: *mut c_char) {
    free_rust_string(ptr);
}

#[no_mangle]
pub extern "C" fn free_compressed_data(ptr: *mut u8, len: usize) {
    if ptr.is_null() || len == 0 {
        return;
    }
    unsafe {
        let slice = std::ptr::slice_from_raw_parts_mut(ptr, len);
        let _ = Box::from_raw(slice);
    }
}
