use std::os::raw::c_char;
use std::ffi::CString;

#[no_mangle]
pub extern "C" fn test_function() -> *mut c_char {
    let msg = CString::new("Hello from Rust DLL!").unwrap();
    msg.into_raw()
}

#[no_mangle]
pub extern "C" fn free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}
