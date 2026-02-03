# 🔐 Guía de Instalación y Uso de VPN en SlskDown

## ¿Por qué usar VPN?

Cuando el servidor de Soulseek detecta demasiados intentos de conexión desde la misma IP, puede bloquearla temporalmente. La VPN permite:

- ✅ Cambiar tu IP automáticamente cuando hay bloqueo
- ✅ Rotar entre múltiples servidores
- ✅ Continuar usando Soulseek sin esperar horas

---

## 📦 Instalación Rápida

### Opción 1: Script Automático (Recomendado)

1. **Ejecuta**: `INSTALAR_PROTONVPN.bat`
2. El script verificará Python e instalará ProtonVPN CLI
3. Sigue las instrucciones en pantalla

### Opción 2: Manual

1. **Instalar Python** (si no lo tienes):
   ```
   https://www.python.org/downloads/
   ```
   ⚠️ **IMPORTANTE**: Durante la instalación, marca "Add Python to PATH"

2. **Instalar ProtonVPN CLI**:
   ```bash
   pip install protonvpn-cli
   ```

3. **Verificar instalación**:
   ```bash
   protonvpn --version
   ```
   o
   ```bash
   protonvpn-cli --version
   ```

---

## ⚙️ Configuración

### 1. Crear Cuenta ProtonVPN (si no tienes)

- Ve a: https://account.protonvpn.com/signup
- Plan **GRATUITO** disponible (suficiente para SlskDown)
- Servidores gratuitos: USA, Países Bajos, Japón

### 2. Configurar ProtonVPN CLI

Ejecuta uno de estos comandos:
```bash
protonvpn init
```
o
```bash
protonvpn-cli init
```

Te pedirá:
- **Username**: Tu usuario de ProtonVPN
- **Password**: Tu contraseña de ProtonVPN
- **Protocol**: Selecciona `OpenVPN` (recomendado)

### 3. Probar Conexión

Ejecuta: `PROBAR_VPN.bat`

Este script:
- Verifica la instalación
- Muestra tu IP actual
- Prueba conectar a VPN
- Muestra tu nueva IP

---

## 🚀 Uso en SlskDown

### Activar VPN Automática

1. Abre SlskDown
2. Ve a **Configuración**
3. Marca: **"🔐 Usar VPN automáticamente si el servidor bloquea la IP"**

### ¿Cómo Funciona?

1. SlskDown intenta conectar a Soulseek
2. Si hay **3 timeouts consecutivos** con TCP funcionando → **Bloqueo detectado**
3. SlskDown conecta VPN automáticamente
4. Reintenta conexión con nueva IP
5. Si falla nuevamente, rota a otro servidor VPN

### Control Manual

- **Botón "🔐 VPN"** en la barra superior
- Click para conectar/desconectar manualmente
- Muestra el servidor actual cuando está conectado

---

## 🔧 Comandos Útiles

### Ver Estado
```bash
protonvpn status
```

### Conectar a Servidor Más Rápido
```bash
protonvpn connect --fastest
```

### Conectar a Servidor Específico
```bash
protonvpn connect US-FREE#1
```

### Desconectar
```bash
protonvpn disconnect
```

### Ver Servidores Disponibles
```bash
protonvpn list
```

---

## 📊 Servidores Gratuitos

SlskDown rota automáticamente entre estos servidores:

| Servidor | País | Velocidad |
|----------|------|-----------|
| US-FREE#1 | USA | Media |
| US-FREE#2 | USA | Media |
| US-FREE#3 | USA | Media |
| NL-FREE#1 | Países Bajos | Media |
| NL-FREE#2 | Países Bajos | Media |
| JP-FREE#1 | Japón | Media |
| JP-FREE#2 | Japón | Media |

**Nota**: Plan gratuito tiene velocidad limitada pero es suficiente para Soulseek.

---

## ❓ Solución de Problemas

### "ProtonVPN CLI no está instalado"

**Causa**: Python no está instalado o no está en PATH

**Solución**:
1. Instala Python desde: https://www.python.org/downloads/
2. Durante instalación, marca "Add Python to PATH"
3. Reinicia la terminal
4. Ejecuta: `pip install protonvpn-cli`

### "Python no está instalado"

**Solución**:
1. Descarga Python 3.x
2. Durante instalación: ✅ "Add Python to PATH"
3. Reinicia el sistema
4. Verifica: `python --version`

### "Error conectando VPN"

**Posibles causas**:
- No has ejecutado `protonvpn init`
- Credenciales incorrectas
- Servidor no disponible

**Solución**:
1. Ejecuta: `protonvpn init`
2. Ingresa credenciales correctas
3. Prueba con: `PROBAR_VPN.bat`

### "VPN conectada pero Soulseek no funciona"

**Solución**:
1. Espera 5-10 segundos después de conectar VPN
2. Desconecta y reconecta Soulseek
3. Si persiste, rota a otro servidor VPN

---

## 🆚 VPN vs Scripts de Cambio de IP

### VPN (ProtonVPN)
- ✅ Cambio de IP instantáneo
- ✅ Múltiples servidores disponibles
- ✅ Automático
- ❌ Requiere instalación
- ❌ Velocidad limitada (plan gratuito)

### Scripts (CAMBIAR_IP.bat)
- ✅ No requiere instalación
- ✅ Velocidad completa
- ❌ Solo funciona si tienes IP dinámica
- ❌ Requiere reiniciar router/adaptador
- ❌ Puede tardar minutos

**Recomendación**: Usa VPN si tienes bloqueos frecuentes. Usa scripts si solo necesitas cambiar IP ocasionalmente.

---

## 📝 Logs y Diagnóstico

SlskDown muestra mensajes en el log cuando usa VPN:

```
🚨 BLOQUEO DETECTADO: 3 timeouts consecutivos con TCP funcionando
🔐 Activando VPN para cambiar IP...
✅ ProtonVPN CLI encontrado: protonvpn
🔐 Conectando a VPN: US-FREE#1...
✅ VPN conectada: US-FREE#1
✅ VPN conectada - Reintentando conexión con nueva IP...
```

---

## 🔒 Privacidad y Seguridad

- ProtonVPN es una empresa suiza con políticas estrictas de privacidad
- No guarda logs de actividad
- Código abierto
- Plan gratuito sin tarjeta de crédito

**Más info**: https://protonvpn.com/privacy-policy

---

## 📞 Soporte

### ProtonVPN
- Documentación: https://protonvpn.com/support/
- GitHub: https://github.com/ProtonVPN/linux-cli

### SlskDown
- Revisa los logs en la aplicación
- Ejecuta `PROBAR_VPN.bat` para diagnóstico
- Verifica que Python esté en PATH

---

## 🎯 Resumen Rápido

1. **Instalar**: Ejecuta `INSTALAR_PROTONVPN.bat`
2. **Configurar**: Ejecuta `protonvpn init` con tus credenciales
3. **Probar**: Ejecuta `PROBAR_VPN.bat`
4. **Activar en SlskDown**: Marca el checkbox en Configuración
5. **¡Listo!**: VPN se activará automáticamente cuando haya bloqueo

---

**Última actualización**: Noviembre 2024
