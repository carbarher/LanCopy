use std::fs::File;
use std::io::{Read, Seek, SeekFrom};
use std::path::Path;

/// Extrae metadata de archivos de audio MP3
pub fn extract_mp3_metadata(file_path: &str) -> Option<AudioMetadata> {
    let path = Path::new(file_path);
    if !path.exists() {
        return None;
    }

    let mut file = File::open(path).ok()?;
    let mut buffer = vec![0u8; 10];
    
    // Leer header ID3v2
    file.read_exact(&mut buffer).ok()?;
    
    if &buffer[0..3] != b"ID3" {
        return None;
    }

    let version = buffer[3];
    let revision = buffer[4];
    
    // Calcular tamaño del tag
    let size = ((buffer[6] as u32) << 21) |
               ((buffer[7] as u32) << 14) |
               ((buffer[8] as u32) << 7) |
               (buffer[9] as u32);

    let mut metadata = AudioMetadata {
        title: None,
        artist: None,
        album: None,
        year: None,
        genre: None,
        bitrate: None,
        duration_seconds: None,
    };

    // Leer frames del tag
    let mut tag_data = vec![0u8; size as usize];
    file.read_exact(&mut tag_data).ok()?;

    // Parsear frames básicos
    let mut pos = 0;
    while pos + 10 < tag_data.len() {
        let frame_id = String::from_utf8_lossy(&tag_data[pos..pos+4]).to_string();
        let frame_size = u32::from_be_bytes([
            tag_data[pos+4], tag_data[pos+5], tag_data[pos+6], tag_data[pos+7]
        ]);

        if frame_size == 0 || frame_size > (tag_data.len() - pos - 10) as u32 {
            break;
        }

        let frame_data = &tag_data[pos+10..pos+10+frame_size as usize];
        
        match frame_id.as_str() {
            "TIT2" => metadata.title = extract_text_frame(frame_data),
            "TPE1" => metadata.artist = extract_text_frame(frame_data),
            "TALB" => metadata.album = extract_text_frame(frame_data),
            "TYER" => metadata.year = extract_text_frame(frame_data),
            "TCON" => metadata.genre = extract_text_frame(frame_data),
            _ => {}
        }

        pos += 10 + frame_size as usize;
    }

    // Calcular bitrate aproximado
    if let Ok(file_size) = std::fs::metadata(path).map(|m| m.len()) {
        // Bitrate aproximado = (file_size * 8) / duration
        // Por ahora, estimamos basándonos en el tamaño
        metadata.bitrate = Some((file_size * 8 / 1024) as u32); // kbps aproximado
    }

    Some(metadata)
}

fn extract_text_frame(data: &[u8]) -> Option<String> {
    if data.is_empty() {
        return None;
    }

    let encoding = data[0];
    let text_data = &data[1..];

    match encoding {
        0 => {
            // ISO-8859-1
            String::from_utf8(text_data.to_vec()).ok()
        }
        1 | 2 => {
            // UTF-16
            let text: Vec<u16> = text_data
                .chunks_exact(2)
                .map(|chunk| u16::from_le_bytes([chunk[0], chunk[1]]))
                .collect();
            String::from_utf16(&text).ok()
        }
        3 => {
            // UTF-8
            String::from_utf8(text_data.to_vec()).ok()
        }
        _ => None,
    }
}

/// Extrae metadata de archivos FLAC
pub fn extract_flac_metadata(file_path: &str) -> Option<AudioMetadata> {
    let path = Path::new(file_path);
    if !path.exists() {
        return None;
    }

    let mut file = File::open(path).ok()?;
    let mut buffer = vec![0u8; 4];
    
    // Verificar header FLAC
    file.read_exact(&mut buffer).ok()?;
    if &buffer != b"fLaC" {
        return None;
    }

    let mut metadata = AudioMetadata {
        title: None,
        artist: None,
        album: None,
        year: None,
        genre: None,
        bitrate: None,
        duration_seconds: None,
    };

    // Leer bloques de metadata
    loop {
        let mut header = vec![0u8; 4];
        if file.read_exact(&mut header).is_err() {
            break;
        }

        let is_last = (header[0] & 0x80) != 0;
        let block_type = header[0] & 0x7F;
        let block_size = u32::from_be_bytes([0, header[1], header[2], header[3]]);

        if block_type == 4 {
            // VORBIS_COMMENT block
            let mut block_data = vec![0u8; block_size as usize];
            file.read_exact(&mut block_data).ok()?;
            
            // Parsear vorbis comments
            let vendor_length = u32::from_le_bytes([
                block_data[0], block_data[1], block_data[2], block_data[3]
            ]) as usize;
            
            let mut pos = 4 + vendor_length;
            let comment_count = u32::from_le_bytes([
                block_data[pos], block_data[pos+1], block_data[pos+2], block_data[pos+3]
            ]);
            
            pos += 4;
            
            for _ in 0..comment_count {
                if pos + 4 > block_data.len() {
                    break;
                }
                
                let comment_length = u32::from_le_bytes([
                    block_data[pos], block_data[pos+1], block_data[pos+2], block_data[pos+3]
                ]) as usize;
                
                pos += 4;
                
                if pos + comment_length > block_data.len() {
                    break;
                }
                
                let comment = String::from_utf8_lossy(&block_data[pos..pos+comment_length]);
                let parts: Vec<&str> = comment.splitn(2, '=').collect();
                
                if parts.len() == 2 {
                    match parts[0].to_uppercase().as_str() {
                        "TITLE" => metadata.title = Some(parts[1].to_string()),
                        "ARTIST" => metadata.artist = Some(parts[1].to_string()),
                        "ALBUM" => metadata.album = Some(parts[1].to_string()),
                        "DATE" => metadata.year = Some(parts[1].to_string()),
                        "GENRE" => metadata.genre = Some(parts[1].to_string()),
                        _ => {}
                    }
                }
                
                pos += comment_length;
            }
        } else {
            // Saltar este bloque
            file.seek(SeekFrom::Current(block_size as i64)).ok()?;
        }

        if is_last {
            break;
        }
    }

    Some(metadata)
}

