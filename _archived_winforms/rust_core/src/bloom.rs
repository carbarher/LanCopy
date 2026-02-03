use probabilistic_collections::bloom::BloomFilter as ProbBloomFilter;
use std::ffi::CStr;
use std::os::raw::c_char;
use std::hash::{Hash, Hasher};
use std::collections::hash_map::DefaultHasher;

/// Bloom filter para detección rápida de duplicados
/// 1M archivos en ~1.2MB RAM con 0.01% falsos positivos
pub struct BloomFilter {
    filter: ProbBloomFilter<u64>,
}

impl BloomFilter {
    /// Crear Bloom filter con capacidad estimada y tasa de falsos positivos
    pub fn new(expected_items: usize, false_positive_rate: f64) -> Self {
        let filter = ProbBloomFilter::new(expected_items, false_positive_rate);
        BloomFilter { filter }
    }

    /// Insertar hash en el filtro
    pub fn insert(&mut self, hash: u64) {
        self.filter.insert(&hash);
    }

    /// Verificar si un hash probablemente existe
    pub fn contains(&self, hash: u64) -> bool {
        self.filter.contains(&hash)
    }

    /// Insertar string (calcula hash automáticamente)
    pub fn insert_string(&mut self, s: &str) {
        let hash = Self::hash_string(s);
        self.insert(hash);
    }

    /// Verificar si un string probablemente existe
    pub fn contains_string(&self, s: &str) -> bool {
        let hash = Self::hash_string(s);
        self.contains(hash)
    }

    fn hash_string(s: &str) -> u64 {
        let mut hasher = DefaultHasher::new();
        s.hash(&mut hasher);
        hasher.finish()
    }

    /// Obtener número de items insertados (aproximado)
    pub fn len(&self) -> usize {
        self.filter.len()
    }

    /// Limpiar el filtro
    pub fn clear(&mut self) {
        self.filter.clear();
    }
}

// FFI para C#

#[no_mangle]
pub extern "C" fn bloom_create(expected_items: usize, false_positive_rate: f64) -> *mut BloomFilter {
    let filter = BloomFilter::new(expected_items, false_positive_rate);
    Box::into_raw(Box::new(filter))
}

#[no_mangle]
pub extern "C" fn bloom_insert(filter: *mut BloomFilter, hash: u64) {
    if filter.is_null() {
        return;
    }
    unsafe {
        (*filter).insert(hash);
    }
}

#[no_mangle]
pub extern "C" fn bloom_insert_string(filter: *mut BloomFilter, s: *const c_char) -> i32 {
    if filter.is_null() || s.is_null() {
        return -1;
    }

    unsafe {
        let str_slice = match CStr::from_ptr(s).to_str() {
            Ok(s) => s,
            Err(_) => return -2,
        };

        (*filter).insert_string(str_slice);
        0
    }
}

#[no_mangle]
pub extern "C" fn bloom_contains(filter: *const BloomFilter, hash: u64) -> bool {
    if filter.is_null() {
        return false;
    }
    unsafe { (*filter).contains(hash) }
}

#[no_mangle]
pub extern "C" fn bloom_contains_string(filter: *const BloomFilter, s: *const c_char) -> i32 {
    if filter.is_null() || s.is_null() {
        return -1;
    }

    unsafe {
        let str_slice = match CStr::from_ptr(s).to_str() {
            Ok(s) => s,
            Err(_) => return -2,
        };

        if (*filter).contains_string(str_slice) {
            1 // Probablemente existe
        } else {
            0 // Definitivamente NO existe
        }
    }
}

#[no_mangle]
pub extern "C" fn bloom_len(filter: *const BloomFilter) -> usize {
    if filter.is_null() {
        return 0;
    }
    unsafe { (*filter).len() }
}

#[no_mangle]
pub extern "C" fn bloom_clear(filter: *mut BloomFilter) {
    if filter.is_null() {
        return;
    }
    unsafe {
        (*filter).clear();
    }
}

#[no_mangle]
pub extern "C" fn bloom_destroy(filter: *mut BloomFilter) {
    if filter.is_null() {
        return;
    }
    unsafe {
        let _ = Box::from_raw(filter);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bloom_filter() {
        let mut filter = BloomFilter::new(1000, 0.01);
        
        filter.insert_string("Cervantes - Don Quijote.epub");
        filter.insert_string("Shakespeare - Hamlet.pdf");
        
        assert!(filter.contains_string("Cervantes - Don Quijote.epub"));
        assert!(filter.contains_string("Shakespeare - Hamlet.pdf"));
        assert!(!filter.contains_string("Tolkien - LOTR.epub"));
    }

    #[test]
    fn test_false_positive_rate() {
        let mut filter = BloomFilter::new(10000, 0.01);
        
        // Insertar 10k items
        for i in 0..10000 {
            filter.insert_string(&format!("file_{}.txt", i));
        }

        // Verificar falsos positivos
        let mut false_positives = 0;
        for i in 10000..20000 {
            if filter.contains_string(&format!("file_{}.txt", i)) {
                false_positives += 1;
            }
        }

        let fp_rate = false_positives as f64 / 10000.0;
        assert!(fp_rate < 0.02); // Debe ser < 2%
    }
}
