# Script para eliminar código duplicado en MainForm.cs
import sys

# Leer el archivo
with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

print(f"Total de líneas originales: {len(lines)}")
print(f"Eliminando líneas 14142-16216 (índices 14141-16215)")

# Eliminar líneas 14142-16216 (índices 14141-16215 en base 0)
# Las líneas a mantener son: 0-14141 y 16216-fin
new_lines = lines[0:14141] + lines[16216:]

print(f"Total de líneas después: {len(new_lines)}")
print(f"Líneas eliminadas: {len(lines) - len(new_lines)}")

# Escribir el archivo
with open(r'c:\p2p\SlskDown\MainForm.cs', 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print("✅ Archivo modificado exitosamente")
