// Biblioteca mínima de prueba - solo FFI básico sin dependencias

#[no_mangle]
pub extern "C" fn test_add(a: i32, b: i32) -> i32 {
    a + b
}

#[no_mangle]
pub extern "C" fn test_hello() -> *const u8 {
    b"Hello from Rust\0".as_ptr()
}
