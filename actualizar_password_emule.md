# Cómo actualizar la contraseña de eMule en SlskDown

## Ubicación del archivo de configuración:
```
C:\Users\carlo\AppData\Roaming\SlskDown\config.json
```

## Pasos:

1. Cierra SlskDown si está abierto

2. Abre el archivo config.json con un editor de texto (Notepad, VS Code, etc.)

3. Busca la línea que contiene "EMulePassword"

4. Cambia el valor a la contraseña correcta:
   ```json
   "EMulePassword": "tu_password_correcta_aqui"
   ```

5. Guarda el archivo

6. Reinicia SlskDown

## Contraseñas comunes de aMule:
- `amule` (la más común)
- `admin`
- (vacío - sin contraseña)

## Verificar que funciona:
1. Abre el navegador
2. Ve a http://localhost:4711
3. Ingresa la contraseña
4. Si entras correctamente, esa es la contraseña que debes usar en SlskDown
