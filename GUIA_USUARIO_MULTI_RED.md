# 📖 Guía de Usuario: Búsquedas y Descargas Multi-Red

## 🎯 Introducción

SlskDown ahora soporta búsquedas y descargas desde **múltiples redes P2P** simultáneamente:

- **Soulseek**: Red principal, especializada en música y contenido cultural
- **eMule/ed2k**: Red secundaria con gran catálogo de libros y documentos

Esta guía te enseñará cómo aprovechar al máximo esta funcionalidad.

---

## 🚀 Inicio Rápido

### 1. **Habilitar eMule** (Primera vez)

1. Abrir SlskDown
2. Ir a **Configuración** (botón ⚙️)
3. Buscar sección **"🌐 REDES P2P"**
4. Activar checkbox **"🔷 Habilitar eMule/ed2k"**
5. **Reiniciar** la aplicación

### 2. **Instalar aMule Daemon** (Requerido)

**Windows**:
```powershell
# Descargar desde: https://www.amule.org/
# Instalar y ejecutar amuled
```

**Linux**:
```bash
sudo apt-get install amule-daemon
amuled -f  # Iniciar daemon
```

**Configurar** (`~/.aMule/amule.conf`):
```ini
[ExternalConnect]
AcceptExternalConnections=1
ECPassword=<tu_contraseña_md5>
ECPort=4712
```

### 3. **Realizar Primera Búsqueda**

1. Escribir término de búsqueda en la caja de texto
2. Hacer clic en **"Buscar"**
3. Esperar resultados (aparecerán de ambas redes)
4. Ver columna **"Red"** para identificar origen

---

## 🔍 Búsquedas Multi-Red

### Cómo Funciona

Cuando realizas una búsqueda, SlskDown:

1. **Busca en paralelo** en Soulseek y eMule
2. **Deduplica** resultados automáticamente
3. **Prioriza** por disponibilidad y velocidad
4. **Muestra** todos los resultados en una lista unificada

### Identificar Red de Origen

Los resultados muestran la red de origen en la columna **"Red"**:

```
Archivo                    Tamaño    Usuario      Red
───────────────────────────────────────────────────────
libro.pdf                  2.5 MB    user123      Soulseek
libro.pdf                  2.5 MB    [hash]       eMule
machine_learning.epub      5.1 MB    reader99     Soulseek
```

### Logs de Búsqueda

Durante la búsqueda verás logs como:

```
🌐 Búsqueda multi-red iniciada en 2 redes
✅ Multi-red: 45 resultados totales
   📡 Soulseek: 30 resultados
   📡 eMule: 15 resultados
```

### Caché Inteligente

SlskDown guarda resultados en caché por **30 minutos**:

- ✅ Búsquedas repetidas son **instantáneas**
- ✅ Ahorra ancho de banda
- ✅ Reduce carga en las redes

---

## 📥 Descargas Multi-Red

### Descargar desde Soulseek

1. Seleccionar archivo con **Red = "Soulseek"**
2. Hacer clic en **"Descargar"**
3. Ver progreso en tiempo real
4. Archivo se guarda en carpeta configurada

### Descargar desde eMule

1. Seleccionar archivo con **Red = "eMule"**
2. Hacer clic en **"Descargar"**
3. Ver progreso con velocidad en tiempo real:
   ```
   Descargando... 1.2 MB / 4.7 MB (150 KB/s) - 25.5%
   ```
4. Al completar: **"✅ Completado (eMule)"**

### Diferencias entre Redes

| Característica | Soulseek | eMule |
|----------------|----------|-------|
| **Velocidad** | Alta (directa) | Media (múltiples fuentes) |
| **Disponibilidad** | Depende de usuario | Múltiples fuentes |
| **Progreso** | Tiempo real | Tiempo real |
| **Cancelación** | Sí | Sí |
| **Reanudación** | Limitada | Sí (automática) |

---

## 🎛️ Configuración Avanzada

### Preferencias de Red

En **Configuración** → **Redes P2P**:

- **Habilitar eMule**: Activa/desactiva red eMule
- **Prioridad**: Soulseek siempre tiene prioridad
- **Timeout**: Tiempo máximo de búsqueda (configurable)

### Caché de Resultados

El caché se gestiona automáticamente:

- **Duración**: 30 minutos
- **Tamaño máximo**: 1000 búsquedas
- **Limpieza**: Automática (entradas más antiguas)

Para ver estadísticas del caché (en logs):
```
Caché: 150 queries, 4500 resultados, 45 hits, promedio 30.0 resultados/query
```

---

## 🔧 Solución de Problemas

### ❌ "eMule no está conectado"

**Causa**: aMule daemon no está corriendo

**Solución**:
```bash
# Verificar si está corriendo
ps aux | grep amuled  # Linux
tasklist | findstr amuled  # Windows

# Iniciar si no está corriendo
amuled -f
```

### ❌ "Error: Hash ed2k no disponible"

**Causa**: Resultado de eMule sin hash válido

**Solución**:
- Buscar de nuevo (puede ser resultado corrupto)
- Intentar descargar otro resultado similar
- Reportar en logs si persiste

