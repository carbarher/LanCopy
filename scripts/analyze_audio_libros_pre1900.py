#!/usr/bin/env python3
"""Analiza carpetas de libros para verificar si son pre-1900 y normalizar autores."""

import os
import re
import unicodedata
import csv
import json
from pathlib import Path
from typing import Dict, List, Set, Tuple, Optional
from collections import defaultdict


class AuthorNormalizer:
    """Normaliza nombres de autor siguiendo la lógica de AuthorNormalizer.cs"""
    
    @staticmethod
    def remove_accents(text: str) -> str:
        """Elimina acentos de un texto."""
        nfd = unicodedata.normalize('NFD', text)
        return ''.join(char for char in nfd if unicodedata.category(char) != 'Mn')
    
    @staticmethod
    def normalize(name: str) -> str:
        """Normaliza un nombre de autor (equivalente a NormalizeFallback en C#)."""
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


class Pre1900Analyzer:
    """Analiza carpetas de libros y verifica si son pre-1900."""
    
    def __init__(self, root_dir: str, pre1900_lists: List[str]):
        self.root_dir = Path(root_dir)
        self.pre1900_works: Dict[str, Set[str]] = defaultdict(set)
        self.normalizer = AuthorNormalizer()
        self._load_pre1900_lists(pre1900_lists)
    
    def _load_pre1900_lists(self, list_files: List[str]):
        """Carga las listas de obras pre-1900."""
        for list_file in list_files:
            if not os.path.exists(list_file):
                print(f"⚠️  Lista no encontrada: {list_file}")
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
    
    def _extract_author_from_path(self, path: str) -> Optional[str]:
        """Extrae el nombre del autor de una ruta de carpeta o archivo."""
        parts = Path(path).parts
        
        for part in reversed(parts):
            if 'Clásicos en Español' in part or 'LIBROS_' in part:
                continue
            
            if ',' in part:
                author_part = part.split(',')[0].strip()
                return author_part
            
            match = re.match(r'^([A-Z][a-zá-úñ]+(?:\s+[A-Z][a-zá-úñ]+)*)', part)
            if match:
                return match.group(1)
        
        return None
    
    def _extract_title_from_filename(self, filename: str) -> Optional[str]:
        """Extrae el título de un nombre de archivo."""
        name = Path(filename).stem
        
        if ' - ' in name:
            parts = name.split(' - ', 1)
            if len(parts) > 1:
                return parts[1].strip()
        
        return name
    
    def _is_pre1900(self, author: str, title: str) -> Tuple[bool, str]:
        """Verifica si una obra es pre-1900."""
        author_norm = self.normalizer.normalize(author)
        title_norm = self.normalizer.normalize(title)
        
        if author_norm in self.pre1900_works:
            if title_norm in self.pre1900_works[author_norm]:
                return True, "Coincidencia exacta en lista pre-1900"
            
            for known_title in self.pre1900_works[author_norm]:
                if title_norm in known_title or known_title in title_norm:
                    return True, f"Coincidencia parcial con: {known_title}"
        
        year_match = re.search(r'\b(1[0-8]\d{2}|19[0]{2})\b', title)
        if year_match:
            year = int(year_match.group(1))
            if year < 1900:
                return True, f"Año detectado en título: {year}"
        
        classic_authors = {
            'cervantes', 'shakespeare', 'dante', 'homero', 'virgilio',
            'ovidio', 'sofocles', 'euripides', 'esquilo', 'aristofanes',
            'platon', 'aristoteles', 'ciceron', 'seneca', 'marco aurelio',
            'tolstoi', 'dostoievski', 'dickens', 'balzac', 'flaubert',
            'stendhal', 'hugo', 'dumas', 'verne', 'poe', 'melville',
            'twain', 'wilde', 'goethe', 'schiller', 'nietzsche',
            'pushkin', 'gogol', 'turguenev', 'chejov', 'ibsen'
        }
        
        author_tokens = set(author_norm.split())
        if author_tokens & classic_authors:
            return True, "Autor clásico conocido (pre-1900)"
        
        return False, "No verificado como pre-1900"
    
    def analyze_directory(self, subdir: str = "") -> List[Dict]:
        """Analiza un directorio y retorna lista de obras encontradas."""
        results = []
        target_dir = self.root_dir / subdir if subdir else self.root_dir
        
        if not target_dir.exists():
            print(f"❌ Directorio no existe: {target_dir}")
            return results
        
        print(f"\n📂 Analizando: {target_dir}")
        
        for root, dirs, files in os.walk(target_dir):
            root_path = Path(root)
            
            author = self._extract_author_from_path(str(root_path))
            
            for file in files:
                if file.startswith('.') or file.startswith('~'):
                    continue
                
                ext = Path(file).suffix.lower()
                if ext not in ['.pdf', '.epub', '.mobi', '.azw3', '.txt', '.doc', '.docx']:
                    continue
                
                title = self._extract_title_from_filename(file)
                if not author:
                    author = self._extract_author_from_path(file)
                
                if not author:
                    author = "Desconocido"
                
                is_pre1900, reason = self._is_pre1900(author, title or file)
                
                results.append({
                    'author': author,
                    'author_normalized': self.normalizer.normalize(author),
                    'title': title or file,
                    'file': file,
                    'path': str(root_path.relative_to(self.root_dir)),
                    'is_pre1900': is_pre1900,
                    'verification_reason': reason,
                    'extension': ext
                })
        
        return results
    
    def generate_report(self, results: List[Dict], output_file: str = "analisis_pre1900.csv"):
        """Genera un reporte CSV con los resultados."""
        if not results:
            print("\n⚠️  No se encontraron resultados para reportar.")
            return
        
        with open(output_file, 'w', newline='', encoding='utf-8-sig') as f:
            fieldnames = ['author', 'author_normalized', 'title', 'file', 'path', 
                         'is_pre1900', 'verification_reason', 'extension']
            writer = csv.DictWriter(f, fieldnames=fieldnames)
            writer.writeheader()
            writer.writerows(results)
        
        print(f"\n✅ Reporte generado: {output_file}")
    
    def print_summary(self, results: List[Dict]):
        """Imprime un resumen del análisis."""
        if not results:
            print("\n⚠️  No se encontraron libros para analizar.")
            return
        
        total = len(results)
        pre1900 = sum(1 for r in results if r['is_pre1900'])
        not_verified = total - pre1900
        
        print(f"\n{'='*60}")
        print(f"📊 RESUMEN DEL ANÁLISIS")
        print(f"{'='*60}")
        print(f"Total de libros encontrados: {total}")
        print(f"✅ Verificados como pre-1900: {pre1900} ({pre1900/total*100:.1f}%)")
        print(f"❓ No verificados: {not_verified} ({not_verified/total*100:.1f}%)")
        
        authors = defaultdict(int)
        for r in results:
            if r['is_pre1900']:
                authors[r['author']] += 1
        
        print(f"\n📚 Top 10 autores pre-1900 encontrados:")
        for author, count in sorted(authors.items(), key=lambda x: x[1], reverse=True)[:10]:
            print(f"  • {author}: {count} obra(s)")
        
        print(f"\n💡 RECOMENDACIÓN:")
        if pre1900 / total > 0.7:
            print(f"  ✅ Alta proporción de obras pre-1900. Recomendado para incorporar.")
        elif pre1900 / total > 0.4:
            print(f"  ⚠️  Proporción media de obras pre-1900. Revisar manualmente.")
        else:
            print(f"  ❌ Baja proporción de obras pre-1900. No recomendado sin revisión.")
        print(f"{'='*60}\n")


