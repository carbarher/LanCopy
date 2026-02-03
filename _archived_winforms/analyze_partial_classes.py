#!/usr/bin/env python3
import os
import glob

mainform_files = [
    r"c:\p2p\SlskDown\MainForm.cs",
    r"c:\p2p\SlskDown\MainForm.Designer.cs",
    r"c:\p2p\SlskDown\MainForm.Downloads.cs",
    r"c:\p2p\SlskDown\MainForm.Search.cs",
    r"c:\p2p\SlskDown\MainForm.UI.cs"
]

total_lines = 0
print("Archivos parciales de MainForm activos:\n")

for filepath in mainform_files:
    if os.path.exists(filepath):
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        line_count = len(lines)
        total_lines += line_count
        print(f"{os.path.basename(filepath)}: {line_count} lineas")
        
        # Verificar balance de llaves en cada archivo
        open_braces = sum(line.count('{') for line in lines)
        close_braces = sum(line.count('}') for line in lines)
        balance = open_braces - close_braces
        print(f"  Balance de llaves: {open_braces} aperturas - {close_braces} cierres = {balance}")
        
        # Verificar si tiene namespace y class
        has_namespace = any('namespace' in line for line in lines)
        has_class = any('class MainForm' in line for line in lines)
        print(f"  Namespace: {has_namespace}, Class MainForm: {has_class}")
        print()

print(f"Total combinado: {total_lines} lineas")
print(f"Linea del error reportado: 40619")
print(f"Diferencia: {40619 - total_lines} lineas")
