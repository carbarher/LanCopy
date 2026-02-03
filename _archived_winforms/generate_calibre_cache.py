#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Script para generar caché de archivos de Calibre
Escanea la carpeta de Calibre y genera un archivo de texto con todos los nombres de archivos
"""

import os
import sys
from pathlib import Path
import json
from datetime import datetime

# Configuración
CALIBRE_PATH = r"D:\emule ya pasados a calibre"
OUTPUT_FILE = r"c:\p2p\SlskDown\calibre_files_cache.txt"
OUTPUT_JSON = r"c:\p2p\SlskDown\calibre_cache_info.json"

# Extensiones de documentos a incluir
DOCUMENT_EXTENSIONS = {'.pdf', '.epub', '.mobi', '.djvu', '.doc', '.docx', '.txt', '.rtf', '.odt', '.cbz', '.cbr'}

def generate_cache():
    """Genera el archivo de caché con todos los archivos de Calibre"""
    
    print("=" * 60)
    print("  Generador de Caché de Archivos de Calibre")
    print("=" * 60)
    print()
    
    # Verificar que existe la carpeta
    if not os.path.exists(CALIBRE_PATH):
        print(f"❌ ERROR: No se encuentra la carpeta: {CALIBRE_PATH}")
        return False
    
    print(f"📂 Escaneando: {CALIBRE_PATH}")
    print()
    
    files_found = []
    total_size = 0
    extensions_count = {}
    
    # Escanear recursivamente
    try:
        for root, dirs, files in os.walk(CALIBRE_PATH):
            for filename in files:
                # Obtener extensión
                ext = os.path.splitext(filename)[1].lower()
                
                # Solo incluir documentos
                if ext in DOCUMENT_EXTENSIONS:
                    files_found.append(filename)
                    
                    # Estadísticas
                    full_path = os.path.join(root, filename)
                    try:
                        size = os.path.getsize(full_path)
                        total_size += size
                    except:
                        pass
                    
                    extensions_count[ext] = extensions_count.get(ext, 0) + 1
            
            # Mostrar progreso cada 1000 archivos
            if len(files_found) % 1000 == 0 and len(files_found) > 0:
                print(f"   Procesados: {len(files_found):,} archivos...")
    
    except Exception as e:
        print(f"❌ ERROR durante el escaneo: {e}")
        return False
    
    # Guardar archivo de texto (un archivo por línea)
    try:
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            for filename in sorted(files_found):
                f.write(filename + '\n')
        
        print()
        print(f"✅ Archivo de caché guardado: {OUTPUT_FILE}")
        print(f"   {len(files_found):,} archivos")
        print(f"   {total_size / (1024**3):.2f} GB total")
    
    except Exception as e:
        print(f"❌ ERROR al guardar archivo: {e}")
        return False
    
    # Guardar información adicional en JSON
    info = {
        "generated": datetime.now().isoformat(),
        "source_path": CALIBRE_PATH,
        "total_files": len(files_found),
        "total_size_bytes": total_size,
        "total_size_gb": round(total_size / (1024**3), 2),
        "extensions": extensions_count
    }
    
    try:
        with open(OUTPUT_JSON, 'w', encoding='utf-8') as f:
            json.dump(info, f, indent=2, ensure_ascii=False)
        
        print(f"✅ Información guardada: {OUTPUT_JSON}")
    
    except Exception as e:
        print(f"⚠️ Advertencia: No se pudo guardar JSON: {e}")
    
    # Mostrar estadísticas
    print()
    print("📊 ESTADÍSTICAS:")
    print(f"   Total archivos: {len(files_found):,}")
    print(f"   Tamaño total: {total_size / (1024**3):.2f} GB")
    print()
    print("   Extensiones encontradas:")
    for ext, count in sorted(extensions_count.items(), key=lambda x: x[1], reverse=True):
        print(f"      {ext}: {count:,} archivos")
    
    print()
    print("=" * 60)
    print("✅ CACHÉ GENERADO EXITOSAMENTE")
    print("=" * 60)
    
    return True

if __name__ == "__main__":
    success = generate_cache()
    
    if not success:
        sys.exit(1)
    
    print()
    input("Presiona Enter para salir...")
