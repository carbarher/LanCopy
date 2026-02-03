use once_cell::sync::Lazy;
use regex::Regex;
use std::ffi::CStr;
use std::os::raw::c_char;
use std::slice;
use std::io::{Read, BufReader, Seek, SeekFrom};
use std::cmp::min;
use std::fs::File;
use std::path::Path;

#[cfg(feature = "pdf")] 
use lopdf::Document as LDocument;

use zip::read::ZipArchive;

// ============================================================================
// DETECCIÓN DE IDIOMA ESPAÑOL
// ============================================================================

/// Regex compilado para caracteres españoles (thread-safe, inicializado una vez)
static SPANISH_REGEX: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"[ñáéíóúüÁÉÍÓÚÜÑ]").expect("Failed to compile Spanish regex")
});

/// Keywords en español (pre-compiladas)
static SPANISH_KEYWORDS: &[&str] = &[
    "español", "spanish", "castellano", "spa", "es", "latino", "latinoamerica",
    "argentina", "mexico", "españa", "chile", "colombia", "peru", "venezuela",
    "de", "del", "la", "el", "los", "las", "un", "una", "con", "para", "por"
];

/// Detecta si un texto contiene indicadores de idioma español
/// 
/// # Safety
/// - `text` debe ser un puntero válido a UTF-8
/// - `len` debe ser la longitud correcta del texto
/// 
/// # Returns
/// - `true` si el texto contiene caracteres o palabras en español
/// - `false` en caso contrario
#[no_mangle]
pub unsafe extern "C" fn is_spanish_text(text: *const u8, len: usize) -> bool {
    if text.is_null() || len == 0 {
        return false;
    }

    let text_slice = slice::from_raw_parts(text, len);

    let text_str = match std::str::from_utf8(text_slice) {
        Ok(s) => s,
        Err(_) => return false,
    };

    let lower_text = text_str.to_lowercase();

    if SPANISH_REGEX.is_match(&lower_text) {
        return true;
    }

    for keyword in SPANISH_KEYWORDS {
        if lower_text.contains(keyword) {
            return true;
        }
    }

    false
}

// ============================================================================
// NORMALIZACIÓN DE NOMBRES DE AUTORES
// ============================================================================

/// Normaliza un nombre de autor eliminando puntos, espacios extras y convirtiendo a minúsculas
/// Ejemplos: "A. E. Pepito" -> "ae pepito"
///
/// # Safety
/// - `input` debe ser un puntero válido a string C null-terminated
/// - `output` debe tener al menos `max_len` bytes disponibles
///
/// # Returns
/// - Longitud del string normalizado (sin null terminator)
/// - -1 si hay error (buffer muy pequeño, input inválido, etc.)
#[no_mangle]
pub unsafe extern "C" fn normalize_author_name(
    input: *const c_char,
    output: *mut c_char,
    max_len: usize,
) -> i32 {
    if input.is_null() || output.is_null() || max_len == 0 {
        return -1;
    }

    // Convertir C string a Rust string
    let c_str = match CStr::from_ptr(input).to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };

    // Normalizar
    let mut result = String::with_capacity(c_str.len());
    let mut last_was_space = false;

    for ch in c_str.chars() {
        if ch == '.' {
            continue; // Ignorar puntos
        }

        if ch.is_whitespace() {
            if !last_was_space && !result.is_empty() {
                result.push(' ');
                last_was_space = true;
            }
        } else {
            // Convertir a minúsculas
            for lower_ch in ch.to_lowercase() {
                result.push(lower_ch);
            }
            last_was_space = false;
        }
    }

    // Trim final
    let trimmed = result.trim_end();
    let bytes = trimmed.as_bytes();

    // Verificar que cabe en el buffer (incluyendo null terminator)
    if bytes.len() + 1 > max_len {
        return -1;
    }

    // Copiar a output
    std::ptr::copy_nonoverlapping(bytes.as_ptr(), output as *mut u8, bytes.len());
    
    // Agregar null terminator
    *output.add(bytes.len()) = 0;

    bytes.len() as i32
}

// ============================================================================
// DISTANCIA DE LEVENSHTEIN (OPTIMIZADA)
// ============================================================================

