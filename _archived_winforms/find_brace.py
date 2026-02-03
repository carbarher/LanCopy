with open('MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

balance = 0
last_negative = -1

for i, line in enumerate(lines, 1):
    for char in line:
        if char == '{':
            balance += 1
        elif char == '}':
            balance -= 1
        
        if balance < 0 and last_negative == -1:
            last_negative = i
            print(f"Primera vez que balance es negativo: línea {i}")
            print(f"Contenido: {line.strip()}")

print(f"\nBalance final: {balance}")
if balance < 0:
    print(f"Hay {abs(balance)} llaves de cierre de más")
elif balance > 0:
    print(f"Faltan {balance} llaves de cierre")