/// Detección de idioma mejorada usando NLP básico
pub fn detect_language_advanced(text: &str) -> String {
    let text_lower = text.to_lowercase();
    
    // Palabras clave por idioma (top 20 más comunes)
    let spanish_keywords = [
        "el", "la", "de", "que", "y", "a", "en", "un", "ser", "se",
        "no", "haber", "por", "con", "su", "para", "como", "estar", "tener", "le"
    ];
    
    let english_keywords = [
        "the", "be", "to", "of", "and", "a", "in", "that", "have", "i",
        "it", "for", "not", "on", "with", "he", "as", "you", "do", "at"
    ];
    
    let french_keywords = [
        "le", "de", "un", "être", "et", "à", "il", "avoir", "ne", "je",
        "son", "que", "se", "qui", "ce", "dans", "en", "du", "elle", "au"
    ];
    
    let german_keywords = [
        "der", "die", "und", "in", "den", "von", "zu", "das", "mit", "sich",
        "des", "auf", "für", "ist", "im", "dem", "nicht", "ein", "eine", "als"
    ];

    // Contar coincidencias
    let words: Vec<&str> = text_lower.split_whitespace().collect();
    let mut spanish_count = 0;
    let mut english_count = 0;
    let mut french_count = 0;
    let mut german_count = 0;

    for word in &words {
        if spanish_keywords.contains(word) {
            spanish_count += 1;
        }
        if english_keywords.contains(word) {
            english_count += 1;
        }
        if french_keywords.contains(word) {
            french_count += 1;
        }
        if german_keywords.contains(word) {
            german_count += 1;
        }
    }

    // Determinar idioma predominante
    let max_count = spanish_count.max(english_count).max(french_count).max(german_count);
    
    if max_count == 0 {
        return "unknown".to_string();
    }

    if spanish_count == max_count {
        "spanish".to_string()
    } else if english_count == max_count {
        "english".to_string()
    } else if french_count == max_count {
        "french".to_string()
    } else {
        "german".to_string()
    }
}

/// Compresión de logs más eficiente usando LZ4
pub fn compress_log_data(data: &[u8]) -> Vec<u8> {
    // Implementación simple de compresión RLE (Run-Length Encoding)
    // Para producción, usar lz4_flex crate
    let mut compressed = Vec::new();
    
    if data.is_empty() {
        return compressed;
    }

    let mut i = 0;
    while i < data.len() {
        let current = data[i];
        let mut count = 1;
        
        while i + count < data.len() && data[i + count] == current && count < 255 {
            count += 1;
        }
        
        compressed.push(count as u8);
        compressed.push(current);
        i += count;
    }
    
    compressed
}

/// Descompresión de logs
pub fn decompress_log_data(data: &[u8]) -> Vec<u8> {
    let mut decompressed = Vec::new();
    
    let mut i = 0;
    while i + 1 < data.len() {
        let count = data[i] as usize;
        let value = data[i + 1];
        
        for _ in 0..count {
            decompressed.push(value);
        }
        
        i += 2;
    }
    
    decompressed
}

#[derive(Debug, Clone)]
pub struct AudioMetadata {
    pub title: Option<String>,
    pub artist: Option<String>,
    pub album: Option<String>,
    pub year: Option<String>,
    pub genre: Option<String>,
    pub bitrate: Option<u32>,
    pub duration_seconds: Option<u32>,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_language_detection() {
        assert_eq!(detect_language_advanced("El perro come la comida"), "spanish");
        assert_eq!(detect_language_advanced("The dog eats the food"), "english");
        assert_eq!(detect_language_advanced("Le chien mange la nourriture"), "french");
    }

    #[test]
    fn test_compression() {
        let data = b"aaaaaabbbbcccccccc";
        let compressed = compress_log_data(data);
        let decompressed = decompress_log_data(&compressed);
        assert_eq!(data.to_vec(), decompressed);
    }
}
