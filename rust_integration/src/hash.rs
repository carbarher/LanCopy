// Fast file hashing using BLAKE3
use blake3;
use rayon::prelude::*;
use std::fs::File;
use std::io::Read;
use std::os::raw::c_int;

const BUFFER_SIZE: usize = 1024 * 1024; // 1MB buffer
const BLAKE3_HASH_SIZE: usize = 32;

/// Hash a single file using BLAKE3
#[no_mangle]
pub extern "C" fn hash_file_blake3(
    path: *const u8,
    path_len: usize,
    hash_output: *mut u8,
) -> c_int {
    if path.is_null() || hash_output.is_null() {
        return -1;
    }
    
    unsafe {
        let path_slice = std::slice::from_raw_parts(path, path_len);
        let path_str = match std::str::from_utf8(path_slice) {
            Ok(s) => s,
            Err(_) => return -2, // Invalid UTF-8
        };
        
        let mut file = match File::open(path_str) {
            Ok(f) => f,
            Err(_) => return -3, // Cannot open file
        };
        
        let mut hasher = blake3::Hasher::new();
        let mut buffer = vec![0u8; BUFFER_SIZE];
        
        loop {
            let n = match file.read(&mut buffer) {
                Ok(0) => break,
                Ok(n) => n,
                Err(_) => return -4, // Read error
            };
            
            hasher.update(&buffer[..n]);
        }
        
        let hash = hasher.finalize();
        std::ptr::copy_nonoverlapping(
            hash.as_bytes().as_ptr(),
            hash_output,
            BLAKE3_HASH_SIZE
        );
        
        0 // Success
    }
}

/// Hash multiple files in parallel using BLAKE3
#[no_mangle]
pub extern "C" fn hash_files_batch(
    paths: *const *const u8,
    path_lens: *const usize,
    count: usize,
    hashes: *mut u8,
) -> c_int {
    if paths.is_null() || path_lens.is_null() || hashes.is_null() {
        return -1;
    }
    
    unsafe {
        // Convert C strings to Rust strings
        let paths_vec: Vec<String> = (0..count)
            .filter_map(|i| {
                let ptr = *paths.add(i);
                let len = *path_lens.add(i);
                let slice = std::slice::from_raw_parts(ptr, len);
                std::str::from_utf8(slice).ok().map(|s| s.to_string())
            })
            .collect();
        
        if paths_vec.len() != count {
            return -2; // Invalid UTF-8 in one or more paths
        }
        
        // Hash files in parallel
        let results: Vec<Option<blake3::Hash>> = paths_vec
            .par_iter()
            .map(|path| {
                let mut file = File::open(path).ok()?;
                let mut hasher = blake3::Hasher::new();
                let mut buffer = vec![0u8; BUFFER_SIZE];
                
                loop {
                    let n = file.read(&mut buffer).ok()?;
                    if n == 0 { break; }
                    hasher.update(&buffer[..n]);
                }
                
                Some(hasher.finalize())
            })
            .collect();
        
        // Copy results to output buffer
        let mut failed = 0;
        for (i, result) in results.iter().enumerate() {
            if let Some(hash) = result {
                std::ptr::copy_nonoverlapping(
                    hash.as_bytes().as_ptr(),
                    hashes.add(i * BLAKE3_HASH_SIZE),
                    BLAKE3_HASH_SIZE
                );
            } else {
                failed += 1;
                // Fill with zeros for failed hashes
                std::ptr::write_bytes(hashes.add(i * BLAKE3_HASH_SIZE), 0, BLAKE3_HASH_SIZE);
            }
        }
        
        if failed > 0 {
            return -(failed as c_int); // Return negative count of failures
        }
        
        0 // Success
    }
}

/// Hash data from memory buffer
#[no_mangle]
pub extern "C" fn hash_buffer_blake3(
    data: *const u8,
    data_len: usize,
    hash_output: *mut u8,
) -> c_int {
    if data.is_null() || hash_output.is_null() {
        return -1;
    }
    
    unsafe {
        let slice = std::slice::from_raw_parts(data, data_len);
        let hash = blake3::hash(slice);
        
        std::ptr::copy_nonoverlapping(
            hash.as_bytes().as_ptr(),
            hash_output,
            BLAKE3_HASH_SIZE
        );
        
        0
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use tempfile::NamedTempFile;

    #[test]
    fn test_hash_buffer() {
        let data = b"Hello, World!";
        let mut hash = [0u8; 32];
        
        let result = hash_buffer_blake3(
            data.as_ptr(),
            data.len(),
            hash.as_mut_ptr()
        );
        
        assert_eq!(result, 0);
        assert_ne!(hash, [0u8; 32]); // Hash should not be all zeros
    }

    #[test]
    fn test_hash_file() {
        let mut temp_file = NamedTempFile::new().unwrap();
        temp_file.write_all(b"Test content").unwrap();
        temp_file.flush().unwrap();
        
        let path = temp_file.path().to_str().unwrap();
        let path_bytes = path.as_bytes();
        let mut hash = [0u8; 32];
        
        let result = hash_file_blake3(
            path_bytes.as_ptr(),
            path_bytes.len(),
            hash.as_mut_ptr()
        );
        
        assert_eq!(result, 0);
        assert_ne!(hash, [0u8; 32]);
    }
}
