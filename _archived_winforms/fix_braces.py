#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Script para corregir el balance de llaves en MainForm.cs"""

import sys
import hashlib

def get_file_hash(filepath):
    """Calcula el hash SHA256 del archivo"""
    sha256_hash = hashlib.sha256()
    with open(filepath, "rb") as f:
        for byte_block in iter(lambda: f.read(4096), b""):
            sha256_hash.update(byte_block)
    return sha256_hash.hexdigest()

def main():
    filepath = "MainForm.cs"
    
    print("Leyendo archivo...")
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    print(f"Total líneas: {len(lines)}")
    print(f"Hash actual: {get_file_hash(filepath)}")
    print()
    
    # Mostrar las líneas problemáticas (índice 20279-20291, líneas 20280-20292)
    print("Líneas actuales (20280-20292):")
    for i in range(20279, min(20292, len(lines))):
        print(f"{i+1}: {lines[i].rstrip()}")
    print()
    
    # Verificar si hay dos llaves de cierre seguidas antes de finally
    # Línea 20284 (índice 20283) y línea 20285 (índice 20284)
    if len(lines) > 20285:
        line_20284 = lines[20283].strip()
        line_20285 = lines[20284].strip()
        line_20286 = lines[20285].strip() if len(lines) > 20286 else ""
        
        print(f"Análisis:")
        print(f"  Línea 20284: '{line_20284}'")
        print(f"  Línea 20285: '{line_20285}'")
        print(f"  Línea 20286: '{line_20286}'")
        print()
        
        # Si línea 20285 es "}" y línea 20286 es "finally", eliminar línea 20285
        if line_20285 == "}" and line_20286 == "finally":
            print("PROBLEMA DETECTADO: Llave extra en línea 20285 antes de finally")
            print("Eliminando línea 20285...")
            
            # Crear nueva lista sin la línea 20285 (índice 20284)
            new_lines = lines[:20284] + lines[20285:]
            
            # Guardar archivo
            with open(filepath, 'w', encoding='utf-8', newline='') as f:
                f.writelines(new_lines)
            
            print(f"¡Corrección aplicada! Nuevas líneas totales: {len(new_lines)}")
            print(f"Nuevo hash: {get_file_hash(filepath)}")
            return 0
        
        # Si línea 20284 es "}" y línea 20285 es "finally", está correcto
        elif line_20284 == "}" and line_20285 == "finally":
            print("Estructura CORRECTA detectada - no se necesita corrección")
            return 0
        
        else:
            print("Estructura no reconocida - se necesita análisis manual")
            return 1
    else:
        print("ERROR: El archivo no tiene suficientes líneas")
        return 1

if __name__ == "__main__":
    sys.exit(main())