def main() -> int:
    base_dir = r"c:\p2p\emule"
    pre1900_lists = [
        r"c:\p2p\novelas_pre1900_gutenberg_anylang.txt",
        r"c:\p2p\novelas_1000_pre1900_mix_es_titles.txt"
    ]
    
    folders = [
        "849 Libros Clasicos En Español De La Literatura Universal - Pdf -- Muy Bien",
        "_2600 Libros Literatura Universal En Español Por Morgan"
    ]
    
    print("🔍 Iniciando análisis de libros pre-1900...\n")
    
    all_results = []
    
    for folder in folders:
        analyzer = Pre1900Analyzer(base_dir, pre1900_lists)
        results = analyzer.analyze_directory(folder)
        all_results.extend(results)
        
        print(f"\n📋 Resultados para: {folder}")
        analyzer.print_summary(results)
        
        output_file = f"analisis_{folder[:30].replace(' ', '_')}.csv"
        analyzer.generate_report(results, output_file)
    
    if len(folders) > 1:
        print(f"\n{'='*60}")
        print(f"📊 RESUMEN GLOBAL (TODAS LAS CARPETAS)")
        print(f"{'='*60}")
        combined_analyzer = Pre1900Analyzer(base_dir, pre1900_lists)
        combined_analyzer.print_summary(all_results)
        combined_analyzer.generate_report(all_results, "analisis_completo_pre1900.csv")
    
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
