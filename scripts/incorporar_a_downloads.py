#!/usr/bin/env python3
r"""Incorpora libros pre-1900 a c:\p2p\downloads organizados por autor."""

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
    'alas', 'clarin', 'galdos', 'zola', 'chejov', 'pushkin', 'gogol',
    'aristoteles', 'platon', 'sofocles', 'euripides', 'esquilo',
    'maquiavelo', 'moliere', 'racine', 'calderon', 'lope', 'quevedo',
    'tirso', 'gongora', 'pardo', 'bazan', 'valera', 'baudelaire',
    'rimbaud', 'verlaine', 'mallarme', 'gautier', 'nerval',
    'lamartine', 'musset', 'stendhal', 'maupassant', 'ibsen'
}


def is_pre1900_author(author):
    author_norm = normalize_author(author)
    tokens = set(author_norm.split())
    return bool(tokens & CLASSIC_AUTHORS)


def extract_author_from_filename(filename):
    name_without_ext = os.path.splitext(filename)[0]
    
    if ',' in name_without_ext:
        author = name_without_ext.split(',')[0].strip()
        if author and len(author) > 2:
            return author
    
    if ' - ' in name_without_ext:
        author = name_without_ext.split(' - ')[0].strip()
        if author and len(author) > 2:
            return author
    
    parts = name_without_ext.split()
    if len(parts) >= 2:
        potential_author = ' '.join(parts[:2])
        if potential_author and len(potential_author) > 5:
            return potential_author
    
    return None


