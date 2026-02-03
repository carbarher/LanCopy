# ⚠️ Archivos Obsoletos - Pendientes de Eliminar

Los siguientes archivos son de la implementación anterior con OpenAI y deben ser eliminados manualmente:

## 📁 Archivos a Eliminar

### **Core/AI/**
- ❌ `OpenAIClient.cs` - Reemplazado por `OllamaClient.cs`
- ❌ `CoverGenerator.cs` - Funcionalidad eliminada (requería DALL-E)
- ❌ `ChatGPTAssistant.cs` - Reemplazado por `SlskDownAssistant.cs`

### **Raíz del proyecto**
- ❌ `AIIntegration.cs` - Reemplazado por `MainForm.AIIntegration.cs`

---

## ✅ Archivos Nuevos (Ollama)

### **Core/AI/**
- ✅ `OllamaClient.cs` - Cliente para modelos locales
- ✅ `IntelligentSearchEngine.cs` - Actualizado a Ollama
- ✅ `AIRecommendationEngine.cs` - Actualizado a Ollama
- ✅ `AIFileTagger.cs` - Actualizado a Ollama
- ✅ `AIQualityPredictor.cs` - Actualizado a Ollama
- ✅ `SlskDownAssistant.cs` - Actualizado a Ollama
- ✅ `AvailabilityPredictor.cs` - Actualizado a Ollama
- ✅ `BookSummarizer.cs` - Actualizado a Ollama
- ✅ `MalwareDetector.cs` - Actualizado a Ollama

### **UI**
- ✅ `MainForm.AIIntegration.cs` - UI actualizada para Ollama

### **Documentación**
- ✅ `README_OLLAMA.md` - Guía completa de Ollama
- ✅ `README_IA_V2.6.md` - Resumen actualizado
- ✅ `FUNCIONALIDADES_IA_V2.6_OLLAMA.md` - Documentación técnica completa

---

## 🔧 Cómo Eliminar

### **Opción 1: Manualmente**
1. Navegar a `c:\p2p\SlskDown\Core\AI\`
2. Eliminar: `OpenAIClient.cs`, `CoverGenerator.cs`, `ChatGPTAssistant.cs`
3. Navegar a `c:\p2p\SlskDown\`
4. Eliminar: `AIIntegration.cs`

### **Opción 2: PowerShell**
```powershell
cd c:\p2p\SlskDown
Remove-Item Core\AI\OpenAIClient.cs -Force
Remove-Item Core\AI\CoverGenerator.cs -Force
Remove-Item Core\AI\ChatGPTAssistant.cs -Force
Remove-Item AIIntegration.cs -Force
```

### **Opción 3: Git**
```bash
cd c:\p2p\SlskDown
git rm Core/AI/OpenAIClient.cs
git rm Core/AI/CoverGenerator.cs
git rm Core/AI/ChatGPTAssistant.cs
git rm AIIntegration.cs
git commit -m "remove: Eliminar archivos obsoletos de OpenAI"
```

---

## ⚠️ Nota Importante

Estos archivos **NO** afectan la funcionalidad actual de SlskDown v2.6 con Ollama. Son simplemente archivos antiguos que quedaron en el proyecto y pueden eliminarse de forma segura.

La aplicación funciona completamente con los archivos nuevos basados en Ollama.

---

**Fecha**: 5 de enero de 2026
**Versión**: SlskDown v2.6 - Ollama Edition
