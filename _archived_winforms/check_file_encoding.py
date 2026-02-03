#!/usr/bin/env python3
import os

def check_file_issues(filepath):
    print(f"Analizando: {filepath}\n")
    
    # Leer como binario para detectar problemas
    with open(filepath, 'rb') as f:
        content = f.read()
    
    # Detectar BOM
    if content.startswith(b'\xef\xbb\xbf'):
        print("✓ Archivo tiene BOM UTF-8")
    elif content.startswith(b'\xff\xfe'):
        print("⚠️ Archivo tiene BOM UTF-16 LE")
    elif content.startswith(b'\xfe\xff'):
        print("⚠️ Archivo tiene BOM UTF-16 BE")
    else:
        print("✓ Sin BOM")
    
    # Contar líneas reales
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        print(f"Líneas totales: {len(lines)}")
        
        # Verificar últimas líneas
        print("\nÚltimas 10 líneas del archivo:")
        for i, line in enumerate(lines[-10:], len(lines)-9):
            line_repr = repr(line)
            print(f"{i}: {line_repr[:100]}")
        
        # Buscar caracteres extraños
        null_count = content.count(b'\x00')
        if null_count > 0:
            print(f"\n⚠️ Encontrados {null_count} caracteres NULL")
        
        # Verificar tamaño del archivo
        file_size = os.path.getsize(filepath)
        print(f"\nTamaño del archivo: {file_size:,} bytes")
        
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    check_file_issues(r"c:\p2p\SlskDown\MainForm.cs")
