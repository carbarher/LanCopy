from pathlib import Path

backups = [
    "MainForm.cs_backup_completo",
    "MainForm.cs.bak_final",
    "MainForm.cs.backup_20251118_165624",
    "MainForm.cs.backup_20251118_153012"
]

for backup in backups:
    try:
        size = Path(backup).stat().st_size
        lines = len(Path(backup).read_text(encoding='utf-8', errors='ignore').split('\n'))
        print(f"{backup}: {size:,} bytes, {lines:,} lines")
    except:
        print(f"{backup}: NOT FOUND")