/// Calcula la distancia de Levenshtein entre dos strings
/// Versión optimizada con una sola fila de memoria (O(min(n,m)) espacio)
///
/// # Safety
/// - `s1` y `s2` deben ser punteros válidos a UTF-8
/// - `len1` y `len2` deben ser las longitudes correctas
///
/// # Returns
/// - Distancia de Levenshtein (número de ediciones necesarias)
/// - -1 si hay error
#[no_mangle]
pub unsafe extern "C" fn levenshtein_distance(
    s1: *const u8,
    len1: usize,
    s2: *const u8,
    len2: usize,
) -> i32 {
    if s1.is_null() || s2.is_null() {
        return -1;
    }

    // Convertir a slices
    let s1_slice = slice::from_raw_parts(s1, len1);
    let s2_slice = slice::from_raw_parts(s2, len2);

    let s1_str = match std::str::from_utf8(s1_slice) {
        Ok(s) => s,
        Err(_) => return -1,
    };

    let s2_str = match std::str::from_utf8(s2_slice) {
        Ok(s) => s,
        Err(_) => return -1,
    };

    // Optimización: si son iguales, distancia = 0
    if s1_str == s2_str {
        return 0;
    }

    let s1_chars: Vec<char> = s1_str.chars().collect();
    let s2_chars: Vec<char> = s2_str.chars().collect();

    let len1 = s1_chars.len();
    let len2 = s2_chars.len();

    // Casos base
    if len1 == 0 {
        return len2 as i32;
    }
    if len2 == 0 {
        return len1 as i32;
    }

    // Optimización: usar solo una fila de memoria
    let mut prev_row: Vec<usize> = (0..=len2).collect();
    let mut curr_row: Vec<usize> = vec![0; len2 + 1];

    for i in 1..=len1 {
        curr_row[0] = i;

        for j in 1..=len2 {
            let cost = if s1_chars[i - 1] == s2_chars[j - 1] {
                0
            } else {
                1
            };

            curr_row[j] = std::cmp::min(
                std::cmp::min(
                    prev_row[j] + 1,      // Eliminación
                    curr_row[j - 1] + 1,  // Inserción
                ),
                prev_row[j - 1] + cost,   // Sustitución
            );
        }

        std::mem::swap(&mut prev_row, &mut curr_row);
    }

    prev_row[len2] as i32
}

// ============================================================================
// BÚSQUEDA RÁPIDA DE SUBSTRING
// ============================================================================

/// Verifica si un texto contiene alguna de las keywords dadas
/// Usa búsqueda optimizada case-insensitive
///
/// # Safety
/// - `text` debe ser un puntero válido a UTF-8
/// - `keywords` debe ser un array de punteros válidos a C strings
/// - `num_keywords` debe ser el número correcto de keywords
///
/// # Returns
/// - `true` si encuentra alguna keyword
/// - `false` en caso contrario
#[no_mangle]
pub unsafe extern "C" fn contains_keywords(
    text: *const u8,
    text_len: usize,
    keywords: *const *const c_char,
    num_keywords: usize,
) -> bool {
    if text.is_null() || keywords.is_null() || text_len == 0 || num_keywords == 0 {
        return false;
    }

    // Convertir texto a string
    let text_slice = slice::from_raw_parts(text, text_len);
    let text_str = match std::str::from_utf8(text_slice) {
        Ok(s) => s.to_lowercase(),
        Err(_) => return false,
    };

    // Iterar sobre keywords
    let keywords_slice = slice::from_raw_parts(keywords, num_keywords);
    
    for i in 0..num_keywords {
        let keyword_ptr = keywords_slice[i];
        if keyword_ptr.is_null() {
            continue;
        }

        let keyword = match CStr::from_ptr(keyword_ptr).to_str() {
            Ok(s) => s.to_lowercase(),
            Err(_) => continue,
        };

        if text_str.contains(&keyword) {
            return true;
        }
    }

    false
}

// ============================================================================
// UTILIDADES
// ============================================================================

/// Obtiene la versión de la biblioteca
#[no_mangle]
pub extern "C" fn get_version() -> *const c_char {
    "slsk_optimizer v0.1.0\0".as_ptr() as *const c_char
}

// ============================================================================
// EXTRACCIÓN DE TEXTO DE ARCHIVOS
// ============================================================================

