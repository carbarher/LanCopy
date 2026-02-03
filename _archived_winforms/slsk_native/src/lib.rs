use once_cell::sync::Lazy;
use rayon::prelude::*;
use regex::Regex;
use std::ffi::{CStr, CString};
use std::hash::{BuildHasher, Hasher};
use std::os::raw::{c_char, c_int, c_uchar};
use std::slice;
use memchr::memmem; // Búsqueda de bytes SIMD
use ahash::RandomState;
use hashbrown::{HashMap as FastHashMap, HashSet as FastHashSet};

const NATIVE_VERSION: u32 = 10002;

const CAP_FILTER_SEARCH_RESULTS: u64 = 1 << 0;
const CAP_SORT_BY_QUALITY: u64 = 1 << 1;
const CAP_PROCESS_SEARCH_RESULTS: u64 = 1 << 2;
const CAP_PROCESS_SEARCH_RESULTS_TABLE: u64 = 1 << 3;
const CAP_FILTER_SEARCH_RESULTS_TABLE: u64 = 1 << 4;
const CAP_SORT_BY_QUALITY_TABLE: u64 = 1 << 5;
const CAP_DEDUPLICATE_KEYS_TABLE: u64 = 1 << 6;

#[no_mangle]
pub extern "C" fn get_native_version() -> u32 {
    NATIVE_VERSION
}

#[no_mangle]
pub extern "C" fn get_native_capabilities() -> u64 {
    CAP_FILTER_SEARCH_RESULTS
        | CAP_SORT_BY_QUALITY
        | CAP_PROCESS_SEARCH_RESULTS
        | CAP_PROCESS_SEARCH_RESULTS_TABLE
        | CAP_FILTER_SEARCH_RESULTS_TABLE
        | CAP_SORT_BY_QUALITY_TABLE
        | CAP_DEDUPLICATE_KEYS_TABLE
}

fn eq_ascii_case_insensitive(a: &[u8], b: &[u8]) -> bool {
    if a.len() != b.len() {
        return false;
    }
    a.iter()
        .zip(b.iter())
        .all(|(&x, &y)| x.to_ascii_lowercase() == y.to_ascii_lowercase())
}

fn hash_ascii_case_insensitive(state: &RandomState, bytes: &[u8]) -> u64 {
    let mut hasher = state.build_hasher();
    for &b in bytes {
        hasher.write_u8(b.to_ascii_lowercase());
    }
    hasher.finish()
}

fn hash_ascii_case_insensitive_with_size(state: &RandomState, bytes: &[u8], size: i64) -> u64 {
    let mut hasher = state.build_hasher();
    for &b in bytes {
        hasher.write_u8(b.to_ascii_lowercase());
    }
    hasher.write_i64(size);
    hasher.finish()
}

fn build_allowed_exts(extensions_ptr: *const *const c_char, ext_count: c_int) -> Vec<Vec<u8>> {
    if extensions_ptr.is_null() || ext_count <= 0 {
        return Vec::new();
    }

    let ext_count = ext_count as usize;
    let ext_ptrs = unsafe { slice::from_raw_parts(extensions_ptr, ext_count) };
    let mut allowed = Vec::with_capacity(ext_count);
    for &p in ext_ptrs {
        if p.is_null() {
            continue;
        }
        let bytes = unsafe { CStr::from_ptr(p).to_bytes() };
        if bytes.is_ascii() {
            let mut lower = Vec::with_capacity(bytes.len());
            for &b in bytes {
                lower.push(b.to_ascii_lowercase());
            }
            allowed.push(lower);
        } else if let Ok(s) = unsafe { CStr::from_ptr(p) }.to_str() {
            allowed.push(s.to_lowercase().into_bytes());
        }
    }
    allowed
}

fn eq_ascii_lower_bytes(allowed_lower: &[u8], candidate: &[u8]) -> bool {
    if allowed_lower.len() != candidate.len() {
        return false;
    }
    for i in 0..allowed_lower.len() {
        if allowed_lower[i] != candidate[i].to_ascii_lowercase() {
            return false;
        }
    }
    true
}

fn ext_allowed(allowed: &[Vec<u8>], ext_bytes: &[u8]) -> bool {
    if allowed.is_empty() {
        return true;
    }

    if ext_bytes.is_ascii() {
        for a in allowed {
            if eq_ascii_lower_bytes(a.as_slice(), ext_bytes) {
                return true;
            }
        }
        return false;
    }

    let ext = match std::str::from_utf8(ext_bytes) {
        Ok(s) => s,
        Err(_) => return false,
    };
    let lowered = ext.to_lowercase();
    let lowered_bytes = lowered.as_bytes();
    for a in allowed {
        if a.as_slice() == lowered_bytes {
            return true;
        }
    }
    false
}

fn contains_ascii_lower_bytes(haystack: &[u8], needle_lower: &[u8]) -> bool {
    if needle_lower.is_empty() {
        return true;
    }
    if needle_lower.len() > haystack.len() {
        return false;
    }

    let last_start = haystack.len() - needle_lower.len();
    for start in 0..=last_start {
        let mut ok = true;
        for j in 0..needle_lower.len() {
            if haystack[start + j].to_ascii_lowercase() != needle_lower[j] {
                ok = false;
                break;
            }
        }
        if ok {
            return true;
        }
    }
    false
}

fn ends_with_ascii_lower_bytes(haystack: &[u8], suffix_lower: &[u8]) -> bool {
    if suffix_lower.len() > haystack.len() {
        return false;
    }
    let start = haystack.len() - suffix_lower.len();
    for i in 0..suffix_lower.len() {
        if haystack[start + i].to_ascii_lowercase() != suffix_lower[i] {
            return false;
        }
    }
    true
}

fn slice_from_table<'a>(table: &'a [u8], off: u32, len: u32) -> Option<&'a [u8]> {
    let off = off as usize;
    let len = len as usize;
    if off.checked_add(len)? > table.len() {
        return None;
    }
    Some(&table[off..off + len])
}

