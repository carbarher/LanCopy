#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import sys

def analyze_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        total_lines = len(lines)
        print(f"Total de líneas en el archivo: {total_lines}")
        
        # Buscar líneas después de 38367
        if total_lines > 38367:
            print(f"\n⚠️ Hay {total_lines - 38367} líneas adicionales después de la línea 38367")
            print("\nContenido después de línea 38367:")
            for i in range(38367, min(38367 + 100, total_lines)):
                line = lines[i].rstrip()
                print(f"{i+1}: {line}")
        
        # Verificar balance de llaves
        open_braces = 0
        close_braces = 0
        brace_positions = []
        
        for i, line in enumerate(lines, 1):
            for char in line:
                if char == '{':
                    open_braces += 1
                    brace_positions.append((i, '{', open_braces - close_braces))
                elif char == '}':
                    close_braces += 1
                    brace_positions.append((i, '}', open_braces - close_braces))
        
        print(f"\nBalance de llaves:")
        print(f"  Llaves de apertura: {open_braces}")
        print(f"  Llaves de cierre: {close_braces}")
        print(f"  Balance: {open_braces - close_braces}")
        
        if open_braces != close_braces:
            print(f"\n⚠️ ERROR: Las llaves no están balanceadas!")
            print(f"  Diferencia: {abs(open_braces - close_braces)} llaves")
            
            # Mostrar últimas posiciones de llaves
            print("\nÚltimas 20 posiciones de llaves:")
            for pos in brace_positions[-20:]:
                print(f"  Línea {pos[0]}: '{pos[1]}' (balance: {pos[2]})")
        
        # Buscar la línea 40619 si existe
        if total_lines >= 40619:
            print(f"\nContenido alrededor de línea 40619:")
            for i in range(max(0, 40618-5), min(40619+5, total_lines)):
                line = lines[i].rstrip()
                print(f"{i+1}: {line}")
        
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    analyze_file(r"c:\p2p\SlskDown\MainForm.cs")
