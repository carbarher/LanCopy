from pathlib import Path

for cs_file in Path('.').rglob('*.cs'):
    try:
        lines = cs_file.read_text(encoding='utf-8', errors='ignore').split('\n')
        if len(lines) == 40712 or len(lines) == 40713:
            print(f"{cs_file}: {len(lines)} lines")
    except:
        pass