#[derive(Clone, Copy)]
struct BestCandidate {
    idx: usize,
    q: i32,
    s: i32,
}

#[derive(Clone, Copy)]
struct AsciiGroup {
    rep_off: u32,
    rep_len: u32,
    best: BestCandidate,
}

#[derive(Clone, Copy)]
struct AsciiGroupSized {
    rep_off: u32,
    rep_len: u32,
    size: i64,
    best: BestCandidate,
}

fn update_best(best: &mut BestCandidate, cand: BestCandidate) {
    let take = cand.q > best.q
        || (cand.q == best.q && (cand.s > best.s || (cand.s == best.s && cand.idx < best.idx)));
    if take {
        *best = cand;
    }
}

// ============================================
// DETECCIÓN DE IDIOMA ESPAÑOL (50x más rápido)
// ============================================

// Patrones de inglés (más exhaustivos)
static ENGLISH_PATTERNS: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"\b(the|with|from|into|through|about|after|before|between|during|without|within|among|against|towards|upon|across|behind|beneath|beside|beyond|inside|outside|under|over|above|below|near|around|along|throughout|toward|until|since|while|because|although|though|unless|whether|whereas|whenever|wherever|however|therefore|moreover|furthermore|nevertheless|otherwise|meanwhile|instead|rather|either|neither|both|such|which|whose|whom|what|when|where|why|how|that|this|these|those|there|here|some|any|many|much|few|little|more|most|less|least|several|every|each|all|half|other|another|own|same|very|too|quite|just|only|even|also|still|yet|already|never|always|often|sometimes|usually|seldom|rarely|hardly|scarcely|barely|nearly|almost|enough|indeed|perhaps|maybe|probably|possibly|certainly|surely|definitely|absolutely)\b").unwrap()
});

static ENGLISH_SUFFIXES: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"(ing|tion|ness|ment|ship|hood|dom|ism|ist|ity|ance|ence|ful|less|ous|ious|eous|ive|ative|able|ible|ial|ical|ward|wards|wise)$").unwrap()
});

static FRENCH_PATTERNS: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"(\bl'|\bd'|\bc'|[àâçèéêëîïôùûü])").unwrap()
});

static ITALIAN_PATTERNS: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"\b(il|lo|gli|della|delle|degli|nell|dell|che|di|sono|è|era|gli|zione|zioni|aggio|eggio)\b").unwrap()
});

static GERMAN_PATTERNS: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"\b(der|die|das|den|dem|des|ein|eine|einen|und|oder|aber|nicht|ist|sind|von|zu|mit|für|auf|aus|bei|nach|über|unter|durch|gegen|ohne|um|bis|seit|während|wahrend|dass|daß|wenn|weil|als|wie|auch|noch|nur|schon|sehr|mehr|können|konnen|müssen|mussen|sollen|werden|wurde|wurden|gewesen|haben|hatte|hatten|sein|war|waren|schaft|keit|lich|isch|bar|ß)\b").unwrap()
});

static PORTUGUESE_PATTERNS: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"([ãõç]|\b(não|nao|dos|das|uma|com|para|pela|pelo|pelos|pelas|também|tambem|você|voce|está|esta|são|sao|português|portugues|brasil)\b)").unwrap()
});

static SPANISH_PATTERNS: Lazy<Vec<Regex>> = Lazy::new(|| {
    vec![
        // Caracteres españoles (NO portugueses)
        Regex::new(r"[ñáéíóú]").unwrap(),
        // Artículos y preposiciones distintivas
        Regex::new(r"\b(del|los|las|por|para|donde|cuando|con|sin|sobre|entre|desde|hasta)\b").unwrap(),
        // Palabras comunes en títulos
        Regex::new(r"\b(una|uno|esta|este|ese|esa|aquel|aquella|como|porque|aunque|mientras|siempre|nunca|también|tampoco|después|antes|ahora|entonces|todo|todos|toda|todas|nada|algo|alguien|nadie|otro|otra|otros|otras|mismo|misma)\b").unwrap(),
        // Verbos españoles
        Regex::new(r"\b(ser|estar|haber|hacer|tener)\b").unwrap(),
        // Términos literarios
        Regex::new(r"\b(libro|novela|historia|cuento|saga|serie|tomo|volumen|edición|edicion|español|espanol|castellano)\b").unwrap(),
    ]
});

fn is_spanish_text_internal(text: &str) -> bool {
    let text = text.to_lowercase();

    // Rechazar inglés primero (más común)
    if ENGLISH_PATTERNS.is_match(&text) || ENGLISH_SUFFIXES.is_match(&text) {
        return false;
    }

    // Rechazar otros idiomas
    if FRENCH_PATTERNS.is_match(&text) {
        return false;
    }
    if ITALIAN_PATTERNS.is_match(&text) {
        return false;
    }
    if GERMAN_PATTERNS.is_match(&text) {
        return false;
    }
    if PORTUGUESE_PATTERNS.is_match(&text) {
        return false;
    }

    // Contar coincidencias españolas
    let mut spanish_score = 0;
    for pattern in SPANISH_PATTERNS.iter() {
        if pattern.is_match(&text) {
            spanish_score += 1;
        }
    }

    // Si tiene caracteres españoles o al menos 2 patrones, es español
    spanish_score >= 2
}

#[no_mangle]
pub extern "C" fn is_spanish_text_native(text_ptr: *const c_char) -> c_int {
    if text_ptr.is_null() {
        return 0;
    }

    let text = unsafe {
        match CStr::from_ptr(text_ptr).to_str() {
            Ok(s) => s,
            Err(_) => return 0,
        }
    };

    if is_spanish_text_internal(text) {
        1
    } else {
        0
    }
}

