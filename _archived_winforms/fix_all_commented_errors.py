#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Script para arreglar código comentado incorrectamente en archivos C#.
Busca patrones como:
    // ERROR: codigo(
        parametro1,
        parametro2
    );
Y los convierte en:
    // ERROR: codigo(
    //    parametro1,
    //    parametro2
    // );
"""

import os
import re

def fix_file(filepath):
    """Arregla un archivo C# con código comentado incorrectamente."""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
    except Exception as e:
        print(f"ERROR leyendo {filepath}: {e}")
        return False
    
    original_content = content
    lines = content.split('\n')
    modified = False
    i = 0
    
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        
        # Buscar líneas que contienen "// ERROR:"
        if '// ERROR:' in stripped:
            # Verificar si hay código después del comentario en la misma línea
            after_comment = stripped.split('// ERROR:', 1)[1].strip()
            
            # Si hay código después del comentario y termina con ( o =
            if after_comment and (after_comment.endswith('(') or after_comment.endswith('=')):
                # Las siguientes líneas probablemente son parte del código comentado
                j = i + 1
                base_indent = len(line) - len(line.lstrip())
                
                # Comentar las siguientes líneas hasta encontrar el final
                while j < len(lines):
                    next_line = lines[j]
                    next_stripped = next_line.strip()
                    
                    # Si la línea está vacía, saltarla
                    if not next_stripped:
                        j += 1
                        continue
                    
                    # Si ya está comentada, salir
                    if next_stripped.startswith('//'):
                        break
                    
                    # Obtener indentación
                    next_indent = len(next_line) - len(next_line.lstrip())
                    
                    # Si la indentación es menor que la base, salir
                    if next_indent < base_indent:
                        break
                    
                    # Comentar esta línea
                    lines[j] = next_line[:next_indent] + '// ' + next_line[next_indent:]
                    modified = True
                    
                    # Si termina con ; o }, probablemente es el final
                    if next_stripped.endswith(';') or next_stripped.endswith('}'):
                        break
                    
                    j += 1
        
        i += 1
    
    if modified:
        new_content = '\n'.join(lines)
        try:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"OK: {filepath}")
            return True
        except Exception as e:
            print(f"ERROR escribiendo {filepath}: {e}")
            return False
    else:
        print(f"SKIP: {filepath}")
        return False

def main():
    """Procesa todos los archivos afectados."""
    base_dir = r'c:\p2p\SlskDown'
    
    # Lista de archivos a procesar (basada en grep_search)
    files = [
        r'MainForm.cs',
        r'Core\ObjectPools\DownloadTaskPool.cs',
        r'Infrastructure\StructuredLogger.cs',
        r'Core\SearchManager.cs',
        r'Core\SearchCacheService.cs',
        r'UI\DarkFileDialog.cs',
        r'Core\Http3ClientService.cs',
        r'Core\MemoryManager.cs',
        r'Core\DownloadTaskPool.cs',
        r'Database\SlskDatabase.cs',
        r'Services\ValidationHelpers.cs',
        r'UI\CredentialPromptDialog.cs',
        r'UI\DarkFolderBrowserDialog.cs',
        r'UI\VirtualListView.cs',
        r'Core\ConnectionManager.cs',
        r'Core\RustCandidateRanker.cs',
        r'Services\Blake3BatchHasher.cs',
        r'Utils\SpanStringUtils.cs',
    ]
    
    fixed_count = 0
    error_count = 0
    
    for file in files:
        filepath = os.path.join(base_dir, file)
        if os.path.exists(filepath):
            if fix_file(filepath):
                fixed_count += 1
        else:
            print(f"NO EXISTE: {filepath}")
            error_count += 1
    
    print(f"\n=== RESUMEN ===")
    print(f"Archivos arreglados: {fixed_count}")
    print(f"Archivos con error: {error_count}")
    print(f"Total procesados: {len(files)}")

if __name__ == '__main__':
    main()
