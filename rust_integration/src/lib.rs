// SlskDown Core - High-performance components in Rust
// Exposes C FFI for .NET interop

use std::ffi::CString;
use std::os::raw::{c_char, c_int};

mod dedup;
mod hash;
mod text;

pub use dedup::*;
pub use hash::*;
pub use text::*;

/// Initialize the Rust library
#[no_mangle]
pub extern "C" fn slskdown_init() -> c_int {
    // Initialize logging, thread pools, etc.
    rayon::ThreadPoolBuilder::new()
        .num_threads(num_cpus::get())
        .build_global()
        .ok();
    
    0 // Success
}

/// Get library version
#[no_mangle]
pub extern "C" fn slskdown_version() -> *const c_char {
    let version = CString::new(env!("CARGO_PKG_VERSION")).unwrap();
    version.into_raw()
}

/// Free a string allocated by Rust
#[no_mangle]
pub extern "C" fn slskdown_free_string(s: *mut c_char) {
    if !s.is_null() {
        unsafe {
            let _ = CString::from_raw(s);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_init() {
        assert_eq!(slskdown_init(), 0);
    }
}