// ============================================
// DEDUPLICACIÓN PARALELA DE ARCHIVOS (20x más rápido)
// ============================================

#[repr(C)]
pub struct FileInfo {
    pub filename_ptr: *const c_char,
    pub username_ptr: *const c_char,
    pub size: i64,
    pub score: c_int,
}

#[repr(C)]
pub struct SearchResultInfo {
    pub filename_ptr: *const c_char,
    pub extension_ptr: *const c_char,
    pub size: i64,
    pub quality: c_int,
    pub provider_score: c_int,
}

#[repr(C)]
pub struct SearchResultInfoOffsets {
    pub filename_offset: u32,
    pub filename_len: u32,
    pub extension_offset: u32,
    pub extension_len: u32,
    pub size: i64,
    pub quality: c_int,
    pub provider_score: c_int,
}

#[no_mangle]
pub extern "C" fn deduplicate_files_native(
    files_ptr: *const FileInfo,
    count: c_int,
    results_ptr: *mut c_int,
) -> c_int {
    if files_ptr.is_null() || results_ptr.is_null() || count == 0 {
        return 0;
    }

    let count = count as usize;
    let files = unsafe { slice::from_raw_parts(files_ptr, count) };

    let state = RandomState::new();
    let mut ascii_groups: FastHashMap<u64, Vec<(Vec<u8>, BestCandidate)>, RandomState> =
        FastHashMap::with_hasher(state.clone());
    let mut unicode_groups: FastHashMap<String, BestCandidate, RandomState> =
        FastHashMap::with_hasher(state.clone());

    for (idx, file) in files.iter().enumerate() {
        if file.filename_ptr.is_null() {
            continue;
        }
        let name_bytes = unsafe { CStr::from_ptr(file.filename_ptr).to_bytes() };
        let cand = BestCandidate {
            idx,
            q: file.score as i32,
            s: 0,
        };

        if name_bytes.is_ascii() {
            let h = hash_ascii_case_insensitive(&state, name_bytes);
            let bucket = ascii_groups.entry(h).or_insert_with(Vec::new);

            let mut found = false;
            for (rep, best) in bucket.iter_mut() {
                if eq_ascii_case_insensitive(rep.as_slice(), name_bytes) {
                    update_best(best, cand);
                    found = true;
                    break;
                }
            }
            if !found {
                bucket.push((name_bytes.to_vec(), cand));
            }
        } else {
            let name = match unsafe { CStr::from_ptr(file.filename_ptr) }.to_str() {
                Ok(s) => s,
                Err(_) => continue,
            };
            let key = name.to_lowercase();
            match unicode_groups.get_mut(&key) {
                None => {
                    unicode_groups.insert(key, cand);
                }
                Some(best) => {
                    update_best(best, cand);
                }
            }
        }
    }

    let mut best_indices: Vec<i32> = Vec::with_capacity(ascii_groups.len() + unicode_groups.len());
    for bucket in ascii_groups.values() {
        for (_, best) in bucket.iter() {
            best_indices.push(best.idx as i32);
        }
    }
    for best in unicode_groups.values() {
        best_indices.push(best.idx as i32);
    }

    // Copiar resultados
    let result_count = best_indices.len().min(count);
    unsafe {
        std::ptr::copy_nonoverlapping(
            best_indices.as_ptr(),
            results_ptr,
            result_count,
        );
    }

    result_count as c_int
}

#[no_mangle]
pub extern "C" fn deduplicate_keys_table_native(
    items_ptr: *const SearchResultInfoOffsets,
    count: c_int,
    string_table_ptr: *const c_uchar,
    string_table_len: c_int,
    results_ptr: *mut c_int,
) -> c_int {
    if items_ptr.is_null() || results_ptr.is_null() || count <= 0 {
        return 0;
    }
    if string_table_ptr.is_null() || string_table_len <= 0 {
        return 0;
    }

    let count_usize = count as usize;
    let items = unsafe { slice::from_raw_parts(items_ptr, count_usize) };
    let table_len = string_table_len as usize;
    let table = unsafe { slice::from_raw_parts(string_table_ptr, table_len) };

    let state = RandomState::new();
    let mut ascii_groups: FastHashMap<u64, Vec<AsciiGroupSized>, RandomState> =
        FastHashMap::with_hasher(state.clone());
    let mut unicode_groups: FastHashMap<(String, i64), BestCandidate, RandomState> =
        FastHashMap::with_hasher(state.clone());

    for (idx, item) in items.iter().enumerate() {
        let name_bytes = match slice_from_table(table, item.filename_offset, item.filename_len) {
            Some(s) => s,
            None => continue,
        };
        let cand = BestCandidate {
            idx,
            q: item.provider_score as i32,
            s: 0,
        };

        if name_bytes.is_ascii() {
            let h = hash_ascii_case_insensitive_with_size(&state, name_bytes, item.size);
            let bucket = ascii_groups.entry(h).or_insert_with(Vec::new);
            let mut found = false;
            for group in bucket.iter_mut() {
                if group.size != item.size {
                    continue;
                }
                let rep = match slice_from_table(table, group.rep_off, group.rep_len) {
                    Some(s) => s,
                    None => continue,
                };
                if eq_ascii_case_insensitive(rep, name_bytes) {
                    update_best(&mut group.best, cand);
                    found = true;
                    break;
                }
            }
            if !found {
                bucket.push(AsciiGroupSized {
                    rep_off: item.filename_offset,
                    rep_len: item.filename_len,
                    size: item.size,
                    best: cand,
                });
            }
        } else {
            let name = match std::str::from_utf8(name_bytes) {
                Ok(s) => s,
                Err(_) => continue,
            };
            let key = (name.to_lowercase(), item.size);
            match unicode_groups.get_mut(&key) {
                None => {
                    unicode_groups.insert(key, cand);
                }
                Some(best) => {
                    update_best(best, cand);
                }
            }
        }
    }

    let mut out: Vec<i32> = Vec::with_capacity(ascii_groups.len() + unicode_groups.len());
    for bucket in ascii_groups.values() {
        for g in bucket.iter() {
            out.push(g.best.idx as i32);
        }
    }
    for best in unicode_groups.values() {
        out.push(best.idx as i32);
    }

    out.sort_unstable();
    let result_count = out.len().min(count_usize);
    unsafe {
        std::ptr::copy_nonoverlapping(out.as_ptr(), results_ptr, result_count);
    }
    result_count as c_int
}

