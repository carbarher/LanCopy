// Deduplication module using SimHash
use fxhash::hash64;
use rayon::prelude::*;
use std::os::raw::c_int;

#[repr(C)]
pub struct DeduplicationResult {
    pub is_duplicate: bool,
    pub similarity: f64,
    pub original_index: i32,
}

/// Calculate SimHash for a text string
/// SimHash is locality-sensitive: similar texts have similar hashes
pub fn simhash(text: &str) -> u64 {
    let mut hash: u64 = 0;
    let mut weights = [0i32; 64];
    
    // Tokenize and hash each token
    for token in text.split_whitespace() {
        let token_hash = hash64(token.as_bytes());
        
        // Update weights for each bit
        for i in 0..64 {
            if (token_hash >> i) & 1 == 1 {
                weights[i] += 1;
            } else {
                weights[i] -= 1;
            }
        }
    }
    
    // Build final hash from weights
    for i in 0..64 {
        if weights[i] > 0 {
            hash |= 1 << i;
        }
    }
    
    hash
}

/// Calculate Hamming distance between two hashes
/// Hamming distance = number of differing bits
#[inline]
pub fn hamming_distance(hash1: u64, hash2: u64) -> u32 {
    (hash1 ^ hash2).count_ones()
}

/// Calculate similarity from Hamming distance
/// Returns value between 0.0 (completely different) and 1.0 (identical)
#[inline]
pub fn similarity_from_hamming(distance: u32) -> f64 {
    1.0 - (distance as f64 / 64.0)
}

/// Deduplicate a batch of filenames
/// Returns array of DeduplicationResult for each filename
#[no_mangle]
pub extern "C" fn deduplicate_batch(
    filenames: *const *const u8,
    lengths: *const usize,
    count: usize,
    threshold: f64,
    results: *mut DeduplicationResult,
) -> c_int {
    if filenames.is_null() || lengths.is_null() || results.is_null() {
        return -1; // Invalid parameters
    }
    
    unsafe {
        // Convert C strings to Rust strings
        let mut texts = Vec::with_capacity(count);
        for i in 0..count {
            let ptr = *filenames.add(i);
            let len = *lengths.add(i);
            let slice = std::slice::from_raw_parts(ptr, len);
            
            match std::str::from_utf8(slice) {
                Ok(s) => texts.push(s),
                Err(_) => return -2, // Invalid UTF-8
            }
        }
        
        // Calculate hashes in parallel
        let hashes: Vec<u64> = texts.par_iter()
            .map(|text| simhash(text))
            .collect();
        
        // Detect duplicates
        for i in 0..count {
            let mut is_dup = false;
            let mut best_similarity = 0.0;
            let mut original_idx = -1;
            
            // Compare with all previous items
            for j in 0..i {
                let distance = hamming_distance(hashes[i], hashes[j]);
                let similarity = similarity_from_hamming(distance);
                
                if similarity >= threshold && similarity > best_similarity {
                    is_dup = true;
                    best_similarity = similarity;
                    original_idx = j as i32;
                }
            }
            
            *results.add(i) = DeduplicationResult {
                is_duplicate: is_dup,
                similarity: best_similarity,
                original_index: original_idx,
            };
        }
        
        0 // Success
    }
}

/// Calculate SimHash for a single filename
#[no_mangle]
pub extern "C" fn calculate_simhash(
    text: *const u8,
    length: usize,
    hash_out: *mut u64,
) -> c_int {
    if text.is_null() || hash_out.is_null() {
        return -1;
    }
    
    unsafe {
        let slice = std::slice::from_raw_parts(text, length);
        match std::str::from_utf8(slice) {
            Ok(s) => {
                *hash_out = simhash(s);
                0
            }
            Err(_) => -2,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_simhash_identical() {
        let text = "Isaac Asimov Foundation";
        let hash1 = simhash(text);
        let hash2 = simhash(text);
        assert_eq!(hash1, hash2);
    }

    #[test]
    fn test_simhash_similar() {
        let text1 = "Isaac Asimov Foundation";
        let text2 = "Isaac Asimov - Foundation";
        let hash1 = simhash(text1);
        let hash2 = simhash(text2);
        
        let distance = hamming_distance(hash1, hash2);
        let similarity = similarity_from_hamming(distance);
        
        assert!(similarity > 0.8, "Similar texts should have high similarity");
    }

    #[test]
    fn test_simhash_different() {
        let text1 = "Isaac Asimov Foundation";
        let text2 = "Stephen King The Shining";
        let hash1 = simhash(text1);
        let hash2 = simhash(text2);
        
        let distance = hamming_distance(hash1, hash2);
        let similarity = similarity_from_hamming(distance);
        
        assert!(similarity < 0.5, "Different texts should have low similarity");
    }
}
