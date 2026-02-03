# 🤖 Guía de Instalación y Configuración de Ollama para SlskDown

## **Fecha**: 17 de enero de 2026

---

## **📋 ¿Qué es Ollama?**

Ollama es un servidor de IA local que te permite ejecutar modelos de lenguaje (como Llama 2, Mistral, etc.) **completamente gratis** en tu propia computadora, sin necesidad de API keys ni conexión a internet.

**Ventajas**:
- ✅ **100% Gratis** - Sin costos de API
- ✅ **Privacidad Total** - Todo funciona localmente
- ✅ **Sin Límites** - Usa cuanto quieras
- ✅ **Offline** - No necesita internet una vez instalado

---

## **💻 Instalación en Windows**

### **Paso 1: Descargar Ollama**

1. Ve a: **https://ollama.ai/download**
2. Descarga **Ollama para Windows** (OllamaSetup.exe)
3. Tamaño aproximado: ~500 MB

### **Paso 2: Instalar Ollama**

1. Ejecuta **OllamaSetup.exe**
2. Sigue el asistente de instalación
3. Acepta los permisos cuando se soliciten
4. La instalación tarda ~2-3 minutos

### **Paso 3: Verificar la Instalación**

Abre **PowerShell** o **CMD** y ejecuta:

```powershell
ollama --version
```

Deberías ver algo como:
```
ollama version 0.1.20
```

---

## **🚀 Configuración Inicial**

### **Paso 1: Descargar un Modelo**

Ollama necesita al menos un modelo de IA. Recomendamos empezar con **Llama 2** (7B):

```powershell
ollama pull llama2
```

**Tiempo de descarga**: ~5-10 minutos (depende de tu conexión)  
**Tamaño**: ~3.8 GB

**Otros modelos disponibles**:
```powershell
ollama pull mistral        # Más rápido, 7B
ollama pull codellama      # Especializado en código
ollama pull llama2:13b     # Más inteligente pero más lento
ollama pull phi            # Muy pequeño (2.7B), rápido
```

### **Paso 2: Iniciar el Servidor Ollama**

```powershell
ollama serve
```

Deberías ver:
```
Ollama is running on http://localhost:11434
```

**IMPORTANTE**: Deja esta ventana abierta mientras uses SlskDown con IA.

### **Paso 3: Probar la Conexión**

En **otra ventana** de PowerShell:

```powershell
curl http://localhost:11434/api/tags
```

Si funciona, verás una lista de modelos instalados.

---

## **🔧 Configuración en SlskDown**

### **Opción 1: Configuración Automática (Recomendada)**

SlskDown ya está configurado para usar Ollama en `http://localhost:11434` por defecto.

1. Abre **SlskDown**
2. Ve a la pestaña **Configuración**
3. Busca la sección **🤖 INTELIGENCIA ARTIFICIAL (OLLAMA)**
4. Marca **"✅ Activar funcionalidades de IA con Ollama"**
5. Guarda la configuración

### **Opción 2: Configuración Manual**

Si Ollama está en otro puerto o servidor, edita `config.json`:

**Ubicación**: `%AppData%\SlskDown\config.json`

```json
{
  "aiEnabled": true,
  "ollamaUrl": "http://localhost:11434",
  "ollamaModel": "llama2"
}
```

---

## **✅ Verificación de Funcionamiento**

### **Test 1: Verificar que Ollama está corriendo**

```powershell
# Debe retornar 200 OK
curl http://localhost:11434/api/tags
```

### **Test 2: Probar una consulta simple**

```powershell
curl http://localhost:11434/api/generate -d '{
  "model": "llama2",
  "prompt": "¿Qué es Soulseek?",
  "stream": false
}'
```

### **Test 3: Probar desde SlskDown**

1. Inicia **SlskDown**
2. Ve a la pestaña **Automático**
3. Inicia una búsqueda automática
4. Verás mensajes como:
   ```
   💬 CHAT INTERACTIVO CON IA
   ⏳ Consultando a Ollama...
   ✅ Respuesta recibida
   ```

---

## **🐛 Solución de Problemas**

### **Error: "404 Not Found"**

**Causa**: Ollama no está corriendo

**Solución**:
```powershell
# Inicia Ollama
ollama serve
```

### **Error: "Connection refused"**

**Causa**: Puerto 11434 bloqueado o Ollama no instalado

**Solución**:
1. Verifica que Ollama esté instalado: `ollama --version`
2. Verifica el firewall de Windows
3. Reinicia Ollama: `ollama serve`

### **Error: "Model not found"**

**Causa**: No has descargado el modelo

**Solución**:
```powershell
ollama pull llama2
```

### **Ollama es muy lento**

**Causa**: Modelo muy grande para tu hardware