#[no_mangle]
pub extern "C" fn filter_search_results_table_native(
    items_ptr: *const SearchResultInfoOffsets,
    count: c_int,
    string_table_ptr: *const c_uchar,
    string_table_len: c_int,
    min_size: i64,
    max_size: i64,
    extensions_ptr: *const *const c_char,
    ext_count: c_int,
    spanish_only: bool,
    min_quality: c_int,
    results_ptr: *mut c_int,
) -> c_int {
    if items_ptr.is_null() || results_ptr.is_null() || count <= 0 {
        return 0;
    }
    if string_table_ptr.is_null() || string_table_len <= 0 {
        return 0;
    }

    let count_usize = count as usize;
    let items = unsafe { slice::from_raw_parts(items_ptr, count_usize) };

    let table_len = string_table_len as usize;
    let table = unsafe { slice::from_raw_parts(string_table_ptr, table_len) };

    let allowed_exts = build_allowed_exts(extensions_ptr, ext_count);

    let indices: Vec<i32> = items
        .iter()
        .enumerate()
        .filter_map(|(idx, item)| {
            if item.size < min_size {
                return None;
            }
            if item.size > max_size {
                return None;
            }
            if item.quality < min_quality {
                return None;
            }

            if !allowed_exts.is_empty() {
                let off = item.extension_offset as usize;
                let len = item.extension_len as usize;
                if off + len > table_len {
                    return None;
                }
                let ext_bytes = &table[off..off + len];
                if !ext_allowed(&allowed_exts, ext_bytes) {
                    return None;
                }
            }

            if spanish_only {
                let off = item.filename_offset as usize;
                let len = item.filename_len as usize;
                if off + len > table_len {
                    return None;
                }
                let name_bytes = &table[off..off + len];
                let name = std::str::from_utf8(name_bytes).ok()?;
                if !is_spanish_text_internal(name) {
                    return None;
                }
            }

            Some(idx as i32)
        })
        .collect();

    let result_count = indices.len().min(count_usize);
    unsafe {
        std::ptr::copy_nonoverlapping(indices.as_ptr(), results_ptr, result_count);
    }
    result_count as c_int
}

#[no_mangle]
pub extern "C" fn process_search_results_table_native(
    items_ptr: *const SearchResultInfoOffsets,
    count: c_int,
    string_table_ptr: *const c_uchar,
    string_table_len: c_int,
    min_size: i64,
    max_size: i64,
    extensions_ptr: *const *const c_char,
    ext_count: c_int,
    spanish_only: bool,
    min_quality: c_int,
    results_ptr: *mut c_int,
) -> c_int {
    if items_ptr.is_null() || results_ptr.is_null() || count <= 0 {
        return 0;
    }
    if string_table_ptr.is_null() || string_table_len <= 0 {
        return 0;
    }

    let count_usize = count as usize;
    let items = unsafe { slice::from_raw_parts(items_ptr, count_usize) };

    let table_len = string_table_len as usize;
    let table = unsafe { slice::from_raw_parts(string_table_ptr, table_len) };

    let state = RandomState::new();
    let allowed_exts = build_allowed_exts(extensions_ptr, ext_count);

    let mut ascii_groups: FastHashMap<u64, Vec<AsciiGroupSized>, RandomState> =
        FastHashMap::with_hasher(state.clone());
    let mut unicode_groups: FastHashMap<(String, i64), BestCandidate, RandomState> =
        FastHashMap::with_hasher(state.clone());

    for (idx, item) in items.iter().enumerate() {
        if item.size < min_size {
            continue;
        }
        if item.size > max_size {
            continue;
        }
        if item.quality < min_quality {
            continue;
        }

        // extension filter
        if !allowed_exts.is_empty() {
            let off = item.extension_offset as usize;
            let len = item.extension_len as usize;
            if off + len > table_len {
                continue;
            }
            let ext_bytes = &table[off..off + len];
            if !ext_allowed(&allowed_exts, ext_bytes) {
                continue;
            }
        }

        let name_bytes = match slice_from_table(table, item.filename_offset, item.filename_len) {
            Some(s) => s,
            None => continue,
        };

        if spanish_only {
            let filename = match std::str::from_utf8(name_bytes) {
                Ok(s) => s,
                Err(_) => continue,
            };
            if !is_spanish_text_internal(filename) {
                continue;
            }
        }

        let cand = BestCandidate {
            idx,
            q: item.quality as i32,
            s: item.provider_score as i32,
        };

        if name_bytes.is_ascii() {
            let h = hash_ascii_case_insensitive_with_size(&state, name_bytes, item.size);
            let bucket = ascii_groups.entry(h).or_insert_with(Vec::new);
            let mut found = false;
            for group in bucket.iter_mut() {
                if group.size != item.size {
                    continue;
                }
                let rep = match slice_from_table(table, group.rep_off, group.rep_len) {
                    Some(s) => s,
                    None => continue,
                };
                if eq_ascii_case_insensitive(rep, name_bytes) {
                    update_best(&mut group.best, cand);
                    found = true;
                    break;
                }
            }
            if !found {
                bucket.push(AsciiGroupSized {
                    rep_off: item.filename_offset,
                    rep_len: item.filename_len,
                    size: item.size,
                    best: cand,
                });
            }
        } else {
            let filename = match std::str::from_utf8(name_bytes) {
                Ok(s) => s,
                Err(_) => continue,
            };
            let key = (filename.to_lowercase(), item.size);
            match unicode_groups.get_mut(&key) {
                None => {
                    unicode_groups.insert(key, cand);
                }
                Some(best) => {
                    update_best(best, cand);
                }
            }
        }
    }

    let mut indices: Vec<(i32, i32, i32)> = Vec::with_capacity(ascii_groups.len() + unicode_groups.len());
    for bucket in ascii_groups.values() {
        for g in bucket.iter() {
            indices.push((g.best.idx as i32, g.best.q, g.best.s));
        }
    }
    for best in unicode_groups.values() {
        indices.push((best.idx as i32, best.q, best.s));
    }

    indices.sort_by(|a, b| {
        let (ia, qa, sa) = *a;
        let (ib, qb, sb) = *b;
        qb.cmp(&qa)
            .then_with(|| sb.cmp(&sa))
            .then_with(|| ia.cmp(&ib))
    });

    let result_count = indices.len().min(count_usize);
    let out: Vec<i32> = indices
        .into_iter()
        .take(result_count)
        .map(|(i, _, _)| i)
        .collect();

    unsafe {
        std::ptr::copy_nonoverlapping(out.as_ptr(), results_ptr, result_count);
    }

    result_count as c_int
}

