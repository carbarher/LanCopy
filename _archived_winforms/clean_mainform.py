import codecs

# Leer el archivo
with open('MainForm.cs', 'rb') as f:
    content = f.read()

print(f"Original size: {len(content)} bytes")
print(f"Last 20 bytes (hex): {content[-20:].hex()}")
print(f"Last 20 bytes (repr): {repr(content[-20:])}")

# Detectar BOM
has_bom = content.startswith(codecs.BOM_UTF8)
print(f"Has UTF-8 BOM: {has_bom}")

# Remover BOM si existe
if has_bom:
    content = content[len(codecs.BOM_UTF8):]
    print("BOM removed")

# Decodificar a texto
text = content.decode('utf-8', errors='ignore')

# Eliminar espacios en blanco al final
text = text.rstrip()

# Asegurar que termine exactamente con }
if not text.endswith('}'):
    print(f"ERROR: File doesn't end with }}, ends with: {repr(text[-10:])}")
else:
    print("✅ File ends with }")

# Escribir sin BOM y sin trailing whitespace
with open('MainForm.cs', 'wb') as f:
    f.write(text.encode('utf-8'))

print(f"\nNew size: {len(text.encode('utf-8'))} bytes")
print("File cleaned and saved")
