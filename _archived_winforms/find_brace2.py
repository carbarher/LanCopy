with open('MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

balance = 0
suspicious_lines = []

for i, line in enumerate(lines, 1):
    line_balance_before = balance
    for char in line:
        if char == '{':
            balance += 1
        elif char == '}':
            balance -= 1
    
    # Guardar líneas donde el balance disminuye más de lo esperado
    line_balance_after = balance
    if line_balance_after < line_balance_before - 1:
        suspicious_lines.append((i, line.strip(), line_balance_before, line_balance_after))

print("Líneas sospechosas (balance disminuye más de 1):")
for line_num, content, before, after in suspicious_lines[-10:]:  # Últimas 10
    print(f"Línea {line_num}: {content[:80]}")
    print(f"  Balance: {before} -> {after} (cambio: {after - before})")
    print()

print(f"\nBalance final: {balance}")

# Buscar líneas cerca del final con solo }
print("\nÚltimas 20 líneas con solo }:")
for i in range(len(lines) - 1, max(0, len(lines) - 100), -1):
    if lines[i].strip() == '}':
        print(f"Línea {i+1}: {lines[i].strip()}")
        if len([x for x in lines[max(0,i-20):i+1] if x.strip() == '}']) >= 20:
            break