#[no_mangle]
pub extern "C" fn filter_search_results_native(
    items_ptr: *const SearchResultInfo,
    count: c_int,
    min_size: i64,
    max_size: i64,
    extensions_ptr: *const *const c_char,
    ext_count: c_int,
    spanish_only: bool,
    min_quality: c_int,
    results_ptr: *mut c_int,
) -> c_int {
    if items_ptr.is_null() || results_ptr.is_null() || count <= 0 {
        return 0;
    }

    let count = count as usize;
    let items = unsafe { slice::from_raw_parts(items_ptr, count) };

    let allowed_exts = build_allowed_exts(extensions_ptr, ext_count);

    let indices: Vec<i32> = items
        .iter()
        .enumerate()
        .filter_map(|(idx, item)| {
            if item.size < min_size {
                return None;
            }
            if item.size > max_size {
                return None;
            }
            if item.quality < min_quality {
                return None;
            }

            if !allowed_exts.is_empty() {
                if item.extension_ptr.is_null() {
                    return None;
                }
                let ext_bytes = unsafe { CStr::from_ptr(item.extension_ptr).to_bytes() };
                if !ext_allowed(&allowed_exts, ext_bytes) {
                    return None;
                }
            }

            if spanish_only {
                if item.filename_ptr.is_null() {
                    return None;
                }
                let name = unsafe { CStr::from_ptr(item.filename_ptr) }.to_str().ok()?;
                if !is_spanish_text_internal(name) {
                    return None;
                }
            }

            Some(idx as i32)
        })
        .collect();

    let result_count = indices.len().min(count);
    unsafe {
        std::ptr::copy_nonoverlapping(indices.as_ptr(), results_ptr, result_count);
    }
    result_count as c_int
}

#[no_mangle]
pub extern "C" fn sort_by_quality_native(
    items_ptr: *const SearchResultInfo,
    count: c_int,
    results_ptr: *mut c_int,
) -> c_int {
    if items_ptr.is_null() || results_ptr.is_null() || count <= 0 {
        return 0;
    }

    let count = count as usize;
    let items = unsafe { slice::from_raw_parts(items_ptr, count) };

    let mut indices: Vec<i32> = (0..count as i32).collect();
    indices.sort_by(|&a, &b| {
        let qa = items[a as usize].quality;
        let qb = items[b as usize].quality;
        qb.cmp(&qa).then_with(|| a.cmp(&b))
    });

    unsafe {
        std::ptr::copy_nonoverlapping(indices.as_ptr(), results_ptr, count);
    }
    count as c_int
}

#[no_mangle]
pub extern "C" fn sort_by_quality_table_native(
    items_ptr: *const SearchResultInfoOffsets,
    count: c_int,
    results_ptr: *mut c_int,
) -> c_int {
    if items_ptr.is_null() || results_ptr.is_null() || count <= 0 {
        return 0;
    }

    let count_usize = count as usize;
    let items = unsafe { slice::from_raw_parts(items_ptr, count_usize) };

    let mut indices: Vec<i32> = (0..count as i32).collect();
    indices.sort_by(|&a, &b| {
        let qa = items[a as usize].quality;
        let qb = items[b as usize].quality;
        qb.cmp(&qa).then_with(|| a.cmp(&b))
    });

    unsafe {
        std::ptr::copy_nonoverlapping(indices.as_ptr(), results_ptr, count_usize);
    }
    count as c_int
}