const MAX_SAMPLE_CHARS: usize = 8000;
const MAX_READ_BYTES: usize = 200_000;
const ZIP_PREFERRED_EXTENSIONS: &[&str] = &[".xhtml", ".html", ".htm", ".xml", ".txt"];
const MOBI_EXTH_IDENTIFIER: &[u8] = b"EXTH";
const MOBI_PALM_SIGNATURES: &[&[u8]] = &[b"BOOKMOBI", b"TEXtREAd", b"MOBI", b"BOOK" ];
const PALM_DOC_HEADER_LEN: usize = 78;
const MOBI_TEXT_RECORD_COUNT_OFFSET: usize = 76;
const MOBI_TEXT_RECORD_SIZE_OFFSET: usize = 88;
const MOBI_RECORDS_BASE: usize = 16;
const MOBI_MAX_SAMPLE_RECORDS: usize = 20;

fn is_probably_mobi(header: &[u8]) -> bool {
    if header.len() < PALM_DOC_HEADER_LEN {
        return false;
    }
    MOBI_PALM_SIGNATURES.iter().any(|sig| header.starts_with(sig))
}

fn extract_mobi_text(path: &str, max_chars: usize) -> Result<String, String> {
    let mut file = File::open(path).map_err(|e| format!("MOBI open error: {e}"))?;

    let mut header = vec![0u8; PALM_DOC_HEADER_LEN + MOBI_MAX_SAMPLE_RECORDS * MOBI_RECORDS_BASE];
    let read = file.read(&mut header).map_err(|e| format!("MOBI read error: {e}"))?;
    header.truncate(read);

    if !is_probably_mobi(&header) {
        return extract_raw_text(path, max_chars);
    }

    if header.len() < MOBI_TEXT_RECORD_SIZE_OFFSET + 2 {
        return Err("MOBI header too short".into());
    }

    let text_record_count = u16::from_be_bytes([
        header[MOBI_TEXT_RECORD_COUNT_OFFSET],
        header[MOBI_TEXT_RECORD_COUNT_OFFSET + 1],
    ]) as usize;

    let text_record_size = u16::from_be_bytes([
        header[MOBI_TEXT_RECORD_SIZE_OFFSET],
        header[MOBI_TEXT_RECORD_SIZE_OFFSET + 1],
    ]) as usize;

    if text_record_count == 0 || text_record_size == 0 {
        return extract_raw_text(path, max_chars);
    }

    let mut record_offsets = Vec::with_capacity(text_record_count.min(MOBI_MAX_SAMPLE_RECORDS));
    for i in 0..text_record_count.min(MOBI_MAX_SAMPLE_RECORDS) {
        let offset_idx = PALM_DOC_HEADER_LEN + i * MOBI_RECORDS_BASE;
        if offset_idx + 4 > header.len() {
            break;
        }
        let offset = u32::from_be_bytes([
            header[offset_idx],
            header[offset_idx + 1],
            header[offset_idx + 2],
            header[offset_idx + 3],
        ]) as usize;
        record_offsets.push(offset);
    }

    if record_offsets.is_empty() {
        return extract_raw_text(path, max_chars);
    }

    let mut buffer = Vec::with_capacity(max_chars);

    for offset in record_offsets {
        file.seek(SeekFrom::Start(offset as u64)).map_err(|e| format!("MOBI seek error: {e}"))?;

        let mut record_buf = vec![0u8; text_record_size];
        let read = file.read(&mut record_buf).map_err(|e| format!("MOBI record read error: {e}"))?;
        record_buf.truncate(read);

        if record_buf.starts_with(MOBI_EXTH_IDENTIFIER) {
            continue;
        }

        for &b in &record_buf {
            if buffer.len() >= max_chars {
                break;
            }
            match b {
                0x09 | 0x0A | 0x0D => buffer.push(b' '),
                0x20..=0x7E => buffer.push(b),
                _ => {}
            }
        }

        if buffer.len() >= max_chars {
            break;
        }
    }

    if buffer.is_empty() {
        return extract_raw_text(path, max_chars);
    }

    Ok(String::from_utf8_lossy(&buffer).chars().take(max_chars).collect())
}

fn printable_ascii(sample: &[u8], max_chars: usize) -> String {
    let mut out = String::with_capacity(min(max_chars, sample.len()));
    for &b in sample {
        if out.len() >= max_chars {
            break;
        }

        match b {
            0x09 | 0x0A | 0x0D => {
                out.push(' ');
            }
            0x20..=0x7E => {
                out.push(b as char);
            }
            _ => {}
        }
    }
    out
}

