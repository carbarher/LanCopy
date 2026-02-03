#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import re

# Leer archivo
with open('MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Procesar línea por línea
for i, line in enumerate(lines):
    # 1. Comentar todas las líneas con .Priority
    if '.Priority' in line and not line.strip().startswith('//'):
        indent = len(line) - len(line.lstrip())
        lines[i] = ' ' * indent + '// ' + line.lstrip()
        continue
    
    # 2. Arreglar TimeSpan?.TotalSeconds
    if '?.TotalSeconds' in line:
        # Buscar el patrón variable?.TotalSeconds
        match = re.search(r'(\w+)\?\.TotalSeconds', line)
        if match:
            var = match.group(1)
            lines[i] = line.replace(f'{var}?.TotalSeconds', 
                                   f'({var}?.HasValue ? {var}.Value.TotalSeconds : 0)')
    
    # 3. Cambiar Sequential a Manual
    if 'QueuePrioritizationStrategy.Sequential' in line:
        lines[i] = line.replace('QueuePrioritizationStrategy.Sequential',
                               'QueuePrioritizationStrategy.Manual')

# Guardar
with open('MainForm.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print("✅ Errores arreglados")
print("   - Líneas con .Priority comentadas")
print("   - TimeSpan?.TotalSeconds arreglado")
print("   - Sequential → Manual")
