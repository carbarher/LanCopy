#!/usr/bin/env python3
# -*- coding: utf-8 -*-

with open('MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

print(f"Total lines: {len(lines)}")
print("\nBuscando cierres de clase (    }} seguido de }}):")

for i in range(1, len(lines)):
    if lines[i].strip() == '}' and lines[i-1].strip() == '}':
        # Verificar que la línea anterior tiene indentación de clase
        if lines[i-1].startswith('    }') and not lines[i-1].startswith('        }'):
            print(f"\nPosible cierre de clase en línea {i+1}:")
            print(f"  {i}: {lines[i-1].rstrip()}")
            print(f"  {i+1}: {lines[i].rstrip()}")
            
            # Mostrar contexto
            if i+2 < len(lines):
                print(f"  {i+2}: {lines[i+1].rstrip()}")
            if i+3 < len(lines):
                print(f"  {i+3}: {lines[i+2].rstrip()}")
