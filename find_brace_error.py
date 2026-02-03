#!/usr/bin/env python3
"""Encuentra el desbalance de llaves en MainForm.cs"""

def find_brace_mismatch(filename):
    with open(filename, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    stack = []
    for i, line in enumerate(lines, 1):
        # Contar llaves en la línea
        open_count = line.count('{')
        close_count = line.count('}')
        
        for _ in range(open_count):
            stack.append(i)
        
        for _ in range(close_count):
            if stack:
                stack.pop()
            else:
                print(f"ERROR: Llave de cierre extra en línea {i}")
                print(f"Línea: {line.rstrip()}")
                return
        
        # Mostrar progreso cada 1000 líneas
        if i % 1000 == 0:
            print(f"Procesadas {i} líneas, balance actual: {len(stack)}")
    
    if stack:
        print(f"\nERROR: Faltan {len(stack)} llaves de cierre")
        print(f"Llaves abiertas en líneas: {stack[:10]}...")  # Mostrar primeras 10
    else:
        print("\n✅ Todas las llaves están balanceadas")

if __name__ == "__main__":
    find_brace_mismatch("SlskDown/MainForm.cs")
