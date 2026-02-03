with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

brace_count = 0
for i, line in enumerate(lines, 1):
    # Contar llaves de apertura y cierre en cada línea
    open_braces = line.count('{')
    close_braces = line.count('}')
    
    brace_count += open_braces - close_braces
    
    # Mostrar líneas donde el balance cambia significativamente
    if brace_count < 0:
        print(f"ERROR en línea {i}: Balance negativo ({brace_count})")
        print(f"  {line.rstrip()}")
        break
    
    # Mostrar las últimas líneas donde el balance es 1 o 2 (nivel de clase/namespace)
    if i > 19100 and brace_count <= 2:
        print(f"Línea {i}: Balance = {brace_count}")
        print(f"  {line.rstrip()}")

print(f"\nBalance final: {brace_count}")
print("Si el balance final es 1, falta una llave de cierre '}' en algún lugar")
