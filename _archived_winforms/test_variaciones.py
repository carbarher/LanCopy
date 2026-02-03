#!/usr/bin/env python3
"""
Script para probar las variaciones de nombres de autores
Simula la lógica de MainForm.cs líneas 16100-16169
"""

import re
import unicodedata

def remove_accents(text):
    """Elimina tildes y acentos"""
    nfd = unicodedata.normalize('NFD', text)
    return ''.join(char for char in nfd if unicodedata.category(char) != 'Mn')

def generate_variations(author):
    """Genera todas las variaciones de un nombre de autor"""
    variations = set()
    
    # 1. Original
    variations.add(author)
    print(f"  1. Original: '{author}'")
    
    # 2-4. Variantes con puntos
    if '.' in author:
        # 2. Sin puntos
        without_dots = author.replace('.', '')
        if without_dots.strip():
            variations.add(without_dots)
            print(f"  2. Sin puntos: '{without_dots}'")
            
            # 3. Sin puntos, espacios normalizados
            normalized = re.sub(r'\s+', ' ', without_dots).strip()
            if normalized:
                variations.add(normalized)
                print(f"  3. Sin puntos, espacios normalizados: '{normalized}'")
        
        # 4. Con puntos, sin espacios entre iniciales
        # "A. E. Nombre" → "A.E. Nombre"
        with_dots_no_spaces = re.sub(r'\.(\s+)(?=[A-Z]\.)', '.', author)
        if with_dots_no_spaces != author and with_dots_no_spaces.strip():
            variations.add(with_dots_no_spaces)
            print(f"  4. Con puntos, sin espacios: '{with_dots_no_spaces}'")
    
    # 5. Sin tildes
    without_accents = remove_accents(author)
    if without_accents != author:
        variations.add(without_accents)
        print(f"  5. Sin tildes: '{without_accents}'")
    
    # 6-7. Variantes sin puntos ni tildes
    if '.' in author:
        without_dots_or_accents = remove_accents(author.replace('.', ''))
        if without_dots_or_accents.strip():
            variations.add(without_dots_or_accents)
            print(f"  6. Sin puntos ni tildes: '{without_dots_or_accents}'")
            
            # 7. Completamente normalizado
            fully_normalized = re.sub(r'\s+', ' ', without_dots_or_accents).strip()
            if fully_normalized:
                variations.add(fully_normalized)
                print(f"  7. Completamente normalizado: '{fully_normalized}'")
    
    return variations

def main():
    test_authors = [
        "A. A. Pepito",
        "J. R. R. Tolkien",
        "A. E. van Vogt",
        "José Saramago",
        "Isaac Asimov"
    ]
    
    print("=" * 60)
    print("TEST DE VARIACIONES DE NOMBRES")
    print("=" * 60)
    
    total_variations = 0
    
    for author in test_authors:
        print(f"\n📖 Autor: {author}")
        variations = generate_variations(author)
        total_variations += len(variations)
        print(f"   Total variaciones: {len(variations)}")
    
    print("\n" + "=" * 60)
    print(f"RESUMEN")
    print("=" * 60)
    print(f"Autores originales: {len(test_authors)}")
    print(f"Total variaciones: {total_variations}")
    print(f"Promedio por autor: {total_variations / len(test_authors):.1f}")

if __name__ == "__main__":
    main()