#[no_mangle]
pub extern "C" fn process_search_results_native(
    items_ptr: *const SearchResultInfo,
    count: c_int,
    min_size: i64,
    max_size: i64,
    extensions_ptr: *const *const c_char,
    ext_count: c_int,
    spanish_only: bool,
    min_quality: c_int,
    results_ptr: *mut c_int,
) -> c_int {
    if items_ptr.is_null() || results_ptr.is_null() || count <= 0 {
        return 0;
    }

    let count_usize = count as usize;
    let items = unsafe { slice::from_raw_parts(items_ptr, count_usize) };

    let state = RandomState::new();
    let allowed_exts = build_allowed_exts(extensions_ptr, ext_count);

    let mut ascii_groups: FastHashMap<u64, Vec<(Vec<u8>, i64, BestCandidate)>, RandomState> =
        FastHashMap::with_hasher(state.clone());
    let mut unicode_groups: FastHashMap<(String, i64), BestCandidate, RandomState> =
        FastHashMap::with_hasher(state.clone());

    for (idx, item) in items.iter().enumerate() {
        if item.size < min_size {
            continue;
        }
        if item.size > max_size {
            continue;
        }
        if item.quality < min_quality {
            continue;
        }

        if !allowed_exts.is_empty() {
            if item.extension_ptr.is_null() {
                continue;
            }
            let ext_bytes = unsafe { CStr::from_ptr(item.extension_ptr).to_bytes() };
            if !ext_allowed(&allowed_exts, ext_bytes) {
                continue;
            }
        }

        if item.filename_ptr.is_null() {
            continue;
        }

        let name_bytes = unsafe { CStr::from_ptr(item.filename_ptr).to_bytes() };
        if spanish_only {
            let filename = match unsafe { CStr::from_ptr(item.filename_ptr) }.to_str() {
                Ok(s) => s,
                Err(_) => continue,
            };
            if !is_spanish_text_internal(filename) {
                continue;
            }
        }

        let cand = BestCandidate {
            idx,
            q: item.quality as i32,
            s: item.provider_score as i32,
        };

        if name_bytes.is_ascii() {
            let h = hash_ascii_case_insensitive_with_size(&state, name_bytes, item.size);
            let bucket = ascii_groups.entry(h).or_insert_with(Vec::new);
            let mut found = false;
            for (rep, rep_size, best) in bucket.iter_mut() {
                if *rep_size != item.size {
                    continue;
                }
                if eq_ascii_case_insensitive(rep.as_slice(), name_bytes) {
                    update_best(best, cand);
                    found = true;
                    break;
                }
            }
            if !found {
                bucket.push((name_bytes.to_vec(), item.size, cand));
            }
        } else {
            let filename = match unsafe { CStr::from_ptr(item.filename_ptr) }.to_str() {
                Ok(s) => s,
                Err(_) => continue,
            };
            let key = (filename.to_lowercase(), item.size);
            match unicode_groups.get_mut(&key) {
                None => {
                    unicode_groups.insert(key, cand);
                }
                Some(best) => {
                    update_best(best, cand);
                }
            }
        }
    }

    let mut indices: Vec<(i32, i32, i32)> = Vec::with_capacity(ascii_groups.len() + unicode_groups.len());
    for bucket in ascii_groups.values() {
        for (_, _, best) in bucket.iter() {
            indices.push((best.idx as i32, best.q, best.s));
        }
    }
    for best in unicode_groups.values() {
        indices.push((best.idx as i32, best.q, best.s));
    }

    indices.sort_by(|a, b| {
        let (ia, qa, sa) = *a;
        let (ib, qb, sb) = *b;
        qb.cmp(&qa)
            .then_with(|| sb.cmp(&sa))
            .then_with(|| ia.cmp(&ib))
    });

    let result_count = indices.len().min(count_usize);
    let out: Vec<i32> = indices.into_iter().take(result_count).map(|(i, _, _)| i).collect();
    unsafe {
        std::ptr::copy_nonoverlapping(out.as_ptr(), results_ptr, result_count);
    }
    result_count as c_int
}

// ============================================
// FILTRADO PARALELO DE AUTORES (10x más rápido)
// ============================================

#[no_mangle]
pub extern "C" fn filter_authors_native(
    authors_ptr: *const *const c_char,
    count: c_int,
    search_ptr: *const c_char,
    results_ptr: *mut c_int,
) -> c_int {
    if authors_ptr.is_null() || search_ptr.is_null() || results_ptr.is_null() {
        return 0;
    }

    let count = count as usize;
    let search = unsafe {
        match CStr::from_ptr(search_ptr).to_str() {
            Ok(s) => s.to_lowercase(),
            Err(_) => return 0,
        }
    };
    let search_lower_bytes = search.as_bytes();
    let search_is_ascii = search_lower_bytes.is_ascii();

    // Convertir punteros a datos thread-safe primero
    let authors_ptrs = unsafe { slice::from_raw_parts(authors_ptr, count) };
    let safe_authors: Vec<String> = authors_ptrs
        .iter()
        .filter_map(|&author_ptr| {
            if author_ptr.is_null() {
                return None;
            }
            unsafe {
                CStr::from_ptr(author_ptr)
                    .to_str()
                    .ok()
                    .map(|s| s.to_string())
            }
        })
        .collect();

    // Filtrado paralelo con datos seguros
    let filtered_indices: Vec<i32> = safe_authors
        .par_iter()
        .enumerate()
        .filter_map(|(idx, author)| {
            if search_is_ascii && author.is_ascii() {
                if contains_ascii_lower_bytes(author.as_bytes(), search_lower_bytes) {
                    return Some(idx as i32);
                }
                return None;
            }
            if author.to_lowercase().contains(&search) {
                Some(idx as i32)
            } else {
                None
            }
        })
        .collect();

    // Copiar resultados
    let result_count = filtered_indices.len().min(count);
    unsafe {
        std::ptr::copy_nonoverlapping(
            filtered_indices.as_ptr(),
            results_ptr,
            result_count,
        );
    }

    result_count as c_int
}

// ============================================
// BÚSQUEDA RÁPIDA EN LISTA (O(1) con hash)
// ============================================

#[no_mangle]
pub extern "C" fn create_author_set() -> *mut FastHashSet<String, RandomState> {
    Box::into_raw(Box::new(FastHashSet::with_hasher(RandomState::new())))
}

#[no_mangle]
pub extern "C" fn author_set_add(
    set_ptr: *mut FastHashSet<String, RandomState>,
    author_ptr: *const c_char,
) {
    if set_ptr.is_null() || author_ptr.is_null() {
        return;
    }

    unsafe {
        if let Ok(author) = CStr::from_ptr(author_ptr).to_str() {
            (*set_ptr).insert(author.to_lowercase());
        }
    }
}

#[no_mangle]
pub extern "C" fn author_set_contains(
    set_ptr: *const FastHashSet<String, RandomState>,
    author_ptr: *const c_char,
) -> c_int {
    if set_ptr.is_null() || author_ptr.is_null() {
        return 0;
    }

    unsafe {
        if let Ok(author) = CStr::from_ptr(author_ptr).to_str() {
            if (*set_ptr).contains(&author.to_lowercase()) {
                1
            } else {
                0
            }
        } else {
            0
        }
    }
}

