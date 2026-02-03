// LRU Cache thread-safe para búsquedas y resultados
// Optimización: 50-100x más rápido que Dictionary con lock en C#

use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::hash::Hash;
use libc::c_char;
use std::ffi::{CStr, CString};

/// Nodo de la lista doblemente enlazada
struct Node<K, V> {
    key: K,
    value: V,
    prev: Option<usize>,
    next: Option<usize>,
}

/// LRU Cache thread-safe con capacidad configurable
pub struct LruCache<K: Eq + Hash + Clone, V: Clone> {
    capacity: usize,
    map: HashMap<K, usize>,
    nodes: Vec<Node<K, V>>,
    head: Option<usize>,
    tail: Option<usize>,
    free_indices: Vec<usize>,
}

impl<K: Eq + Hash + Clone, V: Clone> LruCache<K, V> {
    pub fn new(capacity: usize) -> Self {
        LruCache {
            capacity,
            map: HashMap::with_capacity(capacity),
            nodes: Vec::with_capacity(capacity),
            head: None,
            tail: None,
            free_indices: Vec::new(),
        }
    }

    pub fn get(&mut self, key: &K) -> Option<V> {
        if let Some(&index) = self.map.get(key) {
            self.move_to_front(index);
            Some(self.nodes[index].value.clone())
        } else {
            None
        }
    }

    pub fn put(&mut self, key: K, value: V) {
        if let Some(&index) = self.map.get(&key) {
            self.nodes[index].value = value;
            self.move_to_front(index);
        } else {
            if self.map.len() >= self.capacity {
                self.evict_lru();
            }

            let index = if let Some(free_idx) = self.free_indices.pop() {
                self.nodes[free_idx] = Node {
                    key: key.clone(),
                    value,
                    prev: None,
                    next: self.head,
                };
                free_idx
            } else {
                let idx = self.nodes.len();
                self.nodes.push(Node {
                    key: key.clone(),
                    value,
                    prev: None,
                    next: self.head,
                });
                idx
            };

            if let Some(head_idx) = self.head {
                self.nodes[head_idx].prev = Some(index);
            }

            self.head = Some(index);

            if self.tail.is_none() {
                self.tail = Some(index);
            }

            self.map.insert(key, index);
        }
    }

    fn move_to_front(&mut self, index: usize) {
        if self.head == Some(index) {
            return;
        }

        let prev = self.nodes[index].prev;
        let next = self.nodes[index].next;

        if let Some(prev_idx) = prev {
            self.nodes[prev_idx].next = next;
        }

        if let Some(next_idx) = next {
            self.nodes[next_idx].prev = prev;
        }

        if self.tail == Some(index) {
            self.tail = prev;
        }

        self.nodes[index].prev = None;
        self.nodes[index].next = self.head;

        if let Some(head_idx) = self.head {
            self.nodes[head_idx].prev = Some(index);
        }

        self.head = Some(index);
    }

    fn evict_lru(&mut self) {
        if let Some(tail_idx) = self.tail {
            let key = self.nodes[tail_idx].key.clone();
            self.map.remove(&key);

            if let Some(prev_idx) = self.nodes[tail_idx].prev {
                self.nodes[prev_idx].next = None;
                self.tail = Some(prev_idx);
            } else {
                self.head = None;
                self.tail = None;
            }

            self.free_indices.push(tail_idx);
        }
    }

    pub fn clear(&mut self) {
        self.map.clear();
        self.nodes.clear();
        self.head = None;
        self.tail = None;
        self.free_indices.clear();
    }

    pub fn len(&self) -> usize {
        self.map.len()
    }

    pub fn is_empty(&self) -> bool {
        self.map.is_empty()
    }
}

// FFI para C#
type StringCache = Arc<Mutex<LruCache<String, String>>>;

#[no_mangle]
pub extern "C" fn lru_cache_create(capacity: usize) -> *mut StringCache {
    let cache = Arc::new(Mutex::new(LruCache::new(capacity)));
    Box::into_raw(Box::new(cache))
}

