# 📊 Estadísticas de Uso - SlskDown

## 🎯 ¿Qué es?

Un sistema completo de **tracking de estadísticas** que registra automáticamente tu uso de SlskDown para darte información valiosa sobre tus búsquedas y descargas.

---

## 🔍 ¿Cómo Acceder?

### Método 1: Botón INFO sin selección
1. Haz clic en el botón **ℹ️ INFO** (sin seleccionar ningún archivo)
2. Se abre una ventana con todas las estadísticas

### Método 2: Menú contextual
1. Click derecho en la lista de resultados (sin seleccionar archivo)
2. Selecciona **ℹ️ Info**

---

## 📈 Información Mostrada

### ⏱️ TIEMPO DE USO
- **Primer uso**: Fecha y hora de la primera vez que usaste SlskDown
- **Último uso**: Última vez que abriste la aplicación
- **Días de uso**: Cuántos días has usado la aplicación

### 🔍 BÚSQUEDAS
- **Total**: Número total de búsquedas realizadas
- **Hoy**: Búsquedas realizadas hoy
- **Promedio/día**: Búsquedas promedio por día de uso

### 📥 DESCARGAS
- **Total**: Número total de archivos descargados
- **Hoy**: Archivos descargados hoy
- **Total descargado**: Cantidad total de datos descargados (GB/MB)
- **Hoy descargado**: Datos descargados hoy
- **Velocidad promedio**: Velocidad promedio de todas tus descargas (KB/s)

### 👥 TOP 5 USUARIOS
Lista de los 5 usuarios de los que más has descargado, con el número de descargas de cada uno.

### 📁 TOP 5 EXTENSIONES
Las 5 extensiones de archivo que más has descargado (.pdf, .epub, .mp3, etc.)

### 🔎 BÚSQUEDAS RECIENTES
Las últimas 5 búsquedas que realizaste.

---

## 💾 Persistencia

### Archivo Generado
```
app_stats.json
```

### Contenido del Archivo
```json
{
  "TotalSearches": 150,
  "TotalDownloads": 45,
  "TotalBytesDownloaded": 2147483648,
  "AverageSpeedKBps": 1250.5,
  "FirstUseDate": "2025-10-01T10:00:00",
  "LastUseDate": "2025-10-30T17:00:00",
  "TopUsers": {
    "usuario1": 15,
    "usuario2": 10,
    "usuario3": 8
  },
  "TopExtensions": {
    ".pdf": 20,
    ".epub": 15,
    ".mp3": 10
  },
  "RecentSearches": [
    "Isaac Asimov Foundation",
    "Frank Herbert Dune",
    "Philip K Dick"
  ],
  "SearchesToday": 5,
  "DownloadsToday": 2,
  "BytesDownloadedToday": 104857600
}
```

---

## 🔄 Auto-Guardado

Las estadísticas se guardan automáticamente:
- **Cada 10 búsquedas** realizadas
- **Cada 5 descargas** completadas
- Al cerrar la aplicación

No necesitas hacer nada manualmente.

---

## 🕵️ Modo Incógnito

**IMPORTANTE**: Las estadísticas **NO se registran** cuando el **Modo Incógnito** está activo.

Si tienes activado el modo incógnito:
- ❌ No se registran búsquedas
- ❌ No se registran descargas
- ✅ Las estadísticas anteriores se mantienen

---

## 📊 Ejemplo de Ventana

```
📊 ESTADÍSTICAS DE USO

⏱️ TIEMPO DE USO:
   • Primer uso: 01/10/2025 10:00
   • Último uso: 30/10/2025 17:05
   • Días de uso: 30

🔍 BÚSQUEDAS:
   • Total: 150
   • Hoy: 5
   • Promedio/día: 5.0

📥 DESCARGAS:
   • Total: 45
   • Hoy: 2
   • Total descargado: 2.00 GB
   • Hoy descargado: 100.00 MB
   • Velocidad promedio: 1250.5 KB/s

👥 TOP 5 USUARIOS:
   • usuario1: 15 descargas
   • usuario2: 10 descargas
   • usuario3: 8 descargas
   • usuario4: 7 descargas
   • usuario5: 5 descargas

📁 TOP 5 EXTENSIONES:
   • .pdf: 20 archivos
   • .epub: 15 archivos
   • .mp3: 10 archivos
   • .mobi: 5 archivos
   • .azw3: 3 archivos

🔎 BÚSQUEDAS RECIENTES:
   • Isaac Asimov Foundation
   • Frank Herbert Dune
   • Philip K Dick
   • Arthur C Clarke
   • Ray Bradbury
```

