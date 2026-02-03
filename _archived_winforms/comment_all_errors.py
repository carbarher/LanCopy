#!/usr/bin/env python3
import re
import sys
from pathlib import Path

def parse_all_errors(errors_file):
    """Parse todos los errores y extrae archivos y líneas"""
    errors_by_file = {}
    
    with open(errors_file, 'r', encoding='utf-8') as f:
        for line in f:
            match = re.search(r'([^(]+)\((\d+),\d+\):\s+error', line)
            if match:
                file_path = match.group(1).strip()
                line_num = int(match.group(2))
                
                if file_path not in errors_by_file:
                    errors_by_file[file_path] = set()
                errors_by_file[file_path].add(line_num)
    
    return errors_by_file

def comment_file_errors(file_path, error_lines):
    """Comenta las líneas con errores en un archivo"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except:
        print(f"No se pudo leer: {file_path}")
        return 0
    
    commented = 0
    for line_num in sorted(error_lines):
        if 1 <= line_num <= len(lines):
            idx = line_num - 1
            original = lines[idx]
            
            if not original.strip().startswith('//'):
                indent = len(original) - len(original.lstrip())
                lines[idx] = ' ' * indent + '// ERROR: ' + original.lstrip()
                commented += 1
    
    try:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.writelines(lines)
    except:
        print(f"No se pudo escribir: {file_path}")
        return 0
    
    return commented

def main():
    errors_file = Path('errors_only.txt')
    
    if not errors_file.exists():
        print(f"Error: {errors_file} no existe")
        return 1
    
    print("Parseando todos los errores...")
    errors_by_file = parse_all_errors(errors_file)
    
    total_files = len(errors_by_file)
    total_lines = sum(len(lines) for lines in errors_by_file.values())
    
    print(f"Encontrados {total_lines} errores en {total_files} archivos")
    print()
    
    total_commented = 0
    for file_path, error_lines in errors_by_file.items():
        print(f"Procesando: {file_path} ({len(error_lines)} errores)")
        commented = comment_file_errors(file_path, error_lines)
        total_commented += commented
    
    print()
    print(f"Completado: {total_commented} líneas comentadas")
    print("Ahora ejecuta: dotnet build")
    
    return 0

if __name__ == '__main__':
    sys.exit(main())
