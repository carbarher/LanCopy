from pathlib import Path

# Buscar archivos .cs grandes (>1 MB)
for cs_file in Path('.').rglob('*.cs'):
    try:
        size = cs_file.stat().st_size
        if size > 1_000_000:  # > 1 MB
            lines = len(cs_file.read_text(encoding='utf-8', errors='ignore').split('\n'))
            print(f"{cs_file}: {size:,} bytes, {lines:,} lines")
    except:
        pass
