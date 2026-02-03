from pathlib import Path
import re

text = Path('MainForm.cs').read_text(encoding='utf-8', errors='ignore')
lines = text.split('\n')

# Buscar directivas de preprocesador
if_count = 0
endif_count = 0
if_stack = []

for i, line in enumerate(lines, 1):
    stripped = line.strip()
    if stripped.startswith('#if'):
        if_count += 1
        if_stack.append((i, stripped))
    elif stripped.startswith('#endif'):
        endif_count += 1
        if if_stack:
            if_stack.pop()

print(f"#if directives: {if_count}")
print(f"#endif directives: {endif_count}")
print(f"Difference: {if_count - endif_count}")

if if_stack:
    print(f"\nUnclosed #if directives:")
    for line_num, directive in if_stack:
        print(f"  Line {line_num}: {directive}")
else:
    print("\nAll #if directives are properly closed")

# Buscar strings con """ (verbatim strings en C#)
verbatim_strings = re.findall(r'@"[^"]*"', text)
print(f"\nVerbatim strings found: {len(verbatim_strings)}")

# Buscar raw strings con """ (C# 11+)
raw_strings = re.findall(r'"""', text)
print(f'Raw string delimiters ("""): {len(raw_strings)} (should be even)')
if len(raw_strings) % 2 != 0:
    print("WARNING: Odd number of raw string delimiters!")
