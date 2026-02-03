#!/usr/bin/env python3
"""
Script para encontrar desbalances de llaves en MainForm.cs
"""

def analyze_braces(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    stack = []  # Stack para rastrear llaves abiertas
    errors = []
    
    for line_num, line in enumerate(lines, 1):
        # Ignorar comentarios y strings
        in_string = False
        in_comment = False
        i = 0
        
        while i < len(line):
            # Detectar strings
            if line[i] == '"' and (i == 0 or line[i-1] != '\\'):
                in_string = not in_string
            
            # Detectar comentarios de línea
            if not in_string and i < len(line) - 1:
                if line[i:i+2] == '//':
                    break  # Resto de la línea es comentario
            
            # Contar llaves solo fuera de strings y comentarios
            if not in_string:
                if line[i] == '{':
                    stack.append((line_num, line.strip()))
                elif line[i] == '}':
                    if not stack:
                        errors.append(f"Línea {line_num}: Llave de cierre sin apertura correspondiente")
                        errors.append(f"  Contenido: {line.strip()}")
                    else:
                        stack.pop()
            
            i += 1
    
    # Reportar resultados
    print("=" * 80)
    print("ANÁLISIS DE LLAVES EN MainForm.cs")
    print("=" * 80)
    print(f"\nTotal de líneas: {len(lines)}")
    print(f"Llaves abiertas sin cerrar: {len(stack)}")
    print(f"Errores de cierre sin apertura: {len(errors)}")
    
    if errors:
        print("\n❌ ERRORES ENCONTRADOS:")
        for error in errors:
            print(error)
    
    if stack:
        print("\n⚠️ LLAVES SIN CERRAR:")
        # Mostrar las últimas 10 llaves sin cerrar
        for line_num, content in stack[-10:]:
            print(f"  Línea {line_num}: {content[:80]}")
        
        if len(stack) > 10:
            print(f"  ... y {len(stack) - 10} más")
    
    if not errors and not stack:
        print("\n✅ No se encontraron problemas de balance de llaves")
    
    # Buscar el área problemática alrededor de la línea 24983
    print("\n" + "=" * 80)
    print("ANÁLISIS DEL ÁREA PROBLEMÁTICA (líneas 24970-24995)")
    print("=" * 80)
    
    brace_count = 0
    for line_num in range(24970, min(24995, len(lines))):
        line = lines[line_num]
        open_count = line.count('{')
        close_count = line.count('}')
        brace_count += (open_count - close_count)
        
        print(f"{line_num+1:5d} [{brace_count:+3d}]: {line.rstrip()}")

if __name__ == "__main__":
    analyze_braces(r"c:\p2p\SlskDown\MainForm.cs")
