#!/usr/bin/env python3
# -*- coding: utf-8 -*-

# Script para agregar los cierres de clase y namespace al final de MainForm.cs

with open('MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

print(f"Total lines: {len(lines)}")
print(f"Last 5 lines:")
for i in range(max(0, len(lines)-5), len(lines)):
    print(f"  {i+1}: {lines[i].rstrip()}")

# Verificar si el archivo termina correctamente
last_line = lines[-1].strip() if lines else ""
needs_class_close = True
needs_namespace_close = True

# Buscar hacia atrás para ver si ya hay cierres
for i in range(len(lines)-1, max(0, len(lines)-20), -1):
    line = lines[i].strip()
    if line == "}":
        # Verificar el contexto
        if i > 0:
            prev_line = lines[i-1].strip()
            if "class" in prev_line or needs_class_close:
                needs_class_close = False
            elif "namespace" in prev_line or not needs_class_close:
                needs_namespace_close = False

# Agregar cierres si es necesario
if needs_class_close or needs_namespace_close:
    print(f"\nAggregando cierres:")
    if needs_class_close:
        print("  - Cierre de clase MainForm")
    if needs_namespace_close:
        print("  - Cierre de namespace SlskDown")
    
    with open('MainForm.cs', 'a', encoding='utf-8') as f:
        if needs_class_close:
            f.write("    }\n")  # Cierre de clase
        if needs_namespace_close:
            f.write("}\n")  # Cierre de namespace
    
    print("\n✅ Cierres agregados correctamente")
else:
    print("\n✅ El archivo ya tiene los cierres correctos")
