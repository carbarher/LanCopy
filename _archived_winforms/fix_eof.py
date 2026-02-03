import sys

# Leer el archivo
with open('MainForm.cs', 'rb') as f:
    content = f.read()

# Eliminar todos los bytes de newline al final
while content and content[-1] in (10, 13):  # \n = 10, \r = 13
    content = content[:-1]

# Escribir sin newline final
with open('MainForm.cs', 'wb') as f:
    f.write(content)

print("Archivo corregido - eliminados newlines finales")
