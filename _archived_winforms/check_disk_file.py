import hashlib

# Verificar el archivo en disco
with open(r'c:\p2p\SlskDown\MainForm.cs', 'rb') as f:
    content = f.read()
    file_hash = hashlib.md5(content).hexdigest()
    
with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()
    
print(f"Total lineas en disco: {len(lines)}")
print(f"Hash MD5: {file_hash}")
print(f"\nLinea 17997: {lines[17996].strip()}")
print(f"Balance de llaves:")
content_str = ''.join(lines)
print(f"  Abrir: {content_str.count('{')}")
print(f"  Cerrar: {content_str.count('}')}")
