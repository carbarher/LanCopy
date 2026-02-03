# Script para eliminar líneas duplicadas 15080-16219
with open('SlskDown/MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Mantener líneas 1-15078 y 16220 en adelante
new_lines = lines[0:15078] + lines[16219:]

with open('SlskDown/MainForm.cs', 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print(f"Eliminadas {len(lines) - len(new_lines)} líneas")
print(f"Archivo original: {len(lines)} líneas")
print(f"Archivo nuevo: {len(new_lines)} líneas")
