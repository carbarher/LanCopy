#!/usr/bin/env python3
"""Script para crear el directorio publish_hotfix12 con los archivos compilados"""

import os
import shutil
import sys
from pathlib import Path

def main():
    print("=" * 60)
    print("Creando build publish_hotfix12")
    print("=" * 60)
    
    # Rutas
    base_dir = Path(r"c:\p2p\SlskDown")
    source_dir = base_dir / "bin" / "Release" / "net8.0-windows"
    dest_dir = base_dir / "bin" / "publish_hotfix13"
    
    print(f"\nDirectorio base: {base_dir}")
    print(f"Origen: {source_dir}")
    print(f"Destino: {dest_dir}")
    
    # Verificar que el origen existe
    if not source_dir.exists():
        print(f"\n[ERROR] El directorio origen no existe: {source_dir}")
        sys.exit(1)
    
    print(f"\n[OK] Directorio origen existe")
    
    # Contar archivos en origen
    source_files = list(source_dir.rglob("*"))
    source_file_count = len([f for f in source_files if f.is_file()])
    print(f"[INFO] Archivos en origen: {source_file_count}")
    
    # Crear directorio destino si no existe
    try:
        dest_dir.mkdir(parents=True, exist_ok=True)
        print(f"\n[OK] Directorio destino creado/verificado")
    except Exception as e:
        print(f"\n[ERROR] No se pudo crear directorio destino: {e}")
        sys.exit(1)
    
    # Copiar archivos
    print(f"\n[INFO] Copiando archivos...")
    copied_count = 0
    error_count = 0
    
    try:
        for item in source_dir.iterdir():
            try:
                dest_item = dest_dir / item.name
                if item.is_file():
                    shutil.copy2(item, dest_item)
                    copied_count += 1
                    if copied_count % 10 == 0:
                        print(f"  Copiados {copied_count} archivos...")
                elif item.is_dir():
                    if dest_item.exists():
                        shutil.rmtree(dest_item)
                    shutil.copytree(item, dest_item)
                    copied_count += 1
                    print(f"  Copiado directorio: {item.name}")
            except Exception as e:
                print(f"  [ERROR] Error copiando {item.name}: {e}")
                error_count += 1
        
        print(f"\n[OK] Copia completada")
        print(f"  Archivos/directorios copiados: {copied_count}")
        if error_count > 0:
            print(f"  Errores: {error_count}")
        
    except Exception as e:
        print(f"\n[ERROR] Error durante la copia: {e}")
        sys.exit(1)
    
    # Verificar que SlskDown.exe existe en destino
    exe_path = dest_dir / "SlskDown.exe"
    if exe_path.exists():
        exe_size = exe_path.stat().st_size
        print(f"\n[OK] SlskDown.exe encontrado en destino")
        print(f"  Tamaño: {exe_size:,} bytes")
        print(f"\n{'=' * 60}")
        print(f"BUILD CREADO EXITOSAMENTE")
        print(f"Ubicación: {dest_dir}")
        print(f"{'=' * 60}")
    else:
        print(f"\n[ERROR] SlskDown.exe NO encontrado en destino")
        sys.exit(1)

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n[INFO] Operación cancelada por el usuario")
        sys.exit(1)
    except Exception as e:
        print(f"\n[ERROR FATAL] {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