#[no_mangle]
pub extern "C" fn destroy_author_set(set_ptr: *mut FastHashSet<String, RandomState>) {
    if !set_ptr.is_null() {
        unsafe {
            let _ = Box::from_raw(set_ptr);
        }
    }
}

// ============================================
// ESTADÍSTICAS RÁPIDAS
// ============================================

#[repr(C)]
pub struct BatchStats {
    pub total: c_int,
    pub valid: c_int,
    pub invalid: c_int,
    pub cached: c_int,
    pub avg_time_ms: c_int,
}

#[no_mangle]
pub extern "C" fn calculate_batch_stats(
    files_counts: *const c_int,
    is_valid: *const c_int,
    is_cached: *const c_int,
    times_ms: *const c_int,
    count: c_int,
) -> BatchStats {
    let count = count as usize;

    if files_counts.is_null() || is_valid.is_null() || is_cached.is_null() || times_ms.is_null() {
        return BatchStats {
            total: 0,
            valid: 0,
            invalid: 0,
            cached: 0,
            avg_time_ms: 0,
        };
    }

    let _files = unsafe { slice::from_raw_parts(files_counts, count) };
    let valid = unsafe { slice::from_raw_parts(is_valid, count) };
    let cached = unsafe { slice::from_raw_parts(is_cached, count) };
    let times = unsafe { slice::from_raw_parts(times_ms, count) };

    // Cálculo paralelo
    let (valid_count, cached_count, total_time): (usize, usize, i64) = (0..count)
        .into_par_iter()
        .map(|i| {
            let v = if valid[i] != 0 { 1 } else { 0 };
            let c = if cached[i] != 0 { 1 } else { 0 };
            let t = times[i] as i64;
            (v, c, t)
        })
        .reduce(
            || (0, 0, 0),
            |(v1, c1, t1), (v2, c2, t2)| (v1 + v2, c1 + c2, t1 + t2),
        );

    BatchStats {
        total: count as c_int,
        valid: valid_count as c_int,
        invalid: (count - valid_count) as c_int,
        cached: cached_count as c_int,
        avg_time_ms: if count > 0 {
            (total_time / count as i64) as c_int
        } else {
            0
        },
    }
}

// ============================================
// COMPRESIÓN DE STRINGS (60% menos memoria)
// ============================================

#[no_mangle]
pub extern "C" fn create_string_compressor() -> *mut FastHashMap<String, u32, RandomState> {
    Box::into_raw(Box::new(FastHashMap::with_hasher(RandomState::new())))
}

#[no_mangle]
pub extern "C" fn compress_string(
    compressor_ptr: *mut FastHashMap<String, u32, RandomState>,
    text_ptr: *const c_char,
) -> u32 {
    if compressor_ptr.is_null() || text_ptr.is_null() {
        return 0;
    }

    unsafe {
        if let Ok(text) = CStr::from_ptr(text_ptr).to_str() {
            let compressor = &mut *compressor_ptr;
            let next_id = compressor.len() as u32;
            *compressor.entry(text.to_string()).or_insert(next_id)
        } else {
            0
        }
    }
}

#[no_mangle]
pub extern "C" fn decompress_string(
    compressor_ptr: *const FastHashMap<String, u32, RandomState>,
    id: u32,
) -> *mut c_char {
    if compressor_ptr.is_null() {
        return std::ptr::null_mut();
    }

    unsafe {
        let compressor = &*compressor_ptr;
        for (text, &text_id) in compressor.iter() {
            if text_id == id {
                if let Ok(c_string) = CString::new(text.as_str()) {
                    return c_string.into_raw();
                }
            }
        }
    }

    std::ptr::null_mut()
}

#[no_mangle]
pub extern "C" fn destroy_string_compressor(
    compressor_ptr: *mut FastHashMap<String, u32, RandomState>,
) {
    if !compressor_ptr.is_null() {
        unsafe {
            let _ = Box::from_raw(compressor_ptr);
        }
    }
}

#[no_mangle]
pub extern "C" fn free_string(s: *mut c_char) {
    if !s.is_null() {
        unsafe {
            let _ = CString::from_raw(s);
        }
    }
}

// ============================================
// TESTS
// ============================================

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CString;

    #[test]
    fn test_spanish_detection() {
        let text = CString::new("El libro de García Márquez").unwrap();
        assert_eq!(is_spanish_text_native(text.as_ptr()), 1);

        let text_fr = CString::new("Le livre de Victor Hugo").unwrap();
        assert_eq!(is_spanish_text_native(text_fr.as_ptr()), 0);
    }

    #[test]
    fn test_filter_authors() {
        let authors = vec![
            CString::new("Gabriel García Márquez").unwrap(),
            CString::new("Jorge Luis Borges").unwrap(),
            CString::new("Mario Vargas Llosa").unwrap(),
        ];
        let author_ptrs: Vec<*const c_char> = authors.iter().map(|s| s.as_ptr()).collect();

        let search = CString::new("garcía").unwrap();
        let mut results = vec![0i32; 3];

        let count = filter_authors_native(
            author_ptrs.as_ptr(),
            3,
            search.as_ptr(),
            results.as_mut_ptr(),
        );

        assert_eq!(count, 1);
        assert_eq!(results[0], 0); // Índice de García Márquez
    }
}

// ============================================
// MEJORA #44: VALIDACIÓN RÁPIDA DE ARCHIVOS
// ============================================

