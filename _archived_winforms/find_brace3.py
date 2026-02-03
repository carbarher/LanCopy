with open('MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

balance = 0
checkpoints = [1000, 5000, 10000, 15000, 20000, 25000, 30000, 35000, 40000, 40500, 40600, 40619]

print("Balance de llaves en diferentes puntos del archivo:\n")

for i, line in enumerate(lines, 1):
    for char in line:
        if char == '{':
            balance += 1
        elif char == '}':
            balance -= 1
    
    if i in checkpoints:
        print(f"Línea {i:5d}: balance = {balance:4d}")
        if balance < 0:
            print(f"  ⚠️ NEGATIVO en línea {i}")
            print(f"  Contenido: {line.strip()[:80]}")

print(f"\nBalance final: {balance}")

# Buscar el rango donde el balance se vuelve problemático
print("\n" + "="*60)
print("Buscando el rango problemático...")
print("="*60)

balance = 0
for i, line in enumerate(lines, 1):
    for char in line:
        if char == '{':
            balance += 1
        elif char == '}':
            balance -= 1
    
    # Mostrar cuando el balance esperado no coincide con la estructura
    if i >= 40600:
        print(f"Línea {i}: balance = {balance:4d} | {line.strip()[:60]}")
