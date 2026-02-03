from pathlib import Path
import re

text = Path('MainForm.cs').read_text(encoding='utf-8', errors='ignore')
lines = text.split('\n')

# Buscar líneas que empiezan con [ (atributos reales)
in_attribute = False
attribute_start = 0

for i, line in enumerate(lines, 1):
    stripped = line.strip()
    
    # Detectar inicio de atributo
    if stripped.startswith('[') and not '@"' in line:
        in_attribute = True
        attribute_start = i
        print(f"Line {i}: Attribute starts: {stripped[:80]}")
        
        # Verificar si se cierra en la misma línea
        if ']' in stripped:
            in_attribute = False
            print(f"  -> Closed on same line")
    elif in_attribute:
        # Buscar cierre en líneas siguientes
        if ']' in line:
            in_attribute = False
            print(f"Line {i}: Attribute closes: {stripped[:80]}")
        else:
            print(f"Line {i}: Attribute continues: {stripped[:80]}")

if in_attribute:
    print(f"\nERROR: Unclosed attribute starting at line {attribute_start}")
else:
    print("\nAll attributes properly closed")