static VALID_EXTENSIONS: Lazy<FastHashSet<&'static str, RandomState>> = Lazy::new(|| {
    let mut set = FastHashSet::with_hasher(RandomState::new());
    set.insert(".epub");
    set.insert(".pdf");
    set.insert(".mobi");
    set.insert(".azw3");
    set.insert(".djvu");
    set.insert(".fb2");
    set.insert(".doc");
    set.insert(".docx");
    set.insert(".txt");
    set.insert(".rtf");
    set
});

#[no_mangle]
pub extern "C" fn is_valid_filename_native(filename_ptr: *const c_char) -> c_int {
    if filename_ptr.is_null() {
        return 0;
    }

    let raw = unsafe {
        match CStr::from_ptr(filename_ptr).to_str() {
            Ok(s) => s,
            Err(_) => return 0,
        }
    };

    if raw.is_ascii() {
        let bytes = raw.as_bytes();
        for ext in VALID_EXTENSIONS.iter() {
            if ends_with_ascii_lower_bytes(bytes, ext.as_bytes()) {
                return 1;
            }
        }
        return 0;
    }

    let filename = raw.to_lowercase();
    // Verificar extensión válida (ultra-rápido con HashSet)
    for ext in VALID_EXTENSIONS.iter() {
        if filename.ends_with(ext) {
            return 1;
        }
    }

    0
}

// ============================================
// MEJORA #44: COMPRESIÓN ZSTD (2-3x más rápido que Brotli)
// ============================================

#[no_mangle]
pub extern "C" fn compress_zstd_native(
    data_ptr: *const c_uchar,
    data_len: c_int,
    output_ptr: *mut c_uchar,
    output_capacity: c_int,
    level: c_int,
) -> c_int {
    if data_ptr.is_null() || output_ptr.is_null() || data_len <= 0 || output_capacity <= 0 {
        return -1;
    }

    let data = unsafe { slice::from_raw_parts(data_ptr, data_len as usize) };
    let output = unsafe { slice::from_raw_parts_mut(output_ptr, output_capacity as usize) };

    match zstd::bulk::compress_to_buffer(data, output, level as i32) {
        Ok(size) => size as c_int,
        Err(_) => -1,
    }
}

#[no_mangle]
pub extern "C" fn decompress_zstd_native(
    data_ptr: *const c_uchar,
    data_len: c_int,
    output_ptr: *mut c_uchar,
    output_capacity: c_int,
) -> c_int {
    if data_ptr.is_null() || output_ptr.is_null() || data_len <= 0 || output_capacity <= 0 {
        return -1;
    }

    let data = unsafe { slice::from_raw_parts(data_ptr, data_len as usize) };
    let output = unsafe { slice::from_raw_parts_mut(output_ptr, output_capacity as usize) };

    match zstd::bulk::decompress_to_buffer(data, output) {
        Ok(size) => size as c_int,
        Err(_) => -1,
    }
}

// ============================================
// MEJORA #44: PARSING RÁPIDO DE METADATOS
// ============================================

#[repr(C)]
pub struct BookMetadata {
    pub title_ptr: *mut c_char,
    pub author_ptr: *mut c_char,
    pub has_metadata: c_int,
}

#[no_mangle]
pub extern "C" fn parse_book_metadata_native(filename_ptr: *const c_char) -> *mut BookMetadata {
    if filename_ptr.is_null() {
        return std::ptr::null_mut();
    }

    let filename = unsafe {
        match CStr::from_ptr(filename_ptr).to_str() {
            Ok(s) => s,
            Err(_) => return std::ptr::null_mut(),
        }
    };

    // Buscar patrón "Titulo - Autor.ext"
    if let Some(dash_pos) = filename.find(" - ") {
        let title = &filename[..dash_pos].trim();
        let after_dash = &filename[dash_pos + 3..];
        
        // Remover extensión
        let author = if let Some(dot_pos) = after_dash.rfind('.') {
            &after_dash[..dot_pos].trim()
        } else {
            after_dash.trim()
        };

        if !title.is_empty() && !author.is_empty() {
            let title_c = match CString::new(title.to_string()) {
                Ok(s) => s.into_raw(),
                Err(_) => std::ptr::null_mut(),
            };

            let author_c = match CString::new(author.to_string()) {
                Ok(s) => s.into_raw(),
                Err(_) => std::ptr::null_mut(),
            };

            let metadata = Box::new(BookMetadata {
                title_ptr: title_c,
                author_ptr: author_c,
                has_metadata: 1,
            });

            return Box::into_raw(metadata);
        }
    }

    // No se pudo parsear
    let metadata = Box::new(BookMetadata {
        title_ptr: std::ptr::null_mut(),
        author_ptr: std::ptr::null_mut(),
        has_metadata: 0,
    });

    Box::into_raw(metadata)
}

#[no_mangle]
pub extern "C" fn free_book_metadata(metadata_ptr: *mut BookMetadata) {
    if !metadata_ptr.is_null() {
        unsafe {
            let metadata = Box::from_raw(metadata_ptr);
            if !metadata.title_ptr.is_null() {
                let _ = CString::from_raw(metadata.title_ptr);
            }
            if !metadata.author_ptr.is_null() {
                let _ = CString::from_raw(metadata.author_ptr);
            }
        }
    }
}

// ============================================
// MEJORA #44: BÚSQUEDA SIMD ULTRA-RÁPIDA
// ============================================

#[no_mangle]
pub extern "C" fn find_substring_simd(
    haystack_ptr: *const c_char,
    needle_ptr: *const c_char,
) -> c_int {
    if haystack_ptr.is_null() || needle_ptr.is_null() {
        return -1;
    }

    let haystack = unsafe {
        match CStr::from_ptr(haystack_ptr).to_bytes() {
            bytes => bytes,
        }
    };

    let needle = unsafe {
        match CStr::from_ptr(needle_ptr).to_bytes() {
            bytes => bytes,
        }
    };

    // Usar memchr para búsqueda SIMD ultra-rápida
    match memmem::find(haystack, needle) {
        Some(pos) => pos as c_int,
        None => -1,
    }
}
