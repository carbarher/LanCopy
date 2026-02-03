# 🧪 Instrucciones para Probar Búsqueda en eMule

## ✅ Preparación Completada

He agregado **logging detallado** al cliente eMule Web para ver exactamente qué está pasando:

### Logs que verás:
1. `[eMule Web] 🔍 Buscando: [término]`
2. `[eMule Web] 📄 HTML recibido (X bytes)`
3. `[eMule Web] 📋 Preview: [primeros 500 caracteres del HTML]`
4. `[eMule Web] 🔍 Encontradas X filas HTML`
5. `[eMule Web] ✅ Encontrados X resultados`

## 🚀 Pasos para Probar

### 1. Reinicia SlskDown
```bash
# Cierra SlskDown si está abierto
# Ejecuta la nueva versión compilada
```

### 2. Verifica Conexión
Deberías ver:
```
[eMule Web] Conectando a localhost:4711...
[eMule Web] ✅ Conectado exitosamente a la interfaz web de aMule
```

### 3. Realiza una Búsqueda
1. En el campo de búsqueda, escribe: **"test"** o **"mp3"**
2. Haz clic en **"Buscar"**
3. Observa los logs en la ventana de Log

### 4. Analiza los Logs

#### ✅ Caso Exitoso:
```
[eMule Web] 🔍 Buscando: test
[eMule Web] 📄 HTML recibido (15234 bytes)
[eMule Web] 📋 Preview: <!DOCTYPE html><html>...
[eMule Web] 🔍 Encontradas 25 filas HTML
[eMule Web] ✅ Encontrados 10 resultados
📡 eMule: 10 resultados
```

#### ⚠️ Caso Sin Resultados:
```
[eMule Web] 🔍 Buscando: test
[eMule Web] 📄 HTML recibido (3421 bytes)
[eMule Web] 📋 Preview: <!DOCTYPE html><html>...
[eMule Web] 🔍 Encontradas 5 filas HTML
[eMule Web] ✅ Encontrados 0 resultados
```

#### ❌ Caso Error:
```
[eMule Web] 🔍 Buscando: test
[eMule Web] ❌ Error en búsqueda: [mensaje de error]
```

## 🔍 Qué Buscar en los Logs

### 1. **HTML Recibido**
- Si ves `HTML recibido (0 bytes)` → aMule no respondió
- Si ves `HTML recibido (>1000 bytes)` → aMule respondió correctamente

### 2. **Preview del HTML**
- Deberías ver HTML válido que empiece con `<!DOCTYPE` o `<html>`
- Si ves un mensaje de error o login, hay un problema de autenticación

### 3. **Filas HTML**
- Número de `<tr>` encontradas en la respuesta
- Incluye filas de header, por eso puede ser mayor que resultados

### 4. **Resultados Finales**
- Número de archivos válidos parseados
- Deberían aparecer en la lista de resultados

## 🐛 Troubleshooting

### Problema: "HTML recibido (0 bytes)"
**Causa**: aMule no está respondiendo
**Solución**:
1. Verifica que aMule esté corriendo
2. Abre http://localhost:4711 en navegador
3. Verifica que puedas hacer login

### Problema: "Encontradas 0 filas HTML"
**Causa**: El HTML no tiene tabla de resultados
**Solución**:
1. Copia el "Preview" del log
2. Busca si hay un mensaje de error en el HTML
3. Verifica el formato de la respuesta de aMule

### Problema: "Encontrados 0 resultados" (pero hay filas)
**Causa**: El parseo no está funcionando correctamente
**Solución**:
1. Abre http://localhost:4711/search.html?query=test en navegador
2. Inspecciona el HTML de los resultados
3. Compara con el formato esperado en el código

## 📊 Formato HTML Esperado de aMule

El código espera este formato:
```html
<tr>
    <td>nombre_archivo.mp3</td>
    <td>5.2 MB</td>
    <td>3</td>
    <!-- más columnas... -->
</tr>
```

Con enlaces ed2k opcionales:
```html
<a href="ed2k://|file|nombre.mp3|5242880|A1B2C3D4...|/">
```

## 🎯 Resultados Esperados

### Si aMule tiene resultados:
- ✅ Verás archivos en la lista de resultados
- ✅ Columna "Red" mostrará "eMule"
- ✅ Podrás descargarlos

### Si aMule NO tiene resultados:
- ℹ️ Verás "0 resultados" pero sin error
- ℹ️ Es normal si aMule no está conectado a servidores
- ℹ️ O si el término de búsqueda no existe

## 📝 Información para Reportar

Si hay problemas, copia y pega:

1. **Logs completos** de la búsqueda
2. **Preview del HTML** (primeros 500 caracteres)
3. **Número de filas** encontradas
4. **Número de resultados** parseados

## 🔧 Siguiente Paso

Después de esta prueba, podremos:
- ✅ Confirmar que las búsquedas funcionan
- ✅ Ajustar el parseo si es necesario
- ✅ Habilitar Soulseek para búsquedas multi-red

---

**Estado**: ✅ Logging mejorado, listo para probar
**Acción**: Reinicia SlskDown y realiza una búsqueda
