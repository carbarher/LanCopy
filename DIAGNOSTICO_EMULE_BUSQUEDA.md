# 🔍 Diagnóstico: Búsqueda eMule - Primera Prueba

## 📊 Resultados de la Primera Prueba

### ✅ Lo que Funcionó:
1. **Conexión exitosa**: aMule Web respondió
2. **Búsqueda iniciada**: Query "asimov" enviada correctamente
3. **HTTP 200**: Respuesta exitosa del servidor

### ⚠️ Problema Detectado:

```
[13:33:54] [eMule Web] 🔍 Buscando: asimov
[13:33:56] [eMule Web] 📄 HTML recibido (899 bytes)
[13:33:56] [eMule Web] 📋 Preview: �
```

**Síntomas**:
- HTML recibido: Solo 899 bytes (muy poco para resultados)
- Preview corrupto: Carácter `�` indica problema de encoding
- No se mostraron logs de parseo (filas encontradas, resultados)

## 🔬 Análisis del Problema

### Posibles Causas:

1. **Encoding Incorrecto**:
   - aMule puede estar devolviendo ISO-8859-1 en lugar de UTF-8
   - El preview se corrompe al intentar decodificar

2. **HTML Vacío o Error**:
   - 899 bytes es muy poco para una página de resultados
   - Podría ser una página de error o login

3. **Autenticación Fallida**:
   - La contraseña puede estar incorrecta
   - aMule puede requerir login previo

## 🔧 Mejoras Implementadas

### 1. **Detección Automática de Encoding**
```csharp
// Leer bytes raw
var bytes = await response.Content.ReadAsByteArrayAsync();

// Intentar UTF-8 primero
var html = System.Text.Encoding.UTF8.GetString(bytes);

// Si está corrupto, usar ISO-8859-1
if (html.Contains("�") || html.Length < 100)
{
    html = System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
    OnLog?.Invoke($"[eMule Web] 🔄 Usando encoding ISO-8859-1");
}
```

### 2. **Logging Mejorado**
- Mostrar bytes raw recibidos
- Mostrar caracteres decodificados
- Preview de 1000 caracteres (antes 500)
- Log completo del HTML en lugar de preview truncado

### 3. **Logs Esperados Ahora**
```
[eMule Web] 🔍 Buscando: asimov
[eMule Web] 📄 HTML recibido (899 bytes raw)
[eMule Web] 📄 HTML decodificado (899 caracteres)
[eMule Web] 📋 Preview HTML:
[... HTML completo hasta 1000 caracteres ...]
[eMule Web] 🔍 Encontradas X filas HTML
[eMule Web] ✅ Encontrados X resultados
```

## 🧪 Segunda Prueba - Instrucciones

### 1. Reinicia SlskDown
```bash
# Cierra y ejecuta la nueva versión
```

### 2. Realiza la Misma Búsqueda
- Término: "asimov"
- Observa los logs

### 3. Analiza el Nuevo Output

#### ✅ Si ves HTML válido:
```html
<!DOCTYPE html>
<html>
<head>...</head>
<body>
  <table>
    <tr><td>archivo.mp3</td><td>5 MB</td>...</tr>
  </table>
</body>
</html>
```
→ **Acción**: Ajustar regex de parseo según formato real

#### ⚠️ Si ves página de login:
```html
<form action="/login">
  <input type="password" name="password">
</form>
```
→ **Acción**: Necesitamos hacer login primero

#### ❌ Si ves mensaje de error:
```html
<div class="error">Invalid password</div>
```
→ **Acción**: Verificar contraseña en configuración

## 📋 Checklist de Verificación

Antes de la segunda prueba, verifica:

- [ ] aMule está corriendo
- [ ] Web Server habilitado en aMule (Preferencias → Web Server)
- [ ] Puerto 4711 correcto
- [ ] Contraseña correcta en SlskDown
- [ ] Puedes acceder a http://localhost:4711 en navegador

## 🎯 Próximos Pasos Según Resultado

### Escenario A: HTML Válido con Resultados
1. Ajustar regex de parseo
2. Extraer datos de archivos
3. Probar descarga

### Escenario B: Página de Login
1. Implementar login HTTP
2. Guardar cookie de sesión
3. Reintentar búsqueda

### Escenario C: Error de Autenticación
1. Verificar contraseña
2. Verificar configuración de aMule
3. Probar login manual en navegador

## 💡 Información Adicional

### URL de Búsqueda Usada:
```
http://localhost:4711/search.html?query=asimov&type=global&password=TU_PASSWORD
```

### Formato Esperado de aMule:
aMule Web Interface puede tener diferentes formatos según la versión:
- **aMule 2.3.x**: Tabla HTML simple
- **aMule 2.4.x**: Tabla con clases CSS
- **amuleweb-dlp**: Formato JSON (más moderno)

### Comandos Útiles:

```bash
# Ver configuración de aMule
cat ~/.aMule/amule.conf | grep -i web

# Probar búsqueda manual
curl "http://localhost:4711/search.html?query=test&password=TU_PASSWORD"

# Ver logs de aMule
tail -f ~/.aMule/logfile
```

---

**Estado**: ✅ Mejoras implementadas, esperando segunda prueba
**Objetivo**: Ver HTML completo para diagnosticar formato real
