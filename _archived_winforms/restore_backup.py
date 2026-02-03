from pathlib import Path
import shutil

source = Path('backups/backup_vpn_20251120/MainForm.cs')
dest = Path('MainForm.cs')

print(f"Source exists: {source.exists()}")
print(f"Source size: {source.stat().st_size if source.exists() else 0}")

if source.exists():
    shutil.copy2(source, dest)
    print(f"Copied to MainForm.cs")
    print(f"Dest size: {dest.stat().st_size}")
    lines = len(dest.read_text(encoding='utf-8', errors='ignore').split('\n'))
    print(f"Lines: {lines}")
else:
    print("Source not found!")