**Solución**: Usa un modelo más pequeño
```powershell
ollama pull phi          # Modelo pequeño (2.7B)
ollama pull mistral:7b   # Balance entre velocidad y calidad
```

---

## **⚙️ Configuración Avanzada**

### **Cambiar el Puerto de Ollama**

Por defecto Ollama usa el puerto 11434. Para cambiarlo:

```powershell
# Windows
set OLLAMA_HOST=0.0.0.0:8080
ollama serve
```

Luego actualiza `config.json` en SlskDown:
```json
{
  "ollamaUrl": "http://localhost:8080"
}
```

### **Usar Ollama en Otra Computadora**

Si tienes Ollama en otra PC de tu red:

```json
{
  "ollamaUrl": "http://192.168.1.100:11434"
}
```

### **Optimizar Rendimiento**

```powershell
# Usar GPU (si tienes NVIDIA)
set OLLAMA_GPU=1
ollama serve

# Limitar uso de RAM
set OLLAMA_MAX_LOADED_MODELS=1
ollama serve
```

---

## **📊 Modelos Recomendados por Uso**

| Modelo | Tamaño | Velocidad | Calidad | Uso Recomendado |
|--------|--------|-----------|---------|-----------------|
| **phi** | 2.7B | ⚡⚡⚡ | ⭐⭐ | PCs lentas, respuestas rápidas |
| **llama2** | 7B | ⚡⚡ | ⭐⭐⭐ | **Balance ideal** |
| **mistral** | 7B | ⚡⚡ | ⭐⭐⭐⭐ | Mejor calidad en 7B |
| **llama2:13b** | 13B | ⚡ | ⭐⭐⭐⭐ | PCs potentes |
| **codellama** | 7B | ⚡⚡ | ⭐⭐⭐ | Análisis de código |

---

## **🎯 Funcionalidades de IA en SlskDown**

Una vez configurado Ollama, tendrás acceso a:

### **1. Chat Interactivo**
```
💬 Pregunta: ¿Qué autores de ciencia ficción me recomiendas?
🤖 Respuesta: Te recomiendo Isaac Asimov, Philip K. Dick, Arthur C. Clarke...
```

### **2. Recomendaciones Inteligentes**
- Sugerencias de autores basadas en tus búsquedas
- Autores similares a los que ya tienes
- Descubrimiento de nuevos géneros

### **3. Auto-Tagging**
- Clasificación automática de archivos
- Detección de género literario
- Extracción de metadatos

### **4. Predicción de Calidad**
- Análisis de calidad de archivos
- Detección de duplicados inteligente
- Priorización automática

---

## **💡 Consejos y Trucos**

### **Iniciar Ollama Automáticamente**

Crea un script `start_ollama.bat`:

```batch
@echo off
echo Iniciando Ollama...
start /min ollama serve
echo Ollama iniciado en segundo plano
timeout /t 3
```

Colócalo en la carpeta de inicio de Windows:
`%AppData%\Microsoft\Windows\Start Menu\Programs\Startup`

### **Verificar Estado de Ollama**

```powershell
# Ver modelos instalados
ollama list

# Ver uso de recursos
ollama ps

# Detener Ollama
taskkill /F /IM ollama.exe
```

### **Actualizar Ollama**

```powershell
# Descargar última versión desde https://ollama.ai/download
# Ejecutar nuevo instalador (sobrescribe la versión anterior)
```

---

## **📚 Recursos Adicionales**

- **Sitio oficial**: https://ollama.ai
- **Documentación**: https://github.com/ollama/ollama/blob/main/docs/api.md
- **Modelos disponibles**: https://ollama.ai/library
- **Discord de Ollama**: https://discord.gg/ollama

---

## **🆘 Soporte**

Si tienes problemas con Ollama:

1. **Revisa los logs de Ollama**: Busca errores en la ventana donde ejecutaste `ollama serve`
2. **Verifica los logs de SlskDown**: Busca mensajes de error relacionados con IA
3. **Prueba con un modelo más pequeño**: `ollama pull phi`
4. **Reinicia todo**: Cierra Ollama, SlskDown, y vuelve a iniciar

---

## **✅ Checklist de Configuración**

- [ ] Ollama instalado (`ollama --version` funciona)
- [ ] Modelo descargado (`ollama list` muestra al menos un modelo)
- [ ] Servidor corriendo (`ollama serve` activo)
- [ ] Puerto accesible (`curl http://localhost:11434/api/tags` funciona)
- [ ] SlskDown configurado (IA activada en Configuración)
- [ ] Prueba exitosa (chat interactivo responde)

---

## **🎉 ¡Listo!**

Una vez completados todos los pasos, SlskDown podrá usar IA local para:
- Recomendaciones personalizadas de autores
- Chat interactivo sobre libros
- Auto-clasificación de archivos
- Búsquedas inteligentes

**Todo gratis, privado y sin límites.** 🚀
