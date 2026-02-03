#!/usr/bin/env python3
# Script para corregir indentación del bloque try en StartAutomaticSearch

with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Líneas que necesitan corrección (índice base 0)
# Desde línea 19690 (índice 19689) hasta línea 20392 (índice 20391)
# Todo lo que tenga 16 espacios o menos debe tener 4 espacios más

fixed = 0
for i in range(19689, 20392):
    line = lines[i]
    
    # Saltar líneas vacías
    if line.strip() == '':
        continue
    
    # Contar espacios al inicio
    spaces = len(line) - len(line.lstrip(' '))
    
    # Si tiene 16 espacios o menos y no está vacía, agregar 4 espacios
    if spaces <= 16 and line.strip():
        lines[i] = '    ' + line
        fixed += 1

# Guardar archivo
with open(r'c:\p2p\SlskDown\MainForm.cs', 'w', encoding='utf-8', newline='') as f:
    f.writelines(lines)

print(f'✅ Corregidas {fixed} líneas')
