from pathlib import Path

# Leer el archivo original
original = Path('MainForm.cs').read_bytes()

# Eliminar el archivo original
Path('MainForm.cs').unlink()

# Escribir exactamente el mismo contenido pero como archivo nuevo
# Esto elimina cualquier metadata corrupta del sistema de archivos
Path('MainForm.cs').write_bytes(original)

print("MainForm.cs recreated")
print(f"Size: {len(original)} bytes")
