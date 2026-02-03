#!/usr/bin/env python3
import sys

with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Líneas que necesitan corrección (índice base 0):
# - Líneas 19826-20392 (índices 19825-20391) necesitan 4 espacios adicionales
# Estas líneas están dentro del try anidado que comienza en línea 19688

fixed = 0
for i in range(19825, 20392):
    line = lines[i]
    
    # Saltar líneas vacías
    if not line.strip():
        continue
    
    # Agregar 4 espacios al inicio de cada línea no vacía
    lines[i] = '    ' + line
    fixed += 1

# Guardar
with open(r'c:\p2p\SlskDown\MainForm.cs', 'w', encoding='utf-8', newline='') as f:
    f.writelines(lines)

print(f'Fixed {fixed} lines')
sys.exit(0)
