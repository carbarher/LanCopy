# 🚀 Guía: Instalación aMule y Ejecución de Tests

**Fecha**: 2 de diciembre de 2025, 12:45 PM  
**Tareas**: B (Instalar aMule) + D (Ejecutar Tests)

---

## 📋 Resumen de Acciones

### ✅ Archivos Creados
1. `EMule/INSTALACION_AMULE_WINDOWS.md` - Guía completa de instalación
2. `run_tests.bat` - Script para ejecutar tests

---

## 🔧 Parte B: Instalar aMule

### Opción 1: Instalación Rápida (Windows)

#### Paso 1: Descargar
```
Sitio: https://www.amule.org/
Archivo: aMule-2.3.3-win32.exe (o versión más reciente)
```

#### Paso 2: Instalar
1. Ejecutar instalador
2. Seleccionar componentes:
   - ✅ aMule
   - ✅ aMule Daemon
3. Completar instalación

#### Paso 3: Configurar
1. Abrir aMule
2. Ir a: **Preferencias** → **Conexiones Remotas**
3. Configurar:
   ```
   ✅ Aceptar conexiones externas
   Puerto EC: 4712
   Contraseña: [tu_contraseña]
   ```
4. Aplicar y cerrar

#### Paso 4: Iniciar Daemon
```cmd
cd "C:\Program Files\aMule"
amuled.exe
```

O crear `start_amuled.bat`:
```batch
@echo off
cd "C:\Program Files\aMule"
start amuled.exe
echo aMule Daemon iniciado
pause
```

---

### Opción 2: Usando WSL (Alternativa)

```bash
# Instalar
sudo apt-get install amule-daemon

# Configurar
mkdir -p ~/.aMule
nano ~/.aMule/amule.conf
```

**Contenido de amule.conf**:
```ini
[ExternalConnect]
AcceptExternalConnections=1
ECPassword=<tu_password_md5>
ECPort=4712
```

**Generar password MD5**:
```bash
echo -n "tu_contraseña" | md5sum
```

**Iniciar**:
```bash
amuled -f
```

---

### ✅ Verificar Instalación

#### Verificar proceso corriendo
```cmd
tasklist | findstr amule
```

**Esperado**: `amuled.exe` en la lista

#### Verificar puerto abierto
```cmd
netstat -ano | findstr :4712
```

**Esperado**: `LISTENING` en puerto 4712

---

## 🧪 Parte D: Ejecutar Tests

### Tests Disponibles

#### 1. EMuleClientTests
**Ubicación**: `EMule/Tests/EMuleClientTests.cs`

**Tests incluidos**:
- ✅ Test de conexión
- ✅ Test de autenticación
- ✅ Test de búsqueda básica

**Ejecutar**:
```cmd
cd c:\p2p\SlskDown
dotnet run --project EMule\Tests\EMuleClientTests.cs
```

---

#### 2. EMuleDownloadTests
**Ubicación**: `EMule/Tests/EMuleDownloadTests.cs`

**Tests incluidos**:
- ✅ Test de iniciación de descarga
- ✅ Test de monitoreo de progreso
- ✅ Test de cancelación de descarga

**Ejecutar**:
```cmd
cd c:\p2p\SlskDown
dotnet run --project EMule\Tests\EMuleDownloadTests.cs
```

---

### Script Automático

**Ejecutar todos los tests**:
```cmd
cd c:\p2p\SlskDown
run_tests.bat
```

**Qué hace el script**:
1. Compila los tests
2. Verifica que existen los archivos
3. Muestra instrucciones para ejecutar

---

## 📊 Resultados Esperados

### Test de Conexión ✅
```
📡 Test 1: Conexión a aMule daemon
----------------------------------
Conectando a localhost:4712...
   Estado: Disconnected → Connecting
   Estado: Connecting → Connected
✅ Conexión exitosa
   Red: eMule/ed2k
   Estado: Connected
   Uptime: 0s
✅ Desconexión exitosa
```

### Test de Autenticación ✅
```
🔐 Test 2: Autenticación EC
---------------------------
Autenticando...
   Estado: Connecting → LoggedIn
✅ Autenticación exitosa
   Estado final: LoggedIn
```

