# Sistema de Logging de SlskDown

## 📁 Ubicación de los Logs

Los logs se guardan automáticamente en:

```
%APPDATA%\SlskDown\logs\
```

Ruta completa típica:
```
C:\Users\[TuUsuario]\AppData\Roaming\SlskDown\logs\
```

## 📄 Formato de Archivos

Los archivos de log se crean con el siguiente formato:

```
slskdown-YYYY-MM-DD.txt
```

Ejemplo:
- `slskdown-2025-11-14.txt`
- `slskdown-2025-11-15.txt`

Se crea un archivo nuevo cada día automáticamente.

## 🔍 Contenido de los Logs

Los logs contienen:
- ✅ Eventos de conexión/desconexión
- 🔍 Búsquedas realizadas
- 📥 Descargas iniciadas y completadas
- ⚠️ Errores y advertencias
- 📊 Estadísticas y métricas
- 🔄 Reconexiones automáticas
- 💾 Operaciones de base de datos

Cada línea tiene el formato:
```
[HH:mm:ss] Mensaje
```

Ejemplo:
```
[14:05:17] ✅ Conexión exitosa en puerto 58571
[14:05:18] 🔍 Buscando: asimov
[14:05:20] 📥 Descargando: Foundation.epub
```

## 🚀 Características del Sistema

### Lock-Free Logging
- **Sin bloqueos**: Usa un buffer circular lock-free de 50,000 mensajes
- **Alto rendimiento**: No bloquea el thread principal
- **Thread dedicado**: Procesa logs en background con prioridad baja
- **Retry automático**: Reintenta escritura si el archivo está bloqueado

### Acceso Rápido
- **Botón en UI**: "📁 Abrir Logs" en la pestaña Log
- **Mensaje al inicio**: Muestra la ruta completa al iniciar

## 💡 Uso

### Desde la Aplicación
1. Ve a la pestaña **📋 Log**
2. Haz clic en **📁 Abrir Logs**
3. Se abrirá el explorador de Windows en la carpeta de logs

### Manualmente
1. Presiona `Win + R`
2. Escribe: `%APPDATA%\SlskDown\logs`
3. Presiona Enter

## 🔧 Troubleshooting

### Los logs no se guardan
- Verifica que tengas permisos de escritura en `%APPDATA%`
- Revisa que no haya antivirus bloqueando la escritura
- Comprueba que haya espacio en disco

### Archivo de log muy grande
Los archivos se rotan diariamente, pero si un día tiene muchos logs:
- Puedes eliminar archivos antiguos manualmente
- Los archivos son texto plano y se pueden comprimir fácilmente

### Buscar en logs
Usa cualquier editor de texto o herramientas como:
- `findstr` (Windows)
- Notepad++
- Visual Studio Code
- grep (Git Bash)

Ejemplo con findstr:
```cmd
findstr "Error" slskdown-2025-11-14.txt
```

## 📊 Análisis de Logs

### Contar errores del día
```cmd
findstr /C:"❌" slskdown-2025-11-14.txt | find /C /V ""
```

### Ver solo descargas
```cmd
findstr "📥" slskdown-2025-11-14.txt
```

### Ver conexiones
```cmd
findstr "Conexión" slskdown-2025-11-14.txt
```

## 🗑️ Limpieza

Para limpiar logs antiguos:

```cmd
cd %APPDATA%\SlskDown\logs
del slskdown-2025-10-*.txt
```

O elimina manualmente los archivos que no necesites.