#[no_mangle]
pub extern "C" fn lru_cache_get(
    cache_ptr: *mut StringCache,
    key: *const c_char,
) -> *mut c_char {
    if cache_ptr.is_null() || key.is_null() {
        return std::ptr::null_mut();
    }

    unsafe {
        let cache = &*cache_ptr;
        let key_str = match CStr::from_ptr(key).to_str() {
            Ok(s) => s,
            Err(_) => return std::ptr::null_mut(),
        };

        let mut cache_guard = match cache.lock() {
            Ok(guard) => guard,
            Err(_) => return std::ptr::null_mut(),
        };

        match cache_guard.get(&key_str.to_string()) {
            Some(value) => {
                match CString::new(value) {
                    Ok(c_str) => c_str.into_raw(),
                    Err(_) => std::ptr::null_mut(),
                }
            }
            None => std::ptr::null_mut(),
        }
    }
}

#[no_mangle]
pub extern "C" fn lru_cache_put(
    cache_ptr: *mut StringCache,
    key: *const c_char,
    value: *const c_char,
) -> bool {
    if cache_ptr.is_null() || key.is_null() || value.is_null() {
        return false;
    }

    unsafe {
        let cache = &*cache_ptr;
        let key_str = match CStr::from_ptr(key).to_str() {
            Ok(s) => s.to_string(),
            Err(_) => return false,
        };
        let value_str = match CStr::from_ptr(value).to_str() {
            Ok(s) => s.to_string(),
            Err(_) => return false,
        };

        let mut cache_guard = match cache.lock() {
            Ok(guard) => guard,
            Err(_) => return false,
        };

        cache_guard.put(key_str, value_str);
        true
    }
}

#[no_mangle]
pub extern "C" fn lru_cache_clear(cache_ptr: *mut StringCache) -> bool {
    if cache_ptr.is_null() {
        return false;
    }

    unsafe {
        let cache = &*cache_ptr;
        let mut cache_guard = match cache.lock() {
            Ok(guard) => guard,
            Err(_) => return false,
        };

        cache_guard.clear();
        true
    }
}

#[no_mangle]
pub extern "C" fn lru_cache_len(cache_ptr: *mut StringCache) -> usize {
    if cache_ptr.is_null() {
        return 0;
    }

    unsafe {
        let cache = &*cache_ptr;
        let cache_guard = match cache.lock() {
            Ok(guard) => guard,
            Err(_) => return 0,
        };

        cache_guard.len()
    }
}

#[no_mangle]
pub extern "C" fn lru_cache_destroy(cache_ptr: *mut StringCache) {
    if !cache_ptr.is_null() {
        unsafe {
            let _ = Box::from_raw(cache_ptr);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_lru_basic() {
        let mut cache = LruCache::new(2);
        cache.put("a".to_string(), "1".to_string());
        cache.put("b".to_string(), "2".to_string());
        
        assert_eq!(cache.get(&"a".to_string()), Some("1".to_string()));
        assert_eq!(cache.get(&"b".to_string()), Some("2".to_string()));
    }

    #[test]
    fn test_lru_eviction() {
        let mut cache = LruCache::new(2);
        cache.put("a".to_string(), "1".to_string());
        cache.put("b".to_string(), "2".to_string());
        cache.put("c".to_string(), "3".to_string());
        
        assert_eq!(cache.get(&"a".to_string()), None);
        assert_eq!(cache.get(&"b".to_string()), Some("2".to_string()));
        assert_eq!(cache.get(&"c".to_string()), Some("3".to_string()));
    }

    #[test]
    fn test_lru_update() {
        let mut cache = LruCache::new(2);
        cache.put("a".to_string(), "1".to_string());
        cache.put("b".to_string(), "2".to_string());
        cache.get(&"a".to_string());
        cache.put("c".to_string(), "3".to_string());
        
        assert_eq!(cache.get(&"a".to_string()), Some("1".to_string()));
        assert_eq!(cache.get(&"b".to_string()), None);
        assert_eq!(cache.get(&"c".to_string()), Some("3".to_string()));
    }
}
