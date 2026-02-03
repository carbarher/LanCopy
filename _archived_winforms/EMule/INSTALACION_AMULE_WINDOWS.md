# 🔧 Instalación aMule en Windows

**Fecha**: 2 de diciembre de 2025  
**Sistema**: Windows 10/11

---

## 📥 Opción 1: Instalación Oficial (Recomendado)

### Paso 1: Descargar aMule

**Sitio oficial**: https://www.amule.org/

**Descargar**:
- aMule 2.3.3 para Windows
- O versión más reciente disponible

**Alternativa**: https://sourceforge.net/projects/amule/

---

### Paso 2: Instalar

1. Ejecutar instalador descargado
2. Seguir wizard de instalación
3. Seleccionar componentes:
   - ✅ aMule (aplicación principal)
   - ✅ aMule Daemon (servidor)
   - ✅ aMule Remote GUI (opcional)

4. Completar instalación

---

### Paso 3: Configurar aMule

#### Iniciar aMule por primera vez
```
Inicio → aMule → aMule
```

#### Configurar External Connections (EC)

1. En aMule, ir a: **Preferencias** → **Conexiones Remotas**

2. Configurar:
   ```
   ✅ Aceptar conexiones externas
   Puerto EC: 4712
   Contraseña EC: [tu_contraseña]
   ```

3. **Importante**: Anotar la contraseña (la necesitarás para SlskDown)

4. Aplicar y cerrar aMule

---

### Paso 4: Iniciar aMule Daemon

#### Opción A: Desde línea de comandos
```cmd
cd "C:\Program Files\aMule"
amuled.exe
```

#### Opción B: Crear acceso directo
1. Crear archivo `start_amuled.bat`:
```batch
@echo off
echo Iniciando aMule Daemon...
cd "C:\Program Files\aMule"
start amuled.exe
echo aMule Daemon iniciado en puerto 4712
pause
```

2. Ejecutar `start_amuled.bat`

---

## 📥 Opción 2: Usando WSL (Windows Subsystem for Linux)

Si tienes WSL instalado:

```bash
# En terminal WSL
sudo apt-get update
sudo apt-get install amule-daemon

# Configurar
mkdir -p ~/.aMule
nano ~/.aMule/amule.conf
```

**Agregar en amule.conf**:
```ini
[ExternalConnect]
AcceptExternalConnections=1
ECPassword=<contraseña_md5>
ECPort=4712
```

**Generar password MD5**:
```bash
echo -n "tu_contraseña" | md5sum
```

**Iniciar daemon**:
```bash
amuled -f
```

---

## ✅ Verificar Instalación

### Verificar que aMule está corriendo

**Opción 1: Task Manager**
```
Ctrl + Shift + Esc
Buscar: amuled.exe
Estado: Running ✅
```

**Opción 2: Netstat**
```cmd
netstat -ano | findstr :4712
```

**Esperado**:
```
TCP    0.0.0.0:4712    0.0.0.0:0    LISTENING    [PID]
```

---

## 🔧 Configuración para SlskDown

### Archivo de configuración

**Ubicación**: `C:\Users\[usuario]\.aMule\amule.conf`

**Contenido mínimo**:
```ini
[ExternalConnect]
AcceptExternalConnections=1
ECPassword=<tu_contraseña_md5>
ECPort=4712

[eMule]
Nick=SlskDown-User
MaxUpload=100
MaxDownload=0

[Server]
AutoConnect=1
```

### Generar Password MD5 en Windows

**PowerShell**:
```powershell
$password = "tu_contraseña"
$md5 = New-Object -TypeName System.Security.Cryptography.MD5CryptoServiceProvider
$utf8 = New-Object -TypeName System.Text.UTF8Encoding
$hash = [System.BitConverter]::ToString($md5.ComputeHash($utf8.GetBytes($password)))
$hash.Replace("-","").ToLower()
```

---

## 🧪 Probar Conexión

### Desde SlskDown

1. Abrir SlskDown
2. Ir a **Configuración**
3. Activar **"🔷 Habilitar eMule/ed2k"**
4. Reiniciar SlskDown
5. Verificar logs:
   ```
   ✅ eMule conectado
   📡 Puerto EC: 4712
   ```

### Desde aMule Remote GUI (Opcional)

1. Abrir aMule Remote GUI
2. Conectar a: `localhost:4712`
3. Ingresar contraseña EC
4. **Esperado**: Conexión exitosa ✅

---

## ⚠️ Solución de Problemas

### Problema 1: Puerto 4712 ya en uso

**Solución**:
```cmd
# Ver qué está usando el puerto
netstat -ano | findstr :4712

# Cambiar puerto en amule.conf
ECPort=4713
```

### Problema 2: aMule no inicia

**Verificar**:
1. aMule está instalado correctamente
2. No hay otra instancia corriendo
3. Permisos de usuario correctos

**Solución**:
```cmd
# Terminar procesos existentes
taskkill /F /IM amuled.exe

# Reiniciar
amuled.exe
```

### Problema 3: Contraseña incorrecta

**Síntoma**: SlskDown no puede conectar

**Solución**:
1. Regenerar password MD5
2. Actualizar en `amule.conf`
3. Reiniciar aMule daemon
4. Usar misma contraseña en SlskDown

### Problema 4: Firewall bloquea conexión

**Solución**:
```
Windows Defender Firewall
→ Configuración avanzada
→ Reglas de entrada
→ Nueva regla
→ Puerto TCP 4712
→ Permitir conexión
```

---

## 📊 Verificación Final

### Checklist

- [ ] aMule instalado
- [ ] aMule daemon corriendo
- [ ] Puerto 4712 abierto
- [ ] Contraseña EC configurada
- [ ] amule.conf correcto
- [ ] SlskDown puede conectar
- [ ] Logs muestran conexión exitosa

### Comando de Verificación

```cmd
# Verificar proceso
tasklist | findstr amule

# Verificar puerto
netstat -ano | findstr :4712

# Verificar archivo config
type "%USERPROFILE%\.aMule\amule.conf"
```

---

## 🚀 Siguiente Paso

Una vez aMule esté instalado y corriendo:

1. ✅ Abrir SlskDown
2. ✅ Ir a Configuración
3. ✅ Activar eMule
4. ✅ Reiniciar
5. ✅ Probar búsqueda multi-red

---

## 📚 Referencias

- **aMule Official**: https://www.amule.org/
- **aMule Wiki**: https://wiki.amule.org/
- **EC Protocol**: https://wiki.amule.org/wiki/EC_Protocol_HOWTO
- **Guía SlskDown**: `GUIA_USUARIO_MULTI_RED.md`

---

**¿Necesitas ayuda?** Consulta `GUIA_USUARIO_MULTI_RED.md` sección "Solución de Problemas"
