#!/usr/bin/env python3
"""Script simplificado para incorporar libros pre-1900."""

import os
import shutil
import unicodedata
from pathlib import Path
from datetime import datetime


def remove_accents(text):
    nfd = unicodedata.normalize('NFD', text)
    return ''.join(c for c in nfd if unicodedata.category(c) != 'Mn')


def normalize_author(name):
    if not name or not name.strip():
        return ""
    normalized = name.lower()
    normalized = remove_accents(normalized)
    result = ''.join(c if c.isalnum() or c.isspace() else '' for c in normalized)
    tokens = [t for t in result.split() if len(t) > 1]
    tokens.sort()
    return ' '.join(tokens)


CLASSIC_AUTHORS = {
    'cervantes', 'shakespeare', 'dante', 'homero', 'virgilio',
    'tolstoi', 'dostoievski', 'dostoyevski', 'dickens', 'balzac',
    'flaubert', 'hugo', 'dumas', 'verne', 'poe', 'wilde', 'goethe',
    'defoe', 'swift', 'austen', 'shelley', 'stoker', 'stevenson',
    'darwin', 'descartes', 'kant', 'rousseau', 'voltaire', 'alarcon',
    'alas', 'clarin', 'galdos', 'zola', 'chejov', 'pushkin'
}


def is_pre1900_author(author):
    author_norm = normalize_author(author)
    tokens = set(author_norm.split())
    return bool(tokens & CLASSIC_AUTHORS)


def extract_author_from_filename(filename):
    if ',' in filename:
        return filename.split(',')[0].strip()
    if ' - ' in filename:
        return filename.split(' - ')[0].strip()
    return None


def main():
    source_base = Path(r"c:\p2p\emule")
    dest_base = Path(r"c:\p2p\biblioteca_pre1900")
    
    dest_base.mkdir(exist_ok=True)
    
    log_file = dest_base / f"incorporacion_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
    
    with open(log_file, 'w', encoding='utf-8') as log:
        log.write(f"INCORPORACION INICIADA: {datetime.now()}\n")
        log.write("="*70 + "\n\n")
        
        folder1 = source_base / "849 Libros Clasicos En Español De La Literatura Universal - Pdf -- Muy Bien"
        folder2 = source_base / "_2600 Libros Literatura Universal En Español Por Morgan"
        
        copied_f1 = 0
        copied_f2 = 0
        filtered_f2 = 0
        
        log.write("CARPETA 1: Incorporacion completa (95%+ pre-1900)\n")
        log.write(f"Origen: {folder1}\n\n")
        
        if folder1.exists():
            for root, dirs, files in os.walk(folder1):
                root_path = Path(root)
                
                author_folder = None
                for part in root_path.parts:
                    if part not in ['Clásicos en Español', folder1.name] and part[0].isupper():
                        author_folder = part
                        break
                
                if not author_folder:
                    continue
                
                dest_author = dest_base / author_folder
                dest_author.mkdir(exist_ok=True)
                
                for file in files:
                    ext = Path(file).suffix.lower()
                    if ext not in ['.pdf', '.epub', '.txt', '.doc', '.docx', '.mobi']:
                        continue
                    
                    src_file = root_path / file
                    dst_file = dest_author / file
                    
                    if not dst_file.exists():
                        try:
                            shutil.copy2(src_file, dst_file)
                            copied_f1 += 1
                            log.write(f"[F1] Copiado: {author_folder} / {file}\n")
                        except Exception as e:
                            log.write(f"[F1] ERROR: {file} - {e}\n")
        
        log.write(f"\nCARPETA 1 COMPLETADA: {copied_f1} archivos copiados\n")
        log.write("="*70 + "\n\n")
        
        log.write("CARPETA 2: Incorporacion filtrada (solo pre-1900)\n")
        log.write(f"Origen: {folder2}\n\n")
        
        if folder2.exists():
            for root, dirs, files in os.walk(folder2):
                root_path = Path(root)
                
                for file in files:
                    ext = Path(file).suffix.lower()
                    if ext not in ['.pdf', '.epub', '.txt', '.doc', '.docx', '.mobi', '.rtf', '.htm', '.html']:
                        continue
                    
                    author = extract_author_from_filename(file)
                    if not author:
                        continue
                    
                    if not is_pre1900_author(author):
                        filtered_f2 += 1
                        continue
                    
                    author_clean = ''.join(c if c.isalnum() or c.isspace() else '_' for c in author)
                    author_clean = ' '.join(author_clean.split())
                    
                    dest_author = dest_base / author_clean
                    dest_author.mkdir(exist_ok=True)
                    
                    src_file = root_path / file
                    dst_file = dest_author / file
                    
                    if not dst_file.exists():
                        try:
                            shutil.copy2(src_file, dst_file)
                            copied_f2 += 1
                            log.write(f"[F2] Copiado: {author_clean} / {file}\n")
                        except Exception as e:
                            log.write(f"[F2] ERROR: {file} - {e}\n")
        
        log.write(f"\nCARPETA 2 COMPLETADA: {copied_f2} copiados, {filtered_f2} filtrados\n")
        log.write("="*70 + "\n\n")
        
        log.write(f"RESUMEN FINAL:\n")
        log.write(f"  Carpeta 1: {copied_f1} archivos\n")
        log.write(f"  Carpeta 2: {copied_f2} archivos (pre-1900)\n")
        log.write(f"  Filtrados: {filtered_f2} archivos (post-1900)\n")
        log.write(f"  TOTAL INCORPORADO: {copied_f1 + copied_f2} archivos\n")
        log.write(f"\nFINALIZADO: {datetime.now()}\n")
    
    print(f"Incorporacion completada. Ver log: {log_file}")
    return 0


if __name__ == "__main__":
    try:
        exit(main())
    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()
        exit(1)
