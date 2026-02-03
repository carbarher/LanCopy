#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Script para eliminar todos los emojis de archivos .cs en el proyecto SlskDown
"""

import os
import re
import sys
from pathlib import Path

# Patrón regex para emojis (más completo)
EMOJI_PATTERN = re.compile(
    "["
    "\U0001F1E0-\U0001F1FF"  # Banderas (iOS)
    "\U0001F300-\U0001F5FF"  # Símbolos y pictogramas
    "\U0001F600-\U0001F64F"  # Emoticones
    "\U0001F680-\U0001F6FF"  # Transporte y símbolos de mapa
    "\U0001F700-\U0001F77F"  # Símbolos alquímicos
    "\U0001F780-\U0001F7FF"  # Símbolos geométricos extendidos
    "\U0001F800-\U0001F8FF"  # Flechas suplementarias-C
    "\U0001F900-\U0001F9FF"  # Símbolos y pictogramas suplementarios
    "\U0001FA00-\U0001FA6F"  # Símbolos de ajedrez
    "\U0001FA70-\U0001FAFF"  # Símbolos y pictogramas extendidos-A
    "\U00002600-\U000027BF"  # Símbolos misceláneos
    "\U0001F000-\U0001F02F"  # Fichas de Mahjong
    "\U0001F0A0-\U0001F0FF"  # Cartas de juego
    "\U00002300-\U000023FF"  # Símbolos técnicos misceláneos
    "\U00002B50"              # Estrella blanca mediana
    "\U00002B55"              # Círculo grande
    "\U0000231A-\U0000231B"  # Reloj
    "\U000023E9-\U000023F3"  # Símbolos de reproducción
    "\U000025AA-\U000025AB"  # Cuadrados
    "\U000025B6"              # Triángulo derecha
    "\U000025C0"              # Triángulo izquierda
    "\U000025FB-\U000025FE"  # Cuadrados blancos/negros
    "\U00002614-\U00002615"  # Paraguas/bebida caliente
    "\U00002648-\U00002653"  # Signos zodiacales
    "\U0000267F"              # Símbolo silla de ruedas
    "\U00002693"              # Ancla
    "\U000026A1"              # Alto voltaje
    "\U000026AA-\U000026AB"  # Círculos
    "\U000026BD-\U000026BE"  # Deportes
    "\U000026C4-\U000026C5"  # Clima
    "\U000026CE"              # Ofiuco
    "\U000026D4"              # Sin entrada
    "\U000026EA"              # Iglesia
    "\U000026F2-\U000026F3"  # Fuente
    "\U000026F5"              # Velero
    "\U000026FA"              # Tienda
    "\U000026FD"              # Bomba de gasolina
    "\U00002702"              # Tijeras
    "\U00002705"              # Marca de verificación blanca
    "\U00002708-\U00002709"  # Avión
    "\U0000270A-\U0000270B"  # Puños
    "\U0000270C-\U0000270D"  # Victoria/escritura
    "\U0000270F"              # Lápiz
    "\U00002712"              # Pluma negra
    "\U00002714"              # Marca de verificación pesada
    "\U00002716"              # X pesada
    "\U0000271D"              # Cruz latina
    "\U00002721"              # Estrella de David
    "\U00002728"              # Destellos
    "\U00002733-\U00002734"  # Asteriscos
    "\U00002744"              # Copo de nieve
    "\U00002747"              # Chispa
    "\U0000274C"              # X roja
    "\U0000274E"              # X negativa cuadrada
    "\U00002753-\U00002755"  # Signos de interrogación
    "\U00002757"              # Exclamación pesada
    "\U00002763-\U00002764"  # Corazones
    "\U00002795-\U00002797"  # Más/menos
    "\U000027A1"              # Flecha derecha negra
    "\U000027B0"              # Bucle rizado
    "\U000027BF"              # Doble bucle rizado
    "\U00002934-\U00002935"  # Flechas
    "\U00002B05-\U00002B07"  # Flechas
    "\U00002B1B-\U00002B1C"  # Cuadrados
    "\U00003030"              # Guión ondulado
    "\U0000303D"              # Marca de alternancia de parte
    "\U00003297"              # Ideograma japonés
    "\U00003299"              # Ideograma japonés
    "\U0000FE0F"              # Selector de variación-16
    "\U0000200D"              # Zero Width Joiner
    "]+", 
    flags=re.UNICODE
)

def remove_emojis_from_file(filepath):
    """Elimina emojis de un archivo y retorna estadísticas"""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            original_content = f.read()
        
        # Contar emojis antes de eliminar
        emojis_found = EMOJI_PATTERN.findall(original_content)
        emoji_count = len(emojis_found)
        
        if emoji_count == 0:
            return 0, 0
        
        # Eliminar emojis
        clean_content = EMOJI_PATTERN.sub('', original_content)
        
        # Calcular caracteres eliminados
        chars_removed = len(original_content) - len(clean_content)
        
        # Guardar archivo limpio
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(clean_content)
        
        return emoji_count, chars_removed
    
    except Exception as e:
        print(f"  ❌ Error procesando {filepath}: {e}", file=sys.stderr)
        return 0, 0

def main():
    project_path = Path(r"c:\p2p\SlskDown")
    
    if not project_path.exists():
        print(f"❌ Error: No se encontró el directorio {project_path}")
        sys.exit(1)
    
    print("=" * 60)
    print("ELIMINADOR DE EMOJIS - SlskDown")
    print("=" * 60)
    print(f"Directorio: {project_path}")
    print()
    
    total_files = 0
    total_emojis = 0
    total_chars = 0
    modified_files = []
    
    # Buscar todos los archivos .cs
    cs_files = list(project_path.rglob("*.cs"))
    print(f"Archivos .cs encontrados: {len(cs_files)}")
    print()
    
    # Procesar cada archivo
    for cs_file in cs_files:
        emoji_count, chars_removed = remove_emojis_from_file(cs_file)
        
        if emoji_count > 0:
            relative_path = cs_file.relative_to(project_path)
            print(f"✓ {relative_path}")
            print(f"  - Emojis eliminados: {emoji_count}")
            print(f"  - Caracteres eliminados: {chars_removed}")
            
            total_files += 1
            total_emojis += emoji_count
            total_chars += chars_removed
            modified_files.append(str(relative_path))
    
    # Resumen final
    print()
    print("=" * 60)
    print("RESUMEN")
    print("=" * 60)
    print(f"Archivos modificados: {total_files}")
    print(f"Total emojis eliminados: {total_emojis}")
    print(f"Total caracteres eliminados: {total_chars}")
    print()
    
    if modified_files:
        print("Archivos modificados:")
        for file in modified_files:
            print(f"  - {file}")
    else:
        print("✓ No se encontraron emojis en ningún archivo")
    
    print()
    print("=" * 60)
    print("✓ PROCESO COMPLETADO")
    print("=" * 60)

if __name__ == "__main__":
    main()
