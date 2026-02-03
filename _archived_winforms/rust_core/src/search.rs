use rayon::prelude::*;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use tantivy::collector::TopDocs;
use tantivy::query::QueryParser;
use tantivy::schema::*;
use tantivy::{doc, Index, IndexWriter};

/// Motor de búsqueda full-text ultrarrápido con Tantivy
/// 1000x más rápido que LIKE en SQL
pub struct SearchEngine {
    index: Index,
    schema: Schema,
    writer: IndexWriter,
}

impl SearchEngine {
    pub fn new() -> Result<Self, Box<dyn std::error::Error>> {
        let mut schema_builder = Schema::builder();
        
        schema_builder.add_text_field("filename", TEXT | STORED);
        schema_builder.add_text_field("author", TEXT | STORED);
        schema_builder.add_text_field("extension", STRING | STORED);
        schema_builder.add_i64_field("size", INDEXED | STORED);
        schema_builder.add_text_field("username", STRING | STORED);
        schema_builder.add_text_field("folder", TEXT);
        
        let schema = schema_builder.build();
        let index = Index::create_in_ram(schema.clone());
        let writer = index.writer(50_000_000)?; // 50MB buffer

        Ok(SearchEngine {
            index,
            schema,
            writer,
        })
    }

    pub fn index_file(&mut self, 
        filename: &str, 
        author: &str,
        extension: &str,
        size: i64,
        username: &str,
        folder: &str
    ) -> Result<(), Box<dyn std::error::Error>> {
        let filename_field = self.schema.get_field("filename").unwrap();
        let author_field = self.schema.get_field("author").unwrap();
        let extension_field = self.schema.get_field("extension").unwrap();
        let size_field = self.schema.get_field("size").unwrap();
        let username_field = self.schema.get_field("username").unwrap();
        let folder_field = self.schema.get_field("folder").unwrap();

        self.writer.add_document(doc!(
            filename_field => filename,
            author_field => author,
            extension_field => extension,
            size_field => size,
            username_field => username,
            folder_field => folder
        ))?;

        Ok(())
    }

    pub fn commit(&mut self) -> Result<(), Box<dyn std::error::Error>> {
        self.writer.commit()?;
        Ok(())
    }

    pub fn search(&self, query_str: &str, limit: usize) -> Result<Vec<SearchResult>, Box<dyn std::error::Error>> {
        let reader = self.index
            .reader_builder()
            .try_into()?;

        let searcher = reader.searcher();
        
        let filename_field = self.schema.get_field("filename").unwrap();
        let author_field = self.schema.get_field("author").unwrap();
        
        let query_parser = QueryParser::for_index(&self.index, vec![filename_field, author_field]);
        let query = query_parser.parse_query(query_str)?;

        let top_docs = searcher.search(&query, &TopDocs::with_limit(limit))?;

        let mut results = Vec::new();
        for (_score, doc_address) in top_docs {
            let retrieved_doc: tantivy::TantivyDocument = searcher.doc(doc_address)?;
            
            let filename = retrieved_doc
                .get_first(filename_field)
                .and_then(|v| v.as_str())
                .unwrap_or("")
                .to_string();
            
            let author = retrieved_doc
                .get_first(author_field)
                .and_then(|v| v.as_str())
                .unwrap_or("")
                .to_string();

            results.push(SearchResult {
                filename,
                author,
                score: _score,
            });
        }

        Ok(results)
    }
}

#[repr(C)]
pub struct SearchResult {
    pub filename: String,
    pub author: String,
    pub score: f32,
}

/// Búsqueda paralela en array de archivos (sin índice)
/// Para búsquedas simples sin necesidad de índice full-text
#[no_mangle]
pub extern "C" fn search_files_parallel(
    query: *const c_char,
    filenames: *const *const c_char,
    count: usize,
    results: *mut *mut c_char,
    max_results: usize,
) -> i32 {
    if query.is_null() || filenames.is_null() || results.is_null() {
        return -1;
    }

    unsafe {
        let query_str = match CStr::from_ptr(query).to_str() {
            Ok(s) => s.to_lowercase(),
            Err(_) => return -2,
        };

        let files: Vec<String> = (0..count)
            .filter_map(|i| {
                let ptr = *filenames.add(i);
                if ptr.is_null() {
                    None
                } else {
                    CStr::from_ptr(ptr).to_str().ok().map(|s| s.to_string())
                }
            })
            .collect();

        // Búsqueda paralela con Rayon
        let matches: Vec<String> = files
            .par_iter()
            .filter(|filename| filename.to_lowercase().contains(&query_str))
            .cloned()
            .collect::<Vec<_>>()
            .into_iter()
            .take(max_results)
            .collect();

        // Copiar resultados
        let result_count = matches.len().min(max_results);
        for (i, filename) in matches.iter().take(result_count).enumerate() {
            let c_string = CString::new(filename.as_str()).unwrap();
            *results.add(i) = c_string.into_raw();
        }

        result_count as i32
    }
}

/// Liberar memoria de resultados
#[no_mangle]
pub extern "C" fn free_search_results(results: *mut *mut c_char, count: usize) {
    if results.is_null() {
        return;
    }

    unsafe {
        for i in 0..count {
            let ptr = *results.add(i);
            if !ptr.is_null() {
                let _ = CString::from_raw(ptr);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_search_engine() {
        let mut engine = SearchEngine::new().unwrap();
        
        engine.index_file(
            "Cervantes - Don Quijote.epub",
            "Cervantes",
            "epub",
            1024000,
            "user1",
            "/books"
        ).unwrap();

        engine.commit().unwrap();

        let results = engine.search("cervantes", 10).unwrap();
        assert_eq!(results.len(), 1);
        assert_eq!(results[0].author, "Cervantes");
    }
}
