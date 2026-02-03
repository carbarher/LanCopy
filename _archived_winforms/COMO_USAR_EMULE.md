# 🚀 Cómo Usar eMule con SlskDown

## ✅ **YA ESTÁ TODO LISTO**

SlskDown ya está configurado para conectarse con tu eMule vía Servidor Web (puerto 4711).

---

## 🔧 **CONFIGURACIÓN RÁPIDA (3 PASOS)**

### **Paso 1: Configurar contraseña en SlskDown**

Abre el archivo de configuración:
```
c:\p2p\SlskDown\config.json
```

Busca la línea `"emulePassword"` y pon la contraseña de tu eMule:
```json
{
  "emulePassword": "LA_CONTRASEÑA_QUE_PUSISTE_EN_EMULE",
  "enableEmule": true,
  "enableSoulseek": true
}
```

**Importante:** Usa la misma contraseña que configuraste en:
- eMule → Preferencias → Servidor Web → Contraseña

---

### **Paso 2: Habilitar eMule en SlskDown**

En la aplicación SlskDown:
1. Ve a la pestaña **"Configuración"** o **"Redes"**
2. Marca ✅ **"Habilitar eMule"**
3. La aplicación se conectará automáticamente

---

### **Paso 3: Verificar conexión**

En los logs de SlskDown deberías ver:
```
🔌 Conectando a eMule WebServer...
✅ eMule conectado vía Servidor Web (puerto 4711)
   Búsquedas multi-red habilitadas: Soulseek + eMule
```

---

## 🔍 **CÓMO HACER BÚSQUEDAS MULTI-RED**

Una vez conectado, **todas tus búsquedas automáticamente buscarán en ambas redes**:

1. Escribe tu búsqueda en el cuadro de texto
2. Click en **"Buscar"**
3. SlskDown buscará simultáneamente en:
   - 🟢 Soulseek
   - 🟠 eMule

Los resultados mostrarán la columna **"Red"** indicando el origen:
```
Archivo.epub    - Red: Soulseek
Archivo.pdf     - Red: eMule
```

---

## 📊 **VENTAJAS DEL SISTEMA MULTI-RED**

| Antes (solo Soulseek) | Ahora (Soulseek + eMule) |
|----------------------|--------------------------|
| ~100 resultados | ~500+ resultados |
| 1 fuente por archivo | Múltiples fuentes |
| Sin redundancia | Failover automático |
| Velocidad variable | Prioriza fuente más rápida |

---

## ⚙️ **CONFIGURACIÓN AVANZADA**

### **Cambiar contraseña de eMule:**
1. Edita `config.json`
2. Cambia `"emulePassword": "nueva_contraseña"`
3. Reinicia SlskDown

### **Deshabilitar eMule temporalmente:**
1. Edita `config.json`
2. Cambia `"enableEmule": false`
3. Reinicia SlskDown

### **Usar solo eMule (sin Soulseek):**
```json
{
  "enableSoulseek": false,
  "enableEmule": true
}
```

---

## 🔍 **TROUBLESHOOTING**

### **Error: "No se pudo conectar a eMule"**
- Verifica que eMule esté corriendo
- Verifica que el Servidor Web esté habilitado (puerto 4711)
- Verifica que la contraseña en `config.json` sea correcta

### **Error: "Contraseña incorrecta"**
- Abre eMule → Preferencias → Servidor Web
- Verifica la contraseña configurada
- Cópiala exactamente en `config.json`

### **eMule no aparece en búsquedas**
- Verifica que `"enableEmule": true` en config.json
- Verifica los logs para ver si hay errores de conexión
- Reinicia SlskDown

### **Resultados solo de Soulseek**
- Espera unos segundos, eMule es más lento
- Verifica que eMule esté conectado a servidores ed2k
- Verifica que eMule tenga Kad habilitado

---

## 📝 **EJEMPLO DE config.json COMPLETO**

```json
{
  "username": "",
  "password": "",
  "downloadDir": "c:\\downloads",
  "autoConnect": true,
  "enableSoulseek": true,
  "enableEmule": true,
  "emulePassword": "tu_contraseña_aqui",
  "maxParallelDownloads": 4,
  "maxRetries": 3,
  "searchTimeout": 30
}
```

---

## 🎯 **RESUMEN**

1. ✅ eMule ya está corriendo con Servidor Web habilitado
2. ✅ SlskDown ya tiene el código de integración
3. ⚙️ Solo falta configurar la contraseña en `config.json`
4. 🚀 Reiniciar SlskDown y empezar a buscar

**¡Disfruta de búsquedas multi-red!** 🎉
