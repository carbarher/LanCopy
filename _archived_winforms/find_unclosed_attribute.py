from pathlib import Path
import re

text = Path('MainForm.cs').read_text(encoding='utf-8', errors='ignore')
lines = text.split('\n')

# Procesar línea por línea, removiendo strings
balance = 0
for i, line in enumerate(lines, 1):
    # Remover strings de esta línea
    line_no_strings = re.sub(r'@"(?:[^"]|"")*"', '', line)
    line_no_strings = re.sub(r'"(?:[^"\\]|\\.)*"', '', line_no_strings)
    
    # Contar [ y ]
    opens = line_no_strings.count('[')
    closes = line_no_strings.count(']')
    balance += opens - closes
    
    # Reportar si el balance se vuelve negativo o si hay apertura sin cierre
    if balance < 0:
        print(f"Line {i}: Extra ] (balance={balance})")
        print(f"  {line[:100]}")
        balance = 0  # Reset para continuar
    elif opens > 0 and balance > 0:
        # Posible inicio de atributo sin cerrar
        if i > 1 and balance == opens:  # Solo si es nuevo
            print(f"Line {i}: Opened [ (balance={balance})")
            print(f"  {line[:100]}")

print(f"\nFinal balance: {balance}")
if balance > 0:
    print(f"Missing {balance} closing ]")