#[cfg(feature = "pdf")]
fn extract_pdf_text(path: &str, max_chars: usize) -> Result<String, String> {
    let doc = LDocument::load(path).map_err(|e| format!("PDF load error: {e}"))?;
    let mut out = String::with_capacity(max_chars);

    for page_id in doc.page_iter() {
        if out.len() >= max_chars {
            break;
        }

        let page = doc.get_page_content(page_id)
            .and_then(|content| doc.decode_stream(content))
            .unwrap_or_default();

        let text = String::from_utf8_lossy(&page);
        for ch in text.chars() {
            if out.len() >= max_chars {
                break;
            }

            match ch {
                '\n' | '\r' | '\t' => out.push(' '),
                c if c.is_control() => {},
                c => out.push(c),
            }
        }
    }

    Ok(out)
}

#[cfg(not(feature = "pdf"))]
fn extract_pdf_text(path: &str, max_chars: usize) -> Result<String, String> {
    let mut file = File::open(path).map_err(|e| format!("PDF open error: {e}"))?;
    let mut buffer = vec![0u8; MAX_READ_BYTES];
    let read = file.read(&mut buffer).map_err(|e| format!("PDF read error: {e}"))?;
    buffer.truncate(read);
    Ok(printable_ascii(&buffer, max_chars))
}

fn extract_raw_text(path: &str, max_chars: usize) -> Result<String, String> {
    let mut file = File::open(path).map_err(|e| format!("Open error: {e}"))?;
    let mut buffer = vec![0u8; MAX_READ_BYTES];
    let read = file.read(&mut buffer).map_err(|e| format!("Read error: {e}"))?;
    buffer.truncate(read);

    let decoded = match String::from_utf8(buffer.clone()) {
        Ok(txt) => txt,
        Err(_) => printable_ascii(&buffer, max_chars)
    };

    Ok(decoded.chars().take(max_chars).collect())
}

fn extract_zip_text(path: &str, max_chars: usize) -> Result<String, String> {
    let file = File::open(path).map_err(|e| format!("ZIP open error: {e}"))?;
    let mut archive = ZipArchive::new(BufReader::new(file)).map_err(|e| format!("ZIP parse error: {e}"))?;

    // Elegir entrada preferida por extensión y longitud de nombre
    let mut selected_index: Option<usize> = None;
    for i in 0..archive.len() {
        let entry = archive.by_index(i).map_err(|e| format!("ZIP entry error: {e}"))?;
        let name = entry.name().to_lowercase();

        if ZIP_PREFERRED_EXTENSIONS.iter().any(|ext| name.ends_with(ext)) {
            selected_index = Some(i);
            break;
        }

        if selected_index.is_none() {
            selected_index = Some(i);
        }
    }

    let index = selected_index.ok_or_else(|| "ZIP empty".to_string())?;
    let mut entry = archive.by_index(index).map_err(|e| format!("ZIP entry error: {e}"))?;
    let mut buffer = Vec::new();
    entry.read_to_end(&mut buffer).map_err(|e| format!("ZIP read error: {e}"))?;

    let text = match String::from_utf8(buffer.clone()) {
        Ok(txt) => txt,
        Err(_) => printable_ascii(&buffer, max_chars)
    };

    Ok(text.chars().take(max_chars).collect())
}

fn extract_text_internal(path: &str, extension: &str, max_chars: usize) -> Result<String, String> {
    match extension {
        "pdf" => extract_pdf_text(path, max_chars),
        "epub" | "docx" | "odt" => extract_zip_text(path, max_chars),
        "mobi" | "azw" | "azw3" => extract_mobi_text(path, max_chars),
        _ => extract_raw_text(path, max_chars)
    }
}

#[no_mangle]
pub extern "C" fn extract_text_sample(
    path_ptr: *const c_char,
    out_ptr: *mut c_char,
    max_len: usize
) -> i32 {
    if path_ptr.is_null() || out_ptr.is_null() || max_len == 0 {
        return -1;
    }

    let path = unsafe {
        match CStr::from_ptr(path_ptr).to_str() {
            Ok(s) => s,
            Err(_) => return -2,
        }
    };

    if !Path::new(path).exists() {
        return -3;
    }

    let extension = Path::new(path)
        .extension()
        .and_then(|s| s.to_str())
        .unwrap_or("")
        .to_lowercase();

    let text = match extract_text_internal(path, &extension, min(MAX_SAMPLE_CHARS, max_len.saturating_sub(1))) {
        Ok(txt) => txt,
        Err(err) => {
            #[cfg(debug_assertions)]
            eprintln!("extract_text_sample fallback: {err}");
            String::new()
        }
    };

    let text = if text.is_empty() {
        // Fallback a lectura simple si no obtuvimos contenido
        extract_raw_text(path, min(MAX_SAMPLE_CHARS, max_len.saturating_sub(1))).unwrap_or_default()
    } else {
        text
    };

    let text = text.chars().take(min(MAX_SAMPLE_CHARS, max_len.saturating_sub(1))).collect::<String>();

    let bytes = text.as_bytes();
    let len = bytes.len();

    if len + 1 > max_len {
        return -4;
    }

    unsafe {
        std::ptr::copy_nonoverlapping(bytes.as_ptr(), out_ptr as *mut u8, len);
        *out_ptr.add(len) = 0;
    }

    len as i32
}

