# 🚀 Guía de Instalación de Ollama

## 📥 Instalación en Windows

### **Método 1: Instalador Automático (Recomendado)**

1. **Descargar instalador**:
   - Ir a: [https://ollama.com/download](https://ollama.com/download)
   - Clic en "Download for Windows"
   - Se descargará `OllamaSetup.exe`

2. **Ejecutar instalador**:
   - Doble clic en `OllamaSetup.exe`
   - Seguir el asistente de instalación
   - Ollama se instalará y ejecutará automáticamente

3. **Verificar instalación**:
   ```cmd
   ollama --version
   ```
   
   Deberías ver algo como: `ollama version 0.1.x`

---

### **Método 2: Instalador Ya Descargado**

Si ya descargaste el instalador:

```cmd
# El instalador está en:
%TEMP%\OllamaSetup.exe

# Ejecutarlo:
start %TEMP%\OllamaSetup.exe
```

---

## 📦 Descargar Modelo (Después de Instalar)

Una vez instalado Ollama, descarga un modelo:

### **Modelo Recomendado: llama2**

```cmd
ollama pull llama2
```

**Tamaño**: ~3.8GB  
**Tiempo**: 5-10 minutos (depende de tu conexión)

### **Alternativas**:

```cmd
# Más ligero (1.3GB) - Para PCs modestas
ollama pull phi

# Más preciso (4.1GB) - Mejor calidad
ollama pull mistral

# Más avanzado (4.7GB) - Máxima precisión
ollama pull llama3

# Para código (3.8GB) - Análisis técnico
ollama pull codellama
```

---

## ✅ Verificar que Funciona

### **1. Verificar servicio**

```cmd
# Ver si Ollama está ejecutándose
tasklist | findstr ollama
```

Deberías ver: `ollama.exe`

### **2. Listar modelos descargados**

```cmd
ollama list
```

### **3. Probar modelo**

```cmd
ollama run llama2 "Hola, ¿cómo estás?"
```

Debería responder en español.

---

## 🔧 Configurar en SlskDown

Una vez instalado y con modelo descargado:

1. **Abrir SlskDown**
2. **Clic en botón 🤖 IA** en toolbar
3. **Configurar**:
   - URL: `http://localhost:11434` (default)
   - Modelo: Seleccionar el modelo descargado (ej: `llama2`)
   - ✅ Marcar "Habilitar funcionalidades de IA"
4. **Clic en "Probar Conexión"**
   - Debería mostrar: ✅ Conexión exitosa
   - Y listar modelos disponibles
5. **Guardar**

---

## 🎯 Funcionalidades Disponibles

Una vez configurado, tendrás acceso a:

1. 🔍 **Búsqueda Inteligente** - Expande queries automáticamente
2. 📚 **Recomendaciones** - Sugiere libros similares
3. 🏷️ **Auto-Tagging** - Organiza biblioteca automáticamente
4. 🎯 **Predicción de Calidad** - Evalúa archivos antes de descargar
5. 💬 **Chatbot Asistente** - Ayuda conversacional
6. 🔮 **Predicción de Disponibilidad** - Predice cuándo encontrar archivos raros
7. 📝 **Resúmenes de Libros** - Metadata completa
8. 🚨 **Detección de Malware** - Protección automática

---

## 🐛 Solución de Problemas

### **Problema: "ollama: command not found"**

**Solución**: Reiniciar terminal o PC después de instalar.

```cmd
# Cerrar y abrir nueva terminal
# O reiniciar PC
```

### **Problema: "Failed to connect"**

**Solución**: Verificar que Ollama esté ejecutándose.

```cmd
# Ver procesos
tasklist | findstr ollama

# Si no está, iniciarlo
ollama serve
```

### **Problema: "Model not found"**

**Solución**: Descargar el modelo primero.

```cmd
ollama pull llama2
```

### **Problema: Muy lento**

**Soluciones**:
1. Usar modelo más pequeño: `ollama pull phi`
2. Cerrar otras aplicaciones
3. Verificar que use GPU (si tienes NVIDIA)

---

## 📊 Requisitos del Sistema

### **Mínimos**
- Windows 10/11
- 8GB RAM
- 5GB espacio en disco
- CPU moderna (cualquiera)

### **Recomendados**
- 16GB RAM
- SSD
- GPU NVIDIA (opcional, acelera 10x)

---

## 💡 Comandos Útiles

```cmd
# Ver versión
ollama --version

# Listar modelos instalados
ollama list

# Eliminar modelo
ollama rm llama2

# Ver uso de recursos
tasklist /FI "IMAGENAME eq ollama.exe"

# Detener Ollama
taskkill /F /IM ollama.exe
```

---

## 🔗 Enlaces Útiles

- **Sitio oficial**: https://ollama.com
- **Descargas**: https://ollama.com/download
- **Modelos disponibles**: https://ollama.com/library
- **GitHub**: https://github.com/ollama/ollama
- **Documentación**: https://github.com/ollama/ollama/blob/main/docs/README.md

---

## ✅ Checklist

- [ ] Descargar OllamaSetup.exe
- [ ] Instalar Ollama
- [ ] Verificar con `ollama --version`
- [ ] Descargar modelo: `ollama pull llama2`
- [ ] Probar: `ollama run llama2 "Hola"`
- [ ] Configurar en SlskDown (🤖 IA)
- [ ] Probar conexión
- [ ] ¡Disfrutar IA gratis!

---

**¡Listo! Ahora tienes IA profesional, gratis y privada en SlskDown.** 🚀🤖
