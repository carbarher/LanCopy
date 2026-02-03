from pathlib import Path
import shutil

# Crear carpeta temporal
temp_dir = Path('temp_mainform_backup')
temp_dir.mkdir(exist_ok=True)

# Archivos a mantener
keep_files = {'MainForm.cs', 'MainForm.Designer.cs'}

# Mover todos los demás MainForm*.cs a la carpeta temporal
moved = []
for file in Path('.').glob('MainForm*.cs'):
    if file.name not in keep_files:
        dest = temp_dir / file.name
        shutil.move(str(file), str(dest))
        moved.append(file.name)
        print(f"Moved: {file.name}")

print(f"\nTotal moved: {len(moved)} files")
print("To restore, run: python restore_mainform_variants.py")
