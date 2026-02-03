from pathlib import Path

text = Path('MainForm.cs').read_text(encoding='utf-8', errors='ignore')
lines = text.split('\n')

# Buscar atributos sin cerrar
open_brackets = 0
unclosed_attrs = []

for i, line in enumerate(lines, 1):
    # Contar [ y ] (simplificado, no maneja strings)
    for char in line:
        if char == '[':
            open_brackets += 1
            if open_brackets == 1:
                attr_start = i
        elif char == ']':
            open_brackets -= 1
    
    if open_brackets < 0:
        print(f"ERROR: Extra ] at line {i}")
        open_brackets = 0

if open_brackets > 0:
    print(f"ERROR: {open_brackets} unclosed [ attributes")
else:
    print("All [ ] attributes are balanced")

# Buscar comentarios multilínea sin cerrar
in_comment = False
for i, line in enumerate(lines, 1):
    if '/*' in line:
        in_comment = True
        comment_start = i
    if '*/' in line:
        in_comment = False
    
if in_comment:
    print(f"ERROR: Unclosed /* comment starting at line {comment_start}")
else:
    print("All /* */ comments are closed")