def main():
    source_base = Path(r"c:\p2p\emule")
    dest_base = Path(r"c:\p2p\downloads")
    
    dest_base.mkdir(exist_ok=True)
    
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    log_file = dest_base / f"incorporacion_{timestamp}.log"
    
    stats = {
        'carpeta1_copiados': 0,
        'carpeta1_filtrados': 0,
        'carpeta1_omitidos': 0,
        'carpeta2_copiados': 0,
        'carpeta2_filtrados': 0,
        'carpeta2_omitidos': 0,
        'errores': 0
    }
    
    with open(log_file, 'w', encoding='utf-8') as log:
        log.write(f"INCORPORACION A c:\\p2p\\downloads\n")
        log.write(f"Fecha: {datetime.now()}\n")
        log.write("="*70 + "\n\n")
        
        folder1 = source_base / "5000 Libros en formato EPUB LIT LRF Castellano Spanish"
        
        log.write("CARPETA 1: 5000 Libros EPUB LIT LRF\n")
        log.write(f"Origen: {folder1}\n")
        log.write("Estrategia: Filtrado pre-1900 (solo autores clasicos)\n\n")
        
        if folder1.exists():
            for root, dirs, files in os.walk(folder1):
                root_path = Path(root)
                
                for file in files:
                    file_path = root_path / file
                    ext = file_path.suffix.lower()
                    
                    if ext not in ['.pdf', '.epub', '.txt', '.doc', '.docx', '.mobi', '.lit', '.lrf', '.rtf']:
                        continue
                    
                    author = extract_author_from_filename(file)
                    if not author:
                        stats['carpeta1_filtrados'] += 1
                        continue
                    
                    if not is_pre1900_author(author):
                        stats['carpeta1_filtrados'] += 1
                        continue
                    
                    author_clean = ''.join(c if c.isalnum() or c.isspace() else '_' for c in author)
                    author_clean = ' '.join(author_clean.split())
                    
                    dest_author = dest_base / author_clean
                    dest_author.mkdir(exist_ok=True)
                    
                    dest_file = dest_author / file
                    
                    if dest_file.exists():
                        stats['carpeta1_omitidos'] += 1
                        continue
                    
                    try:
                        shutil.copy2(file_path, dest_file)
                        stats['carpeta1_copiados'] += 1
                        log.write(f"[F1] COPIADO: {author_clean} / {file}\n")
                    except Exception as e:
                        stats['errores'] += 1
                        log.write(f"[F1] ERROR: {file} - {e}\n")
        
        log.write(f"\nCARPETA 1 COMPLETADA:\n")
        log.write(f"  Copiados: {stats['carpeta1_copiados']}\n")
        log.write(f"  Filtrados (post-1900): {stats['carpeta1_filtrados']}\n")
        log.write(f"  Omitidos: {stats['carpeta1_omitidos']}\n")
        log.write("="*70 + "\n\n")
        
        folder2 = source_base / "Libros 6000"
        
        log.write("CARPETA 2: Libros 6000\n")
        log.write(f"Origen: {folder2}\n")
        log.write("Estrategia: Filtrado pre-1900 (solo autores clasicos)\n\n")
        
        if folder2.exists():
            for root, dirs, files in os.walk(folder2):
                root_path = Path(root)
                
                for file in files:
                    file_path = root_path / file
                    ext = file_path.suffix.lower()
                    
                    if ext not in ['.pdf', '.epub', '.txt', '.doc', '.docx', '.mobi', '.rtf']:
                        continue
                    
                    author = extract_author_from_filename(file)
                    if not author:
                        stats['carpeta2_filtrados'] += 1
                        log.write(f"[F2] FILTRADO (sin autor): {file}\n")
                        continue
                    
                    if not is_pre1900_author(author):
                        stats['carpeta2_filtrados'] += 1
                        log.write(f"[F2] FILTRADO (post-1900): {author} / {file}\n")
                        continue
                    
                    author_clean = ''.join(c if c.isalnum() or c.isspace() else '_' for c in author)
                    author_clean = ' '.join(author_clean.split())
                    
                    dest_author = dest_base / author_clean
                    dest_author.mkdir(exist_ok=True)
                    
                    dest_file = dest_author / file
                    
                    if dest_file.exists():
                        stats['carpeta2_omitidos'] += 1
                        log.write(f"[F2] OMITIDO (existe): {author_clean} / {file}\n")
                        continue
                    
                    try:
                        shutil.copy2(file_path, dest_file)
                        stats['carpeta2_copiados'] += 1
                        log.write(f"[F2] COPIADO: {author_clean} / {file}\n")
                    except Exception as e:
                        stats['errores'] += 1
                        log.write(f"[F2] ERROR: {file} - {e}\n")
        
        log.write(f"\nCARPETA 2 COMPLETADA:\n")
        log.write(f"  Copiados: {stats['carpeta2_copiados']}\n")
        log.write(f"  Filtrados (post-1900): {stats['carpeta2_filtrados']}\n")
        log.write(f"  Omitidos: {stats['carpeta2_omitidos']}\n")
        log.write("="*70 + "\n\n")
        
        total_incorporados = stats['carpeta1_copiados'] + stats['carpeta2_copiados']
        total_omitidos = stats['carpeta1_omitidos'] + stats['carpeta2_omitidos']
        total_filtrados = stats['carpeta1_filtrados'] + stats['carpeta2_filtrados']
        
        log.write(f"RESUMEN FINAL:\n")
        log.write(f"  Total incorporados: {total_incorporados}\n")
        log.write(f"  Total omitidos (ya existian): {total_omitidos}\n")
        log.write(f"  Filtrados (post-1900): {total_filtrados}\n")
        log.write(f"  Errores: {stats['errores']}\n")
        log.write(f"\nFINALIZADO: {datetime.now()}\n")
    
    print(f"\n{'='*70}")
    print(f"INCORPORACION COMPLETADA")
    print(f"{'='*70}")
    print(f"Carpeta 1: {stats['carpeta1_copiados']} copiados, {stats['carpeta1_filtrados']} filtrados")
    print(f"Carpeta 2: {stats['carpeta2_copiados']} copiados, {stats['carpeta2_filtrados']} filtrados")
    print(f"Total incorporados: {total_incorporados}")
    print(f"Log: {log_file}")
    print(f"{'='*70}\n")
    
    return 0


if __name__ == "__main__":
    try:
        exit(main())
    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()
        exit(1)
