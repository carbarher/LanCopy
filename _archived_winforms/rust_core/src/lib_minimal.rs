// Biblioteca mínima de prueba para verificar generación de DLL

#[no_mangle]
pub extern "C" fn test_function() -> i32 {
    42
}

#[no_mangle]
pub extern "C" fn add_numbers(a: i32, b: i32) -> i32 {
    a + b
}