### Test de Búsqueda ✅
```
🔍 Test 3: Búsqueda básica
--------------------------
Buscando 'machine learning'...
   📄 Machine Learning.pdf (2.5 MB)
   📄 ML Tutorial.epub (1.2 MB)
   📄 Python ML Guide.pdf (3.8 MB)

   Búsqueda Completed: 3 resultados en 5.2s
✅ Búsqueda completada: 3 resultados
```

---

## ⚠️ Solución de Problemas

### Problema 1: aMule no está corriendo

**Síntoma**:
```
❌ Error: No se pudo conectar
   Timeout esperando respuesta
```

**Solución**:
1. Verificar que aMule daemon está corriendo
2. Iniciar con: `amuled.exe`
3. Verificar puerto: `netstat -ano | findstr :4712`

---

### Problema 2: Contraseña incorrecta

**Síntoma**:
```
❌ Autenticación rechazada
   Verifica la contraseña EC en amule.conf
```

**Solución**:
1. Regenerar password MD5
2. Actualizar en `amule.conf`
3. Reiniciar aMule daemon

---

### Problema 3: Puerto bloqueado

**Síntoma**:
```
❌ Error: Puerto 4712 ya en uso
```

**Solución**:
```cmd
# Ver qué está usando el puerto
netstat -ano | findstr :4712

# Terminar proceso
taskkill /F /PID [PID]

# O cambiar puerto en amule.conf
ECPort=4713
```

---

### Problema 4: Tests no compilan

**Síntoma**:
```
❌ Error compilando tests
```

**Solución**:
1. Verificar que .NET SDK está instalado
2. Verificar que archivos de test existen
3. Compilar proyecto principal primero:
   ```cmd
   dotnet build SlskDown.csproj
   ```

---

## 📋 Checklist Completo

### Instalación aMule
- [ ] aMule descargado
- [ ] aMule instalado
- [ ] Daemon configurado
- [ ] Puerto EC: 4712
- [ ] Contraseña EC configurada
- [ ] Daemon corriendo
- [ ] Puerto verificado abierto

### Tests
- [ ] Tests compilados
- [ ] Test de conexión ejecutado
- [ ] Test de autenticación ejecutado
- [ ] Test de búsqueda ejecutado
- [ ] Tests de descarga ejecutados
- [ ] Todos los tests pasan ✅

---

## 🎯 Siguiente Paso

Una vez completado B y D:

### Probar Integración Completa

1. **Abrir SlskDown**
   ```cmd
   cd c:\p2p\SlskDown\bin\Release\net8.0-windows
   SlskDown.exe
   ```

2. **Habilitar eMule**
   - Ir a Configuración
   - Activar "🔷 Habilitar eMule/ed2k"
   - Reiniciar SlskDown

3. **Probar Búsqueda Multi-Red**
   - Conectar a Soulseek
   - Buscar: "machine learning"
   - **Esperado**: Resultados de Soulseek Y eMule

4. **Verificar Logs**
   ```
   🌐 Búsqueda multi-red iniciada en 2 redes
   ✅ Multi-red: 45 resultados totales
      📡 Soulseek: 30 resultados
      📡 eMule: 15 resultados
   ```

---

## 📚 Documentación Relacionada

1. **Instalación detallada**: `EMule/INSTALACION_AMULE_WINDOWS.md`
2. **Guía de usuario**: `GUIA_USUARIO_MULTI_RED.md`
3. **Tests de integración**: `EMule/TESTING_README.md`
4. **Solución de problemas**: `GUIA_USUARIO_MULTI_RED.md` (sección troubleshooting)

---

## ✅ Resumen

### Tiempo Estimado
- **Instalación aMule**: 15 minutos
- **Ejecución de tests**: 5 minutos
- **Total**: 20 minutos

### Resultado Esperado
- ✅ aMule daemon corriendo
- ✅ Puerto 4712 abierto
- ✅ Tests pasando
- ✅ Listo para integración con SlskDown

---

## 🚀 Comandos Rápidos

```cmd
# Instalar aMule (manual desde web)
# https://www.amule.org/

# Iniciar daemon
cd "C:\Program Files\aMule"
amuled.exe

# Verificar
netstat -ano | findstr :4712

# Ejecutar tests
cd c:\p2p\SlskDown
run_tests.bat

# Ejecutar SlskDown
cd bin\Release\net8.0-windows
SlskDown.exe
```

---

**¡Listo para empezar!** 🎉

Sigue esta guía paso a paso y tendrás aMule funcionando con tests verificados en ~20 minutos.
