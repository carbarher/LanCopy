use std::ffi::CStr;
use libc::c_char;

// Palabras comunes en español (sin acentos para comparación más robusta)
const SPANISH_WORDS: &[&str] = &[
    "el", "la", "de", "que", "y", "a", "en", "un", "ser", "se", "no", "haber",
    "por", "con", "su", "para", "como", "estar", "tener", "le", "lo", "todo",
    "pero", "mas", "hacer", "o", "poder", "decir", "este", "ir", "otro", "ese",
    "si", "me", "ya", "ver", "porque", "dar", "cuando", "muy", "sin",
    "vez", "mucho", "saber", "sobre", "mi", "alguno", "mismo", "yo",
    "tambien", "hasta", "ano", "dos", "querer", "entre", "asi", "primero",
    "desde", "grande", "eso", "ni", "nos", "llegar", "pasar", "tiempo", "ella",
    "dia", "uno", "bien", "poco", "deber", "entonces", "poner", "cosa",
    "tanto", "hombre", "parecer", "nuestro", "tan", "donde", "ahora", "parte",
    "despues", "vida", "quedar", "siempre", "creer", "hablar", "llevar", "dejar",
    "nada", "cada", "seguir", "menos", "nuevo", "encontrar", "algo", "solo",
    "casa", "usar", "pais", "tal", "durante", "mundo", "aunque",
    "contra", "propio", "forma", "cual", "general", "mayor", "nacional",
    "social", "politico", "economico", "espanol", "historia", "gobierno", "sistema",
    "segun", "ademas", "ejemplo", "traves", "mientras", "manera", "caso", "grupo",
    "nivel", "obra", "proceso", "resultado", "tipo", "desarrollo", "problema",
    "estado", "ciudad", "nombre", "lugar", "momento", "numero", "punto", "razon",
    "servicio", "situacion", "zona", "area", "base", "centro", "condicion",
    "conocimiento", "control", "cuenta", "curso", "dato", "derecho", "efecto",
    "empresa", "epoca", "espacio", "estudio", "evento", "familia", "figura",
    "funcion", "gente", "idea", "imagen", "informacion", "interes", "linea",
    "lista", "medio", "medida", "miembro", "modelo", "modo", "motivo", "objeto",
    "objetivo", "ocasion", "opinion", "orden", "organizacion", "papel", "periodo",
    "persona", "plan", "poblacion", "posibilidad", "posicion", "practica", "precio",
    "presencia", "presente", "presidente", "principio", "producto", "programa",
    "proyecto", "pueblo", "puesto", "relacion", "respuesta", "resto", "sector",
    "sentido", "serie", "tema", "teoria", "termino", "texto",
    "titulo", "trabajo", "unidad", "uso", "valor", "vista",
    // Palabras adicionales muy comunes
    "del", "los", "las", "una", "unos", "unas", "al", "fue", "era", "son", "esta",
    "estos", "estas", "ese", "esa", "esos", "esas", "aquel", "aquella", "aquellos",
    "aquellas", "quien", "quienes", "cual", "cuales", "cuanto", "cuanta", "cuantos",
    "cuantas", "donde", "cuando", "como", "porque", "aunque", "sino", "pero"
];

const SPANISH_CHARS: &[char] = &['á', 'é', 'í', 'ó', 'ú', 'ñ', 'ü', '¿', '¡'];

// Función para normalizar acentos (simplificada)
fn remove_accents(text: &str) -> String {
    text.chars()
        .map(|c| match c {
            'á' | 'à' | 'ä' | 'â' => 'a',
            'é' | 'è' | 'ë' | 'ê' => 'e',
            'í' | 'ì' | 'ï' | 'î' => 'i',
            'ó' | 'ò' | 'ö' | 'ô' => 'o',
            'ú' | 'ù' | 'ü' | 'û' => 'u',
            'Á' | 'À' | 'Ä' | 'Â' => 'A',
            'É' | 'È' | 'Ë' | 'Ê' => 'E',
            'Í' | 'Ì' | 'Ï' | 'Î' => 'I',
            'Ó' | 'Ò' | 'Ö' | 'Ô' => 'O',
            'Ú' | 'Ù' | 'Ü' | 'Û' => 'U',
            _ => c,
        })
        .collect()
}

#[no_mangle]
pub extern "C" fn is_spanish_text(text_ptr: *const c_char) -> i32 {
    if text_ptr.is_null() {
        return 0;
    }

    let text = unsafe {
        match CStr::from_ptr(text_ptr).to_str() {
            Ok(s) => s,
            Err(_) => return 0,
        }
    };

    if text.is_empty() || text.len() < 10 {
        return 0;
    }

    // Verificar caracteres españoles primero
    let has_spanish_chars = text.chars().any(|c| SPANISH_CHARS.contains(&c));
    if has_spanish_chars {
        return 1;
    }

    // Normalizar y convertir a minúsculas
    let text_normalized = remove_accents(&text.to_lowercase());
    
    let words: Vec<&str> = text_normalized
        .split(|c: char| !c.is_alphanumeric())
        .filter(|w| w.len() >= 2)
        .collect();

    if words.is_empty() {
        return 0;
    }

    let spanish_word_count = words
        .iter()
        .filter(|w| SPANISH_WORDS.contains(w))
        .count();

    // Si hay 3 o más palabras españolas, considerarlo español
    if spanish_word_count >= 3 {
        return 1;
    }

    // Para textos cortos (menos de 20 palabras), 2 palabras españolas es suficiente
    if words.len() < 20 && spanish_word_count >= 2 {
        return 1;
    }

    let ratio = spanish_word_count as f64 / words.len() as f64;

    // Reducir umbral a 5% para ser más permisivo
    if ratio >= 0.05 {
        1
    } else {
        0
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CString;

    #[test]
    fn test_spanish_text() {
        let text = CString::new("Este es un texto en español con palabras comunes").unwrap();
        assert_eq!(is_spanish_text(text.as_ptr()), 1);
    }

    #[test]
    fn test_english_text() {
        let text = CString::new("This is an English text with common words").unwrap();
        assert_eq!(is_spanish_text(text.as_ptr()), 0);
    }

    #[test]
    fn test_spanish_chars() {
        let text = CString::new("Año español niño").unwrap();
        assert_eq!(is_spanish_text(text.as_ptr()), 1);
    }

    #[test]
    fn test_empty_text() {
        let text = CString::new("").unwrap();
        assert_eq!(is_spanish_text(text.as_ptr()), 0);
    }
}