#[no_mangle]
pub extern "C" fn is_spanish_file(
    path_ptr: *const c_char,
    sample_limit: usize
) -> bool {
    if path_ptr.is_null() {
        return false;
    }

    let path = unsafe {
        match CStr::from_ptr(path_ptr).to_str() {
            Ok(s) => s,
            Err(_) => return false,
        }
    };

    let limit = min(sample_limit.max(1024), MAX_READ_BYTES);
    let extension = Path::new(path)
        .extension()
        .and_then(|s| s.to_str())
        .unwrap_or("")
        .to_lowercase();

    match extract_text_internal(path, &extension, min(MAX_SAMPLE_CHARS, limit)) {
        Ok(text) if !text.is_empty() => unsafe { is_spanish_text(text.as_bytes().as_ptr(), text.len()) },
        _ => false,
    }
}

#[no_mangle]
pub extern "C" fn is_spanish_stream(
    data_ptr: *const u8,
    len: usize
) -> bool {
    if data_ptr.is_null() || len == 0 {
        return false;
    }

    let sample = unsafe { slice::from_raw_parts(data_ptr, len) };

    let decoded = match String::from_utf8(sample.to_vec()) {
        Ok(text) => text,
        Err(_) => printable_ascii(sample, MAX_SAMPLE_CHARS),
    };

    let truncated: String = decoded.chars().take(MAX_SAMPLE_CHARS).collect();

    if truncated.is_empty() {
        return false;
    }

    unsafe { is_spanish_text(truncated.as_bytes().as_ptr(), truncated.len()) }
}

// ============================================================================
// TESTS
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CString;

    #[test]
    fn test_is_spanish_text() {
        let text = "Este es un libro en español";
        unsafe {
            assert!(is_spanish_text(text.as_ptr(), text.len()));
        }

        let text_en = "This is a book in English";
        unsafe {
            assert!(!is_spanish_text(text_en.as_ptr(), text_en.len()));
        }

        let text_accent = "Título con acentos";
        unsafe {
            assert!(is_spanish_text(text_accent.as_ptr(), text_accent.len()));
        }
    }

    #[test]
    fn test_normalize_author_name() {
        let input = CString::new("A. E. Pepito").unwrap();
        let mut output = vec![0u8; 100];

        unsafe {
            let len = normalize_author_name(
                input.as_ptr(),
                output.as_mut_ptr() as *mut c_char,
                output.len(),
            );

            assert!(len > 0);
            let result = CStr::from_ptr(output.as_ptr() as *const c_char)
                .to_str()
                .unwrap();
            assert_eq!(result, "ae pepito");
        }
    }

    #[test]
    fn test_levenshtein_distance() {
        let s1 = "kitten";
        let s2 = "sitting";

        unsafe {
            let dist = levenshtein_distance(
                s1.as_ptr(),
                s1.len(),
                s2.as_ptr(),
                s2.len(),
            );
            assert_eq!(dist, 3);
        }

        let s3 = "hello";
        let s4 = "hello";

        unsafe {
            let dist = levenshtein_distance(
                s3.as_ptr(),
                s3.len(),
                s4.as_ptr(),
                s4.len(),
            );
            assert_eq!(dist, 0);
        }
    }

    #[test]
    fn test_contains_keywords() {
        let text = "Este es un libro de ciencia ficción";
        let keywords = vec![
            CString::new("ciencia").unwrap(),
            CString::new("ficción").unwrap(),
        ];
        let keyword_ptrs: Vec<*const c_char> = keywords.iter().map(|s| s.as_ptr()).collect();

        unsafe {
            assert!(contains_keywords(
                text.as_ptr(),
                text.len(),
                keyword_ptrs.as_ptr(),
                keyword_ptrs.len(),
            ));
        }
    }
}
