#!/usr/bin/env python3
# -*- coding: utf-8 -*-

with open(r"c:\p2p\SlskDown\MainForm.cs", 'r', encoding='utf-8') as f:
    lines = f.readlines()

balance = 0
method_stack = []

for i, line in enumerate(lines, 1):
    stripped = line.strip()
    
    # Detectar inicio de método
    if ('private ' in line or 'public ' in line or 'protected ' in line) and '(' in line and '{' not in line:
        # Puede ser una declaración de método en múltiples líneas
        method_name = stripped.split('(')[0].strip()
        method_stack.append((i, method_name, balance))
    
    # Contar llaves
    open_braces = line.count('{')
    close_braces = line.count('}')
    balance += open_braces - close_braces
    
    # Si el balance es negativo, hay un problema
    if balance < 0:
        print(f"❌ PROBLEMA en línea {i}: Balance negativo ({balance})")
        print(f"   Línea: {line.rstrip()}")
        if method_stack:
            print(f"   Último método: {method_stack[-1][1]} (línea {method_stack[-1][0]})")
        break
    
    # Mostrar métodos que se cierran
    if method_stack and balance == method_stack[-1][2]:
        method_info = method_stack.pop()
        if i > 21600 and i < 21700:
            print(f"✓ Método cerrado: {method_info[1]} (líneas {method_info[0]}-{i})")

print(f"\nBalance final: {balance}")
if balance > 0:
    print(f"❌ Faltan {balance} llaves de cierre")
    if method_stack:
        print(f"\nMétodos sin cerrar:")
        for line_num, name, bal in method_stack[-5:]:
            print(f"  • {name} (línea {line_num}, balance: {bal})")
elif balance < 0:
    print(f"❌ Sobran {-balance} llaves de cierre")
else:
    print("✅ Todas las llaves están balanceadas")
