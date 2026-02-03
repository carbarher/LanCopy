#!/usr/bin/env python3
import sys

with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Reemplazar todo el bloque problemático con indentación correcta
# Buscar desde línea 19837 hasta el final del método

# Leer líneas
lines = content.split('\n')

# Desde línea 19837 (índice 19836) hasta línea 20392 (índice 20391)
# Agregar 4 espacios a cada línea no vacía
for i in range(19836, min(20392, len(lines))):
    if lines[i].strip():  # Solo líneas no vacías
        lines[i] = '    ' + lines[i]

# Guardar
with open(r'c:\p2p\SlskDown\MainForm.cs', 'w', encoding='utf-8', newline='\n') as f:
    f.write('\n'.join(lines))

print('Fixed indentation')
sys.exit(0)
