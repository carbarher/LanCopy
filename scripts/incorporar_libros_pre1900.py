#!/usr/bin/env python3
"""Script para incorporar libros pre-1900 a la biblioteca con filtros y normalización."""

import os
import shutil
import unicodedata
import csv
from pathlib import Path
from typing import Dict, List, Set, Tuple
from collections import defaultdict
from datetime import datetime


class AuthorNormalizer:
    """Normaliza nombres de autor siguiendo la lógica de AuthorNormalizer.cs"""
    
    @staticmethod
    def remove_accents(text: str) -> str:
        nfd = unicodedata.normalize('NFD', text)
        return ''.join(char for char in nfd if unicodedata.category(char) != 'Mn')
    
    @staticmethod
    def normalize(name: str) -> str:
        if not name or not name.strip():
            return ""
        
        normalized = name.lower()
        normalized = AuthorNormalizer.remove_accents(normalized)
        
        result = []
        for c in normalized:
            if c.isalnum() or c.isspace():
                result.append(c)
        normalized = ''.join(result)
        
        tokens = [t for t in normalized.split() if len(t) > 1]
        tokens.sort()
        
        return ' '.join(tokens)


class LibraryIncorporator:
    """Incorpora libros pre-1900 a la biblioteca con filtros y normalización."""
    
    def __init__(self, source_dir: str, dest_dir: str, pre1900_lists: List[str]):
        self.source_dir = Path(source_dir)
        self.dest_dir = Path(dest_dir)
        self.normalizer = AuthorNormalizer()
        self.pre1900_works: Dict[str, Set[str]] = defaultdict(set)
        self.classic_authors = {
            'cervantes', 'shakespeare', 'dante', 'homero', 'virgilio',
            'ovidio', 'sofocles', 'euripides', 'esquilo', 'aristofanes',
            'platon', 'aristoteles', 'ciceron', 'seneca', 'marco aurelio',
            'tolstoi', 'dostoievski', 'dostoyevski', 'dickens', 'balzac',
            'flaubert', 'stendhal', 'hugo', 'dumas', 'verne', 'poe',
            'melville', 'twain', 'wilde', 'goethe', 'schiller', 'nietzsche',
            'pushkin', 'gogol', 'turguenev', 'chejov', 'ibsen', 'defoe',
            'swift', 'austen', 'bronte', 'shelley', 'stoker', 'stevenson',
            'carroll', 'doyle', 'darwin', 'descartes', 'kant', 'rousseau',
            'voltaire', 'maquiavelo', 'moliere', 'racine', 'corneille',
            'calderon', 'lope', 'quevedo', 'gongora', 'tirso', 'alarcon',
            'alas', 'clarin', 'galdos', 'valera', 'pardo', 'bazan',
            'zola', 'maupassant', 'baudelaire', 'rimbaud', 'verlaine',
            'mallarme', 'gautier', 'nerval', 'lamartine', 'musset'
        }
        self._load_pre1900_lists(pre1900_lists)
        self.log_entries = []
        
    def _load_pre1900_lists(self, list_files: List[str]):
        for list_file in list_files:
            if not os.path.exists(list_file):
                continue
            
            with open(list_file, 'r', encoding='utf-8') as f:
                for line in f:
                    line = line.strip()
                    if not line or line.startswith('#'):
                        continue
                    
                    if ' - ' in line:
                        author, title = line.split(' - ', 1)
                        author_norm = self.normalizer.normalize(author)
                        title_norm = self.normalizer.normalize(title)
                        self.pre1900_works[author_norm].add(title_norm)
    
    def _is_pre1900(self, author: str, title: str = "") -> Tuple[bool, str]:
        author_norm = self.normalizer.normalize(author)
        
        author_tokens = set(author_norm.split())
        if author_tokens & self.classic_authors:
            return True, "Autor clásico conocido"
        
        if author_norm in self.pre1900_works:
            if not title:
                return True, "Autor en lista pre-1900"
            
            title_norm = self.normalizer.normalize(title)
            if title_norm in self.pre1900_works[author_norm]:
                return True, "Obra verificada en lista"
            
            for known_title in self.pre1900_works[author_norm]:
                if title_norm in known_title or known_title in title_norm:
                    return True, f"Coincidencia parcial"
        
        post1900_keywords = ['siglo xx', 'siglo 20', 'contemporaneo', 'moderno']
        author_lower = author.lower()
        if any(kw in author_lower for kw in post1900_keywords):
            return False, "Indicador post-1900 en nombre"
        
        return False, "No verificado"
    
    def _extract_author_from_path(self, path: Path) -> str:
        parts = path.parts
        
        for part in reversed(parts):
            if any(skip in part for skip in ['Clásicos', 'LIBROS_', 'Literatura']):
                continue
            
            if ',' in part:
                return part.split(',')[0].strip()
            
            if part[0].isupper():
                return part
        
        return "Desconocido"
    
    def _get_dest_path(self, author: str, filename: str) -> Path:
        author_norm = self.normalizer.normalize(author)
        author_clean = ''.join(c if c.isalnum() or c.isspace() else '_' 
                              for c in author)
        author_clean = ' '.join(author_clean.split())
        
        dest_author_dir = self.dest_dir / author_clean
        dest_author_dir.mkdir(parents=True, exist_ok=True)
        
        return dest_author_dir / filename
    
    def incorporate_folder1(self, folder_name: str, dry_run: bool = False):
        """Incorpora Carpeta 1 completa (alta proporción pre-1900)."""
        source = self.source_dir / folder_name
        
        if not source.exists():
            print(f"❌ No existe: {source}")
            return
        
        print(f"\n📂 Incorporando Carpeta 1: {folder_name}")
        print(f"   Estrategia: Copia completa (95%+ pre-1900 verificado)")
        
        copied = 0
        skipped = 0
        
        for root, dirs, files in os.walk(source):
            root_path = Path(root)
            author = self._extract_author_from_path(root_path)
            
            for file in files:
                if file.startswith('.') or file.startswith('~'):
                    continue
                
                ext = Path(file).suffix.lower()
                if ext not in ['.pdf', '.epub', '.mobi', '.azw3', '.txt', 
                              '.doc', '.docx', '.htm', '.html']:
                    continue
                
                source_file = root_path / file
                dest_file = self._get_dest_path(author, file)
                
                if dest_file.exists():
                    skipped += 1
                    self.log_entries.append({
                        'action': 'skip',
                        'author': author,
                        'file': file,
                        'reason': 'Ya existe en destino'
                    })
                    continue
                
                if not dry_run:
                    try:
                        shutil.copy2(source_file, dest_file)
                        copied += 1
                        self.log_entries.append({
                            'action': 'copy',
                            'author': author,
                            'file': file,
                            'source': str(source_file),
                            'dest': str(dest_file)
                        })
                    except Exception as e:
                        self.log_entries.append({
                            'action': 'error',
                            'author': author,
                            'file': file,
                            'error': str(e)
                        })
                else:
                    copied += 1
                    print(f"  [DRY-RUN] Copiaría: {author} - {file}")
        
        print(f"✅ Carpeta 1: {copied} archivos copiados, {skipped} omitidos")
    
    def incorporate_folder2(self, folder_name: str, dry_run: bool = False):
        """Incorpora Carpeta 2 con filtro pre-1900."""
        source = self.source_dir / folder_name
        
        if not source.exists():
            print(f"❌ No existe: {source}")
            return
        
        print(f"\n📂 Incorporando Carpeta 2: {folder_name}")
        print(f"   Estrategia: Filtrar solo pre-1900 (~45% del total)")
        
        copied = 0
        filtered = 0
        skipped = 0
        
        for root, dirs, files in os.walk(source):
            root_path = Path(root)
            
            for file in files:
                if file.startswith('.') or file.startswith('~'):
                    continue
                
                ext = Path(file).suffix.lower()
                if ext not in ['.pdf', '.epub', '.mobi', '.azw3', '.txt',
                              '.doc', '.docx', '.htm', '.html', '.rtf']:
                    continue
                
                author = "Desconocido"
                if ',' in file:
                    author = file.split(',')[0].strip()
                elif ' - ' in file:
                    author = file.split(' - ')[0].strip()
                else:
                    author = self._extract_author_from_path(root_path)
                
                is_pre1900, reason = self._is_pre1900(author, file)
                
                if not is_pre1900:
                    filtered += 1
                    self.log_entries.append({
                        'action': 'filter',
                        'author': author,
                        'file': file,
                        'reason': reason
                    })
                    continue
                
                source_file = root_path / file
                dest_file = self._get_dest_path(author, file)
                
                if dest_file.exists():
                    skipped += 1
                    self.log_entries.append({
                        'action': 'skip',
                        'author': author,
                        'file': file,
                        'reason': 'Ya existe en destino'
                    })
                    continue
                
                if not dry_run:
                    try:
                        shutil.copy2(source_file, dest_file)
                        copied += 1
                        self.log_entries.append({
                            'action': 'copy',
                            'author': author,
                            'file': file,
                            'source': str(source_file),
                            'dest': str(dest_file),
                            'verification': reason
                        })
                    except Exception as e:
                        self.log_entries.append({
                            'action': 'error',
                            'author': author,
                            'file': file,
                            'error': str(e)
                        })
                else:
                    copied += 1
                    print(f"  [DRY-RUN] Copiaría: {author} - {file} ({reason})")
        
        print(f"✅ Carpeta 2: {copied} pre-1900 copiados, "
              f"{filtered} filtrados, {skipped} omitidos")
    
    def generate_log(self, log_file: str = "incorporacion_log.csv"):
        if not self.log_entries:
            return
        
        with open(log_file, 'w', newline='', encoding='utf-8-sig') as f:
            fieldnames = ['action', 'author', 'file', 'source', 'dest',
                         'reason', 'verification', 'error']
            writer = csv.DictWriter(f, fieldnames=fieldnames, extrasaction='ignore')
            writer.writeheader()
            writer.writerows(self.log_entries)
        
        print(f"\n📝 Log generado: {log_file}")


