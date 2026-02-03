with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Verificar las últimas 20 líneas
print("Últimas 20 líneas del archivo:")
for i, line in enumerate(lines[-20:], len(lines)-19):
    print(f"{i:5d}: {line.rstrip()}")

# Contar niveles de anidamiento al final
print("\nAnálisis de estructura al final:")
brace_level = 0
for i in range(len(lines)-1, max(len(lines)-50, 0), -1):
    line = lines[i]
    brace_level += line.count('}') - line.count('{')
    if '}' in line or '{' in line:
        print(f"Línea {i+1}: nivel={brace_level}, contenido={line.rstrip()[:80]}")
