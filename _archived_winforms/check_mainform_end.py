from pathlib import Path

# Leer MainForm.cs
text = Path('MainForm.cs').read_text(encoding='utf-8', errors='ignore')
lines = text.split('\n')

print(f"Total lines: {len(lines)}")
print(f"Last 10 lines:")
for i in range(max(0, len(lines)-10), len(lines)):
    line = lines[i] if i < len(lines) else ""
    print(f"  {i+1}: {repr(line[:80])}")

# Verificar si hay strings sin cerrar en las últimas 100 líneas
last_100 = '\n'.join(lines[-100:])
quote_count = last_100.count('"')
print(f"\nQuote count in last 100 lines: {quote_count} ({'even' if quote_count % 2 == 0 else 'ODD - PROBLEM!'})")

# Buscar strings multilínea sin cerrar
in_string = False
for i in range(max(0, len(lines)-100), len(lines)):
    line = lines[i] if i < len(lines) else ""
    # Contar comillas (simplificado, no maneja escapes)
    for char in line:
        if char == '"':
            in_string = not in_string
    if in_string:
        print(f"WARNING: Line {i+1} might have unclosed string")