def main() -> int:
    source_dir = r"c:\p2p\emule"
    dest_dir = r"c:\p2p\biblioteca_pre1900"
    
    pre1900_lists = [
        r"c:\p2p\novelas_pre1900_gutenberg_anylang.txt",
        r"c:\p2p\novelas_1000_pre1900_mix_es_titles.txt"
    ]
    
    folders = [
        "849 Libros Clasicos En Español De La Literatura Universal - Pdf -- Muy Bien",
        "_2600 Libros Literatura Universal En Español Por Morgan"
    ]
    
    print("="*70)
    print("🔧 INCORPORACIÓN DE LIBROS PRE-1900 A BIBLIOTECA")
    print("="*70)
    print(f"Origen: {source_dir}")
    print(f"Destino: {dest_dir}")
    print(f"Fecha: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("="*70)
    
    incorporator = LibraryIncorporator(source_dir, dest_dir, pre1900_lists)
    
    incorporator.incorporate_folder1(folders[0], dry_run=False)
    incorporator.incorporate_folder2(folders[1], dry_run=False)
    
    incorporator.generate_log(f"incorporacion_log_{datetime.now().strftime('%Y%m%d_%H%M%S')}.csv")
    
    print("\n" + "="*70)
    print("✅ INCORPORACIÓN COMPLETADA")
    print("="*70)
    
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
