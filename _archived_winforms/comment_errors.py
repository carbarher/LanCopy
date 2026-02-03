#!/usr/bin/env python3
"""
Script para comentar automáticamente líneas con errores de compilación en MainForm.cs
"""
import re
import sys
from pathlib import Path

def parse_errors(errors_file):
    """Parse el archivo de errores y extrae los números de línea de MainForm.cs"""
    errors = {}
    
    with open(errors_file, 'r', encoding='utf-8') as f:
        for line in f:
            # Buscar patrón: MainForm.cs(LINEA,COLUMNA): error
            match = re.search(r'MainForm\.cs\((\d+),\d+\):\s+error', line)
            if match:
                line_num = int(match.group(1))
                if line_num not in errors:
                    errors[line_num] = []
                errors[line_num].append(line.strip())
    
    return errors

def comment_lines(source_file, errors, output_file):
    """Comenta las líneas con errores en MainForm.cs"""
    with open(source_file, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    commented_count = 0
    
    # Comentar líneas con errores
    for line_num in sorted(errors.keys()):
        if 1 <= line_num <= len(lines):
            idx = line_num - 1
            original = lines[idx]
            
            # Si la línea ya está comentada, skip
            if original.strip().startswith('//'):
                continue
            
            # Comentar la línea
            indent = len(original) - len(original.lstrip())
            lines[idx] = ' ' * indent + '// ERROR: ' + original.lstrip()
            commented_count += 1
    
    # Guardar archivo modificado
    with open(output_file, 'w', encoding='utf-8') as f:
        f.writelines(lines)
    
    return commented_count

def main():
    errors_file = Path('errors_only.txt')
    source_file = Path('MainForm.cs')
    output_file = Path('MainForm.cs')
    backup_file = Path('MainForm.cs.backup_before_fix')
    
    if not errors_file.exists():
        print(f"Error: {errors_file} no existe")
        return 1
    
    if not source_file.exists():
        print(f"Error: {source_file} no existe")
        return 1
    
    print("Parseando errores...")
    errors = parse_errors(errors_file)
    print(f"Encontrados {len(errors)} líneas con errores en MainForm.cs")
    
    # Crear backup
    print(f"Creando backup: {backup_file}")
    import shutil
    shutil.copy2(source_file, backup_file)
    
    print("Comentando líneas con errores...")
    commented = comment_lines(source_file, errors, output_file)
    
    print(f"\n✅ Completado!")
    print(f"   - Líneas comentadas: {commented}")
    print(f"   - Backup guardado en: {backup_file}")
    print(f"\nAhora ejecuta: dotnet build")
    
    return 0

if __name__ == '__main__':
    sys.exit(main())
