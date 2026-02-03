from pathlib import Path
import re

text = Path('MainForm.cs').read_text(encoding='utf-8', errors='ignore')

# Remover todos los strings (verbatim y normales) para no contar [ ] dentro de ellos
# Primero verbatim strings @"..."
text_no_strings = re.sub(r'@"(?:[^"]|"")*"', '', text)
# Luego strings normales "..."
text_no_strings = re.sub(r'"(?:[^"\\]|\\.)*"', '', text_no_strings)

# Ahora contar [ y ]
open_count = text_no_strings.count('[')
close_count = text_no_strings.count(']')

print(f"[ count (outside strings): {open_count}")
print(f"] count (outside strings): {close_count}")
print(f"Difference: {open_count - close_count}")

if open_count != close_count:
    print(f"\nERROR: Unbalanced attributes!")
    if open_count > close_count:
        print(f"Missing {open_count - close_count} closing ]")
    else:
        print(f"Extra {close_count - open_count} closing ]")
else:
    print("\n✅ All attributes are balanced")
