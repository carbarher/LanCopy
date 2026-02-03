# INSTRUCCIONES PARA INCORPORAR LIBROS PRE-1900

## ⚠️ PROBLEMA DETECTADO
Los comandos automáticos no funcionan correctamente en este entorno. 
Necesitas ejecutar estos comandos manualmente en tu terminal.

---

## 📋 PASOS A SEGUIR

### 1. Crear carpeta de destino
```cmd
mkdir "c:\p2p\biblioteca_pre1900"
```

### 2. Copiar Carpeta 1 (849 Libros Clásicos) - COMPLETA
**Razón**: 95%+ son pre-1900 verificados

```cmd
xcopy "c:\p2p\emule\849 Libros Clasicos En Español De La Literatura Universal - Pdf -- Muy Bien" "c:\p2p\biblioteca_pre1900\Clasicos_849" /E /I /Y /H
```

**Resultado esperado**: ~952 archivos copiados

---

### 3. Copiar Carpeta 2 (2600 Libros) - COMPLETA (filtraremos después)
**Razón**: ~45% son pre-1900, pero es más rápido copiar todo y filtrar después

```cmd
xcopy "c:\p2p\emule\_2600 Libros Literatura Universal En EspaÃ±ol Por Morgan" "c:\p2p\biblioteca_pre1900\Universal_2600" /E /I /Y /H
```

**Resultado esperado**: ~2601 archivos copiados

---

### 4. Ejecutar script de análisis para identificar post-1900 en Carpeta 2
```cmd
cd c:\p2p
python scripts\analyze_audio_libros_pre1900.py
```

Esto generará un CSV con la clasificación de cada libro.

---

### 5. Revisar el CSV generado
Abre el archivo `analisis_completo_pre1900.csv` y filtra por:
- `is_pre1900 = False`

Esos son los libros que debes eliminar de `c:\p2p\biblioteca_pre1900\Universal_2600`

---

## 📊 RESUMEN ESPERADO

| Carpeta | Archivos | Pre-1900 | Acción |
|---------|----------|----------|--------|
| Clasicos_849 | ~952 | 95%+ | ✅ Mantener todos |
| Universal_2600 | ~2601 | ~45% | ⚠️ Filtrar ~1400 post-1900 |

**Total final esperado**: ~2100-2200 libros pre-1900

---

## 🔧 ALTERNATIVA RÁPIDA (Si los comandos fallan)

Copia manualmente las carpetas usando el Explorador de Windows:

1. Abre `c:\p2p\emule`
2. Copia la carpeta "849 Libros Clasicos..." a `c:\p2p\biblioteca_pre1900\`
3. Copia la carpeta "_2600 Libros..." a `c:\p2p\biblioteca_pre1900\`
4. Renombra las carpetas copiadas a nombres más cortos

---

## ✅ VERIFICACIÓN

Después de copiar, ejecuta:
```cmd
dir "c:\p2p\biblioteca_pre1900" /s /b | find /c ":"
```

Esto te dirá cuántos archivos se copiaron en total.

---

## 📝 NOTAS

- Los scripts Python están listos en `c:\p2p\scripts\`
- El análisis completo está en `c:\p2p\analisis_audio_libros_pre1900.md`
- Las listas de referencia pre-1900 están en:
  - `c:\p2p\novelas_pre1900_gutenberg_anylang.txt`
  - `c:\p2p\novelas_1000_pre1900_mix_es_titles.txt`

---

**Fecha**: 20 de diciembre de 2025
**Estado**: Pendiente de ejecución manual por limitaciones del entorno