### ❌ "Descarga desde eMule no soportada aún"

**Causa**: Versión antigua de SlskDown

**Solución**:
- Actualizar a última versión
- Verificar que `_emuleEnabled = true` en código

### ⚠️ Búsquedas lentas

**Causa**: Timeout muy alto o redes lentas

**Solución**:
1. Reducir timeout en configuración
2. Verificar conexión a internet
3. Verificar que aMule daemon esté respondiendo

### 📊 Ver Logs Detallados

Los logs muestran información útil:

```
📥 Iniciando descarga desde eMule: libro.pdf
🔄 Progreso: 25.5% (1.2 MB / 4.7 MB) @ 150 KB/s
✅ Descarga completada desde eMule: libro.pdf
```

Si hay errores:
```
❌ Error descargando desde eMule: No hay conexión activa
⚠️ Hash ed2k no disponible para: archivo.pdf
```

---

## 💡 Consejos y Trucos

### 1. **Búsquedas Específicas**

Para mejores resultados:
- Usa términos específicos: `"machine learning python"` mejor que `"ml"`
- Incluye formato si es importante: `"libro epub"` o `"pdf"`
- Evita caracteres especiales

### 2. **Comparar Resultados**

Si el mismo archivo aparece en ambas redes:
- **Soulseek**: Descarga más rápida, directa
- **eMule**: Más fuentes, mejor para archivos raros

### 3. **Aprovechar el Caché**

- Búsquedas repetidas son instantáneas
- Útil para explorar resultados sin esperar
- Se limpia automáticamente después de 30 min

### 4. **Descargas Paralelas**

Puedes descargar desde ambas redes simultáneamente:
- Archivo A desde Soulseek
- Archivo B desde eMule
- Ambos se descargan en paralelo

### 5. **Monitorear Progreso**

La UI muestra:
- **Porcentaje**: `25.5%`
- **Bytes**: `1.2 MB / 4.7 MB`
- **Velocidad**: `150 KB/s` (solo eMule)
- **Estado**: `Descargando...`, `✅ Completado`, `❌ Error`

---

## 📈 Estadísticas

### Ver Estadísticas de Red

En logs verás estadísticas como:

```
🌐 Redes activas: 2
   📡 Soulseek: Conectado (150 peers)
   📡 eMule: Conectado (4500 peers)

📊 Búsquedas hoy: 45
   Soulseek: 30 búsquedas
   eMule: 15 búsquedas
   
💾 Caché: 150 queries, 45 hits (30% hit rate)
```

### Métricas de Rendimiento

- **Búsqueda promedio**: 2-5 segundos
- **Búsqueda desde caché**: <100 ms
- **Descarga Soulseek**: 500 KB/s - 2 MB/s
- **Descarga eMule**: 100 KB/s - 500 KB/s

---

## 🔐 Seguridad y Privacidad

### Conexiones

- **Soulseek**: Conexión directa P2P
- **eMule**: Conexión a través de daemon local
- **Caché**: Solo local, no se comparte

### Datos Compartidos

- **Búsquedas**: Se envían a las redes respectivas
- **Descargas**: Directas desde peers
- **No se comparte**: Tu biblioteca local (a menos que lo configures)

### Recomendaciones

1. Usa VPN si te preocupa la privacidad
2. Verifica archivos descargados con antivirus
3. No descargues contenido ilegal
4. Respeta derechos de autor

---

## 🆘 Soporte

### Reportar Problemas

Si encuentras un problema:

1. **Revisar logs**: Busca mensajes de error
2. **Verificar configuración**: aMule daemon, conexión
3. **Reproducir**: Intenta replicar el problema
4. **Reportar**: Incluye logs y pasos para reproducir

### Información Útil para Reportes

```
- Versión de SlskDown: [versión]
- Sistema operativo: [Windows/Linux/Mac]
- aMule version: [versión]
- Red afectada: [Soulseek/eMule/Ambas]
- Logs relevantes: [copiar logs]
```

---

## 📚 Recursos Adicionales

### Documentación Técnica

- **Arquitectura Multi-Red**: `MULTI_NETWORK_ARCHITECTURE.md`
- **Integración eMule**: `EMULE_INTEGRATION_COMPLETED.md`
- **Guía de Instalación aMule**: `EMule/INSTALLATION_GUIDE.md`
- **Tests**: `EMule/TESTING_README.md`

### Enlaces Útiles

- **aMule**: https://www.amule.org/
- **Soulseek**: https://www.slsknet.org/
- **Protocolo EC**: https://wiki.amule.org/wiki/EC_Protocol_HOWTO

---

## ✨ Conclusión

La funcionalidad multi-red de SlskDown te permite:

- ✅ Buscar en múltiples redes simultáneamente
- ✅ Descargar desde la red más conveniente
- ✅ Aprovechar caché inteligente
- ✅ Ver progreso en tiempo real
- ✅ Acceder a catálogos más amplios

**¡Disfruta de tu experiencia multi-red mejorada!** 🚀

---

**Última actualización**: 2 de diciembre de 2025  
**Versión**: 1.0  
**Autor**: Equipo SlskDown
