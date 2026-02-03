# Troubleshooting: Problemas de Conexión a Soulseek

## 🔍 Diagnóstico Mejorado

La nueva versión incluye diagnósticos detallados que te ayudarán a identificar el problema:

### Logs de Diagnóstico

Ahora verás en el log:

```
🔐 Intentando conectar como: [usuario]
   Usuario: X caracteres
   Password: Y caracteres
🌐 Verificando conectividad con vps.slsknet.org:2271...
✅ Servidor alcanzable
🔧 Cliente creado, iniciando conexión...
⏳ Intentando autenticar con el servidor...
```

Si hay un error, verás:
```
❌ Error durante ConnectAsync: [TipoDeError]
   Mensaje: [Descripción del error]
   Inner: [Error interno si existe]
```

## 🚨 Errores Comunes y Soluciones

### 1. "Remote connection closed" / "Se ha forzado la interrupción"

**Causas:**
- ❌ Credenciales incorrectas (usuario o contraseña)
- ⚠️ Cuenta suspendida o inactiva
- 🔒 IP bloqueada temporalmente por demasiados intentos
- 🌐 Servidor rechazando la conexión

**Soluciones:**
1. **Verifica las credenciales:**
   ```cmd
   notepad C:\Users\[TuUsuario]\AppData\Roaming\SlskDown\config.json
   ```
   Busca las líneas:
   ```json
   "username": "tu_usuario",
   "password": "tu_contraseña"
   ```

2. **Prueba con el cliente oficial:**
   - Descarga SoulseekQt desde https://www.slsknet.org/
   - Intenta conectarte con las mismas credenciales
   - Si falla, el problema es con la cuenta, no con SlskDown

3. **Espera antes de reintentar:**
   - Si has intentado conectar muchas veces, espera 15-30 minutos
   - El servidor puede haber bloqueado temporalmente tu IP

4. **Verifica el firewall:**
   - Windows Defender Firewall
   - Antivirus (Avast, Norton, etc.)
   - Firewall del router

### 2. "TimeoutException: The wait timed out"

**Causas:**
- 🌐 Problemas de red
- 🔥 Firewall bloqueando la conexión
- 📡 Servidor lento o sobrecargado

**Soluciones:**
1. Verifica tu conexión a Internet
2. Desactiva temporalmente el firewall/antivirus para probar
3. Intenta desde otra red (datos móviles, otra WiFi)

### 3. "Usuario vacío" / "Contraseña vacía"

**Causa:**
- ⚙️ Archivo config.json corrupto o vacío

**Solución:**
1. Ve a la pestaña **Config** en SlskDown
2. Ingresa tu usuario y contraseña
3. Haz clic en **Guardar Config**
4. Intenta reconectar

### 4. "⚠️ Error de conectividad" / "Servidor no alcanzable"

**Causas:**
- 🌐 Sin conexión a Internet
- 🔥 Firewall bloqueando puerto 2271
- 🌍 DNS no resuelve vps.slsknet.org

**Soluciones:**
1. **Verifica Internet:**
   ```cmd
   ping 8.8.8.8
   ```

2. **Verifica DNS:**
   ```cmd
   nslookup vps.slsknet.org
   ```

3. **Prueba conectividad al servidor:**
   ```cmd
   telnet vps.slsknet.org 2271
   ```
   (Si telnet no está instalado: `dism /online /Enable-Feature /FeatureName:TelnetClient`)

### 5. "⚠️ Ya hay un intento de conexión en curso"

**Causa:**
- 🔄 Múltiples intentos simultáneos (ahora bloqueados automáticamente)

**Solución:**
- Esto es normal y la protección está funcionando
- Espera a que termine el intento actual

## 🔧 Pasos de Diagnóstico

### Paso 1: Verificar Credenciales
```cmd
type C:\Users\%USERNAME%\AppData\Roaming\SlskDown\config.json
```

### Paso 2: Probar Conectividad
```cmd
ping vps.slsknet.org
telnet vps.slsknet.org 2271
```

### Paso 3: Revisar Logs
Los logs ahora muestran información detallada:
- Longitud de usuario y contraseña
- Test de conectividad al servidor
- Tipo exacto de error
- Mensaje de error interno

### Paso 4: Probar con Cliente Oficial
Si SlskDown no conecta pero SoulseekQt sí:
- Puede ser un problema con la librería Soulseek.NET
- Reporta el issue con los logs

Si ninguno conecta:
- El problema es con tu cuenta o red
- Contacta soporte de Soulseek

## 📊 Información de Diagnóstico para Reportar

Si necesitas ayuda, incluye esta información:

1. **Logs completos** desde el inicio hasta el error
2. **Resultado del test de conectividad:**
   ```
   🌐 Verificando conectividad con vps.slsknet.org:2271...
   [resultado]
   ```
3. **Tipo de error:**
   ```
   ❌ Error durante ConnectAsync: [tipo]
   ```
4. **¿Funciona con SoulseekQt?** Sí/No
5. **Sistema operativo:** Windows 10/11
6. **Firewall/Antivirus activo:** Sí/No

## 🔐 Seguridad de Credenciales

**IMPORTANTE:** 
- Nunca compartas tu contraseña en logs o capturas
- El archivo config.json contiene tu contraseña en texto plano
- Mantén seguro el directorio `%APPDATA%\SlskDown`

## 🆘 Última Opción: Reset Completo

Si nada funciona:

1. **Cierra SlskDown**
2. **Elimina la configuración:**
   ```cmd
   rmdir /s C:\Users\%USERNAME%\AppData\Roaming\SlskDown
   ```
3. **Reinicia SlskDown**
4. **Configura desde cero**

## 📞 Soporte

- **Issues de SlskDown:** [GitHub Issues]
- **Soporte de Soulseek:** https://www.slsknet.org/news/
- **Foro de Soulseek:** https://www.reddit.com/r/Soulseek/
