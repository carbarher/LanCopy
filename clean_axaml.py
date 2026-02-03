import sys

# Leer el archivo
with open(r'c:\p2p\SlskDownAvalonia\MainWindow.axaml', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Encontrar la primera línea que contiene </Window>
output_lines = []
for i, line in enumerate(lines):
    output_lines.append(line)
    if '</Window>' in line:
        print(f"Encontrado </Window> en línea {i+1}")
        break

# Escribir el archivo limpio
with open(r'c:\p2p\SlskDownAvalonia\MainWindow.axaml', 'w', encoding='utf-8') as f:
    f.writelines(output_lines)

print(f"Archivo limpiado: {len(output_lines)} líneas escritas")
