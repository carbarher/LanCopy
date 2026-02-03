#!/usr/bin/env python3
import re

with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Desde línea 19772 (índice 19771) hasta línea 20392 (índice 20391)
# Todo debe tener al menos 20 espacios (dentro de try anidado)
fixed = 0
for i in range(19771, 20392):
    line = lines[i]
    if not line.strip():
        continue
    
    # Contar espacios
    match = re.match(r'^( *)', line)
    if match:
        spaces = len(match.group(1))
        # Si tiene menos de 20 espacios, agregar los necesarios
        if spaces < 20:
            needed = 20 - spaces
            lines[i] = ' ' * needed + line
            fixed += 1

with open(r'c:\p2p\SlskDown\MainForm.cs', 'w', encoding='utf-8', newline='') as f:
    f.writelines(lines)

print(f'Fixed {fixed} lines')
