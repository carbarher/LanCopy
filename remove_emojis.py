#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Script para eliminar todos los emojis de archivos .cs en SlskDown"""

import os
import re
from pathlib import Path

def remove_emojis(text):
    """Elimina todos los emojis del texto usando regex Unicode"""
    # Patrón que cubre la mayoría de emojis
    emoji_pattern = re.compile(
        "["
        "\U0001F600-\U0001F64F"  # emoticones
        "\U0001F300-\U0001F5FF"  # símbolos & pictogramas
        "\U0001F680-\U0001F6FF"  # transporte & símbolos de mapa
        "\U0001F1E0-\U0001F1FF"  # banderas (iOS)
        "\U00002600-\U000027BF"  # símbolos misceláneos
        "\U0001F900-\U0001F9FF"  # símbolos suplementarios
        "\U00002700-\U000027BF"  # dingbats
        "\U0001FA00-\U0001FA6F"  # símbolos extendidos-A
        "\U0001FA70-\U0001FAFF"  # símbolos extendidos-B
        "\U00002300-\U000023FF"  # símbolos técnicos misceláneos
        "\U0001F000-\U0001F02F"  # fichas de Mahjong
        "\U0001F0A0-\U0001F0FF"  # cartas de juego
        "\uFE0F"                 # selector de variación
        "\u200D"                 # zero width joiner
        "]+", 
        flags=re.UNICODE
    )
    return emoji_pattern.sub('', text)

def process_files(project_path):
    """Procesa todos los archivos .cs en el proyecto"""
    files_processed = 0
    total_chars_removed = 0
    
    print(f"Procesando archivos en: {project_path}\n")
    
    for cs_file in Path(project_path).rglob("*.cs"):
        try:
            # Leer archivo
            with open(cs_file, 'r', encoding='utf-8') as f:
                content = f.read()
            
            original_length = len(content)
            
            # Eliminar emojis
            cleaned_content = remove_emojis(content)
            
            chars_removed = original_length - len(cleaned_content)
            
            if chars_removed > 0:
                # Guardar archivo modificado
                with open(cs_file, 'w', encoding='utf-8') as f:
                    f.write(cleaned_content)
                
                print(f"✓ {cs_file.name} - {chars_removed} caracteres eliminados")
                files_processed += 1
                total_chars_removed += chars_removed
                
        except Exception as e:
            print(f"✗ Error procesando {cs_file.name}: {e}")
    
    print("\n" + "="*50)
    print("RESUMEN FINAL")
    print("="*50)
    print(f"Archivos modificados: {files_processed}")
    print(f"Total caracteres eliminados: {total_chars_removed}")
    print("="*50)

if __name__ == "__main__":
    project_path = r"c:\p2p\SlskDown"
    process_files(project_path)
