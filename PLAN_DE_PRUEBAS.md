# 📋 Plan de Pruebas - Multi-Red

**Fecha**: 2 de diciembre de 2025  
**Versión**: 1.0

---

## ✅ Checklist de Pruebas

### Fase 1: Verificación Básica (5 min)
- [ ] SlskDown inicia sin errores
- [ ] UI se muestra correctamente
- [ ] Logs funcionan
- [ ] Conexión Soulseek funciona
- [ ] Búsqueda Soulseek funciona
- [ ] Descarga Soulseek funciona

**Si todo OK → Continuar a Fase 2**  
**Si hay problemas → Revertir a backup**

---

### Fase 2: Verificación eMule (10 min)
- [ ] aMule daemon está corriendo
- [ ] Puerto 4712 abierto
- [ ] Checkbox "Habilitar eMule" visible
- [ ] Activar checkbox
- [ ] Reiniciar SlskDown
- [ ] Logs muestran: "eMule habilitado"

**Si todo OK → Continuar a Fase 3**  
**Si hay problemas → Revisar instalación aMule**

---

### Fase 3: Búsqueda Multi-Red (10 min)
- [ ] Buscar: "machine learning"
- [ ] Logs muestran: "🌐 Búsqueda multi-red iniciada"
- [ ] Resultados de Soulseek aparecen
- [ ] Resultados de eMule aparecen
- [ ] Columna "Red" muestra origen correcto
- [ ] Deduplicación funciona (sin duplicados)

**Métricas a anotar**:
- Resultados Soulseek: ___
- Resultados eMule: ___
- Tiempo de búsqueda: ___
- Duplicados eliminados: ___

---

### Fase 4: Caché Inteligente (5 min)
- [ ] Buscar: "python tutorial" (primera vez)
- [ ] Anotar tiempo: ___ segundos
- [ ] Buscar: "python tutorial" (segunda vez)
- [ ] Anotar tiempo: ___ milisegundos
- [ ] Verificar mejora: ___x más rápido
- [ ] Logs muestran: "Resultados desde caché"

**Objetivo**: 20-50x más rápido en segunda búsqueda

---

### Fase 5: Descarga desde eMule (15 min)
- [ ] Buscar archivo pequeño (~1-5 MB)
- [ ] Seleccionar resultado con Red = "eMule"
- [ ] Iniciar descarga
- [ ] Logs muestran: "📥 Iniciando descarga desde eMule"
- [ ] Progreso se actualiza en tiempo real
- [ ] Velocidad se muestra (KB/s)
- [ ] Descarga completa exitosamente
- [ ] Archivo se guarda correctamente

**Métricas a anotar**:
- Tamaño archivo: ___
- Tiempo descarga: ___
- Velocidad promedio: ___
- Errores: ___

---

### Fase 6: Estabilidad (Opcional - 30 min)
- [ ] Realizar 10 búsquedas variadas
- [ ] Descargar 3-5 archivos
- [ ] Verificar uso de memoria
- [ ] Verificar uso de CPU
- [ ] Verificar logs por errores
- [ ] Verificar caché funciona consistentemente

---

## 📊 Resultados Esperados

### Búsquedas
- ✅ Primera búsqueda: 2-5 segundos
- ✅ Búsqueda desde caché: <100ms
- ✅ Resultados de ambas redes
- ✅ Sin duplicados

### Descargas
- ✅ Descarga Soulseek: Funciona como antes
- ✅ Descarga eMule: Muestra progreso
- ✅ Velocidad visible
- ✅ Sin errores

### Caché
- ✅ Hit rate: >30% después de 20 búsquedas
- ✅ Ahorro de tiempo: 20-50x
- ✅ Ahorro de ancho de banda: ~90%

---

## ⚠️ Problemas Comunes

### Problema 1: eMule no conecta
**Solución**:
1. Verificar aMule daemon corriendo
2. Verificar puerto 4712
3. Verificar contraseña EC

### Problema 2: Sin resultados eMule
**Solución**:
1. Esperar 30 segundos (red eMule es más lenta)
2. Verificar logs de aMule
3. Probar búsqueda diferente

### Problema 3: Caché no funciona
**Solución**:
1. Verificar logs
2. Buscar mismo término exacto
3. Esperar <30 minutos desde primera búsqueda

---

## 📝 Notas de Prueba

### Fecha: ___________
### Hora inicio: ___________
### Hora fin: ___________

### Observaciones:
```
[Anotar aquí cualquier observación, error o comportamiento inesperado]




```

### Resultados Generales:
- [ ] Todo funciona perfectamente
- [ ] Funciona con problemas menores
- [ ] Requiere ajustes
- [ ] No funciona

### Próximos Pasos:
```
[Anotar qué hacer después de las pruebas]




```

---

## ✅ Conclusión

**Estado final**: ___________

**Recomendación**: ___________

**Fecha próxima revisión**: ___________
