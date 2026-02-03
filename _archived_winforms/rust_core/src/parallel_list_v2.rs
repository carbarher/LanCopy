// API FFI thread-safe usando Worker Pool
// Versión 2: Sin race conditions, compatible con paralelismo de C#
// API síncrona: C# llama → Rust procesa en worker pool → C# recibe resultado

use libc::c_char;
use std::ffi::CStr;
use std::sync::atomic::{AtomicUsize, Ordering};
use crate::worker_pool::{get_pool, Task, TaskResult};

// Contador global de tareas para IDs únicos
static TASK_COUNTER: AtomicUsize = AtomicUsize::new(0);

/// Ordena una lista de strings usando worker pool (thread-safe)
/// API síncrona: envía tarea y espera resultado
#[no_mangle]
pub extern "C" fn parallel_sort_strings_v2(
    strings: *const *const c_char,
    count: usize,
    out_buffer: *mut *mut u8,
    out_size: *mut usize,
) -> bool {
    if strings.is_null() || count == 0 || out_buffer.is_null() || out_size.is_null() {
        return false;
    }

    unsafe {
        let input_slice = std::slice::from_raw_parts(strings, count);
        let mut string_vec: Vec<String> = Vec::with_capacity(count);
        
        for &ptr in input_slice {
            if ptr.is_null() {
                continue;
            }
            if let Ok(s) = CStr::from_ptr(ptr).to_str() {
                string_vec.push(s.to_string());
            }
        }

        // Enviar tarea al pool
        let task_id = TASK_COUNTER.fetch_add(1, Ordering::SeqCst);
        let task = Task::Sort(string_vec);
        
        if get_pool().submit(task_id, task).is_err() {
            return false;
        }
        
        // Esperar resultado
        let (received_id, result) = match get_pool().receive() {
            Ok(r) => r,
            Err(_) => return false,
        };
        
        if received_id != task_id {
            return false;
        }
        
        // Serializar resultado
        let strings = match result {
            TaskResult::Sort(s) => s,
            _ => return false,
        };
        
        serialize_result(strings, out_buffer, out_size)
    }
}

/// Elimina duplicados usando worker pool (thread-safe)
#[no_mangle]
pub extern "C" fn parallel_distinct_strings_v2(
    strings: *const *const c_char,
    count: usize,
    out_buffer: *mut *mut u8,
    out_size: *mut usize,
) -> bool {
    if strings.is_null() || count == 0 || out_buffer.is_null() || out_size.is_null() {
        return false;
    }

    unsafe {
        let input_slice = std::slice::from_raw_parts(strings, count);
        let mut string_vec: Vec<String> = Vec::with_capacity(count);
        
        for &ptr in input_slice {
            if ptr.is_null() {
                continue;
            }
            if let Ok(s) = CStr::from_ptr(ptr).to_str() {
                string_vec.push(s.to_string());
            }
        }

        let task_id = TASK_COUNTER.fetch_add(1, Ordering::SeqCst);
        let task = Task::Distinct(string_vec);
        
        if get_pool().submit(task_id, task).is_err() {
            return false;
        }
        
        let (received_id, result) = match get_pool().receive() {
            Ok(r) => r,
            Err(_) => return false,
        };
        
        if received_id != task_id {
            return false;
        }
        
        let strings = match result {
            TaskResult::Distinct(s) => s,
            _ => return false,
        };
        
        serialize_result(strings, out_buffer, out_size)
    }
}

/// Filtra una lista usando worker pool (thread-safe)
#[no_mangle]
pub extern "C" fn parallel_filter_strings_v2(
    strings: *const *const c_char,
    count: usize,
    pattern: *const c_char,
    case_sensitive: bool,
    out_buffer: *mut *mut u8,
    out_size: *mut usize,
) -> bool {
    if strings.is_null() || pattern.is_null() || count == 0 || out_buffer.is_null() || out_size.is_null() {
        return false;
    }

    unsafe {
        let input_slice = std::slice::from_raw_parts(strings, count);
        let pattern_str = match CStr::from_ptr(pattern).to_str() {
            Ok(s) => s.to_string(),
            Err(_) => return false,
        };
        
        let mut string_vec: Vec<String> = Vec::with_capacity(count);
        for &ptr in input_slice {
            if ptr.is_null() {
                continue;
            }
            if let Ok(s) = CStr::from_ptr(ptr).to_str() {
                string_vec.push(s.to_string());
            }
        }

        let task_id = TASK_COUNTER.fetch_add(1, Ordering::SeqCst);
        let task = Task::Filter {
            items: string_vec,
            pattern: pattern_str,
            case_sensitive,
        };
        
        if get_pool().submit(task_id, task).is_err() {
            return false;
        }
        
        let (received_id, result) = match get_pool().receive() {
            Ok(r) => r,
            Err(_) => return false,
        };
        
        if received_id != task_id {
            return false;
        }
        
        let strings = match result {
            TaskResult::Filter(s) => s,
            _ => return false,
        };
        
        serialize_result(strings, out_buffer, out_size)
    }
}

/// Serializa un vector de strings al formato de buffer
fn serialize_result(
    strings: Vec<String>,
    out_buffer: *mut *mut u8,
    out_size: *mut usize,
) -> bool {
    let mut buffer = Vec::new();
    buffer.extend_from_slice(&(strings.len() as u32).to_le_bytes());
    
    for s in &strings {
        let bytes = s.as_bytes();
        buffer.extend_from_slice(&(bytes.len() as u32).to_le_bytes());
        buffer.extend_from_slice(bytes);
    }

    unsafe {
        let buffer_len = buffer.len();
        let buffer_box = Box::new(buffer);
        *out_size = buffer_len;
        *out_buffer = Box::leak(buffer_box).as_mut_ptr();
    }

    true
}

/// Libera un buffer alocado por Rust
#[no_mangle]
pub extern "C" fn free_rust_buffer_v2(ptr: *mut u8, size: usize) {
    if !ptr.is_null() && size > 0 {
        unsafe {
            let _ = Vec::from_raw_parts(ptr, size, size);
        }
    }
}
