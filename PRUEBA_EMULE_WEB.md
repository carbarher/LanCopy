# 🧪 Guía de Prueba: eMule Web Client

## ✅ Estado Actual
- **Conexión**: ✅ Funcionando (localhost:4711)
- **Cliente**: `EMuleWebClient` implementado
- **Provider**: `EMuleSearchProvider` registrado
- **Búsquedas**: Listo para probar

## 🔍 Cómo Probar Búsquedas en eMule

### 1. **Verificar Conexión**
Deberías ver en los logs:
```
[eMule Web] Conectando a localhost:4711...
[eMule Web] ✅ Conectado exitosamente a la interfaz web de aMule
```

### 2. **Realizar una Búsqueda**
1. En el campo de búsqueda, escribe un término (ej: "asimov")
2. Haz clic en "Buscar"
3. Observa los logs para ver:
   ```
   [eMule Web] 🔍 Buscando: asimov
   [eMule Web] ✅ Encontrados X resultados
   ```

### 3. **Verificar Resultados**
Los resultados de eMule deberían aparecer en la lista con:
- **Columna "Red"**: Mostrará "eMule"
- **Columna "Usuario"**: Mostrará "eMule" (genérico)
- **Columna "Archivo"**: Nombre del archivo
- **Columna "Tamaño"**: Tamaño del archivo

### 4. **Descargar desde eMule**
1. Selecciona un resultado de eMule
2. Haz clic en "Descargar"
3. Observa los logs:
   ```
   [eMule Web] 📥 Iniciando descarga: nombre_archivo.ext
   [eMule Web] ✅ Descarga agregada a la cola de aMule
   ```

## 🐛 Posibles Problemas

### ❌ No aparecen resultados
**Causa**: aMule no tiene resultados para esa búsqueda
**Solución**: 
- Verifica que aMule esté conectado a servidores ed2k
- Prueba con términos de búsqueda más comunes
- Revisa la interfaz web de aMule en http://localhost:4711

### ❌ Error al parsear resultados
**Causa**: El formato HTML de aMule puede variar
**Solución**:
- Abre http://localhost:4711/search.html en el navegador
- Realiza una búsqueda manual
- Inspecciona el HTML para ver el formato real

### ❌ Error en descarga
**Causa**: Hash ed2k no disponible
**Solución**:
- Algunos resultados pueden no tener hash
- Verifica que el resultado tenga un enlace ed2k válido

## 🔧 Debugging

### Ver HTML de Búsqueda
Para depurar el parseo de resultados, puedes agregar logging temporal:

```csharp
// En EMuleWebClient.cs, método SearchAsync
var html = await response.Content.ReadAsStringAsync();
OnLog?.Invoke($"[DEBUG] HTML recibido: {html.Substring(0, Math.Min(500, html.Length))}...");
```

### Verificar URL de Búsqueda
La URL de búsqueda debería ser:
```
http://localhost:4711/search.html?query=TERMINO&type=global&password=TU_PASSWORD
```

### Probar en Navegador
1. Abre http://localhost:4711 en tu navegador
2. Inicia sesión con tu contraseña
3. Realiza una búsqueda manual
4. Verifica que aparezcan resultados

## 📊 Métricas Esperadas

### Búsqueda Exitosa
```
[eMule Web] 🔍 Buscando: asimov
[eMule Web] ✅ Encontrados 15 resultados
✅ Multi-red: 15 resultados totales
   📡 eMule: 15 resultados
```

### Búsqueda Sin Resultados
```
[eMule Web] 🔍 Buscando: xyzabc123
[eMule Web] ✅ Encontrados 0 resultados
⚠️ Multi-red: Sin resultados
```

## 🎯 Próximos Pasos

1. ✅ **Verificar búsquedas funcionan** ← Estamos aquí
2. ⏳ **Habilitar Soulseek** (para multi-red)
3. ⏳ **Probar búsquedas simultáneas** (Soulseek + eMule)
4. ⏳ **Verificar descargas** desde ambas redes

## 💡 Notas Importantes

- **Solo eMule activo**: Actualmente solo eMule está habilitado
- **Búsquedas multi-red**: El código ya está implementado
- **Necesitas ambas redes**: Para ver resultados de Soulseek y eMule juntos
- **Columna "Red"**: Identifica el origen de cada resultado

## 🚀 Comandos Útiles

### Reiniciar aMule
```bash
# Si aMule no responde
pkill amule
amule --full-daemon
```

### Ver Logs de aMule
```bash
tail -f ~/.aMule/logfile
```

### Verificar Puerto Web
```bash
netstat -an | grep 4711
```

---

**Estado**: ✅ Cliente eMule Web implementado y conectado
**Siguiente**: Probar búsqueda real en eMule