---

## 🛠️ Implementación Técnica

### StatsService
```csharp
// Registrar búsqueda
_stats.RecordSearch(query, resultsCount);

// Registrar descarga
_stats.RecordDownload(filename, size, speedKBps, username);

// Obtener estadísticas
var stats = _stats.GetStats();
```

### Tracking Automático
- Se registra al **completar** una búsqueda
- Se registra al **completar** una descarga
- Respeta el modo incógnito
- Calcula velocidad promedio automáticamente
- Mantiene top 5 de usuarios y extensiones

---

## 🎨 Interfaz

### Ventana de Estadísticas
- **Tamaño**: 600x700 pixels
- **Estilo**: Oscuro (tema consistente con la app)
- **Fuente**: Consolas 10pt (monoespaciada para alineación)
- **Scroll**: Automático si hay mucho contenido
- **Botón**: "Cerrar" en la parte inferior

---

## 🔒 Privacidad

### Datos Almacenados
- ✅ Número de búsquedas y descargas
- ✅ Tamaños de archivos
- ✅ Velocidades de descarga
- ✅ Nombres de usuarios (de Soulseek)
- ✅ Extensiones de archivos
- ✅ Términos de búsqueda

### Datos NO Almacenados
- ❌ Nombres completos de archivos
- ❌ Rutas de archivos
- ❌ Contenido de archivos
- ❌ IPs o información de red

### Ubicación
- **Local**: Todo se guarda en tu PC
- **No se envía**: Nada se envía a internet
- **Privado**: Solo tú tienes acceso

---

## 🗑️ Resetear Estadísticas

Si quieres empezar de cero:

1. Cierra SlskDown
2. Elimina el archivo `app_stats.json`
3. Abre SlskDown
4. ✅ Estadísticas reseteadas

---

## 💡 Casos de Uso

### 1. Monitorear Uso
Ver cuánto usas la aplicación y cuánto descargas.

### 2. Identificar Fuentes
Descubrir de qué usuarios descargas más frecuentemente.

### 3. Analizar Preferencias
Ver qué tipos de archivos (extensiones) descargas más.

### 4. Optimizar Búsquedas
Revisar búsquedas recientes para refinar términos.

### 5. Velocidad de Conexión
Monitorear la velocidad promedio de tus descargas.

---

## 🎯 Ventajas

- ✅ **Automático**: No requiere intervención manual
- ✅ **Ligero**: Impacto mínimo en rendimiento
- ✅ **Persistente**: Se mantiene entre sesiones
- ✅ **Privado**: Todo local, nada en la nube
- ✅ **Respeta incógnito**: No registra en modo privado
- ✅ **Informativo**: Datos útiles y bien presentados

---

## 📝 Logging

Las operaciones de estadísticas se registran en el log:

```
[2025-10-30 17:00:00.123] [DEBUG] Búsqueda registrada: 'Isaac Asimov' (25 resultados)
[2025-10-30 17:05:00.456] [DEBUG] Descarga registrada: Foundation.epub (2.5 MB, 1250.5 KB/s)
[2025-10-30 17:10:00.789] [DEBUG] Estadísticas guardadas: 150 búsquedas, 45 descargas
```

---

## 🎉 Conclusión

El sistema de estadísticas te da **visibilidad completa** de tu uso de SlskDown, ayudándote a:
- Entender tus patrones de uso
- Identificar tus fuentes favoritas
- Monitorear tu actividad de descarga
- Optimizar tus búsquedas

**Todo de forma automática, privada y sin esfuerzo.**

---

**Fecha**: 30 de octubre de 2025  
**Versión**: SlskDown 1.3  
**Estado**: ✅ Implementado y funcional
