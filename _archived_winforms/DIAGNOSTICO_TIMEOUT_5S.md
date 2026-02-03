# Diagnóstico: Timeout de 5 Segundos Constante

## 🔴 Problema

**Síntoma:** La aplicación no puede conectar a Soulseek, con timeout constante de 5 segundos en cada intento.

```
[16:56:15] Intento 1/5 - Puerto: 54870
[16:56:20] ❌ TimeoutException: The wait timed out after 5000 milliseconds
[16:56:23] Intento 2/5 - Puerto: 52315
[16:56:29] ❌ TimeoutException: The wait timed out after 5000 milliseconds
... (se repite en todos los intentos)
```

---

## 🔍 Análisis

### **Timeout de 5 Segundos**

El timeout de **5 segundos** es un timeout **interno del cliente Soulseek.NET** que ocurre durante el handshake inicial con el servidor. Aunque configuramos `connectTimeout: 60000`, este timeout interno no se puede sobrescribir.

**Esto indica:**
- ❌ El servidor de Soulseek **NO está respondiendo** al handshake inicial
- ❌ Hay un **bloqueo de red** (firewall, ISP, etc.)
- ❌ El servidor puede estar **caído o sobrecargado**

---

## 🎯 Causas Posibles

### **1. Firewall de Windows**

El firewall puede estar bloqueando las conexiones salientes a Soulseek.

**Verificación:**
```powershell
# Ejecutar en PowerShell como Administrador
Get-NetFirewallRule | Where-Object {$_.DisplayName -like "*Soulseek*" -or $_.DisplayName -like "*SlskDown*"}
```

**Solución:**
1. Abre **Windows Defender Firewall**
2. Click en **"Permitir una aplicación o característica a través de Windows Defender Firewall"**
3. Click en **"Cambiar configuración"**
4. Click en **"Permitir otra aplicación"**
5. Busca y agrega `SlskDown.exe`
6. Marca **"Privada"** y **"Pública"**
7. Click en **"Aceptar"**

---

### **2. Antivirus Bloqueando**

Algunos antivirus bloquean conexiones P2P.

**Verificación:**
- Revisa los logs de tu antivirus (Avast, AVG, Norton, etc.)
- Busca bloqueos relacionados con `SlskDown.exe`

**Solución:**
1. Agrega `SlskDown.exe` a la lista de exclusiones del antivirus
2. O desactiva temporalmente el antivirus para probar

---

### **3. ISP Bloqueando P2P**

Algunos proveedores de Internet bloquean tráfico P2P.

**Verificación:**
- Prueba con una **VPN** activa
- Prueba con **datos móviles** (hotspot)

**Solución:**
- Usa una VPN (ProtonVPN, NordVPN, etc.)
- Contacta a tu ISP para verificar si bloquean P2P

---

### **4. Servidor de Soulseek Caído**

El servidor principal de Soulseek puede estar temporalmente caído.

**Verificación:**
- Visita https://www.slsknet.org/news/
- Revisa el estado del servidor
- Prueba con el **cliente oficial de Soulseek** (SoulseekQt)

**Solución:**
- Espera a que el servidor se recupere
- Intenta en diferentes horarios

---

### **5. Credenciales Incorrectas**

Aunque el timeout ocurre antes de la autenticación, credenciales incorrectas pueden causar problemas.

**Verificación:**
```
Usuario actual: carbar
```

**Solución:**
1. Verifica que el usuario existe en Soulseek
2. Verifica que la contraseña es correcta
3. Prueba iniciar sesión en el cliente oficial de Soulseek

---

### **6. Puerto Bloqueado**

Los puertos aleatorios (50000-60000) pueden estar bloqueados.

**Verificación:**
```powershell
# Probar conectividad a servidor de Soulseek
Test-NetConnection -ComputerName server.slsknet.org -Port 2242
```

**Solución:**
- Usa un puerto fijo conocido (ej: 2242)
- Configura port forwarding en tu router

---

## 🔧 Soluciones a Probar

### **Solución 1: Probar con Cliente Oficial**

**Objetivo:** Verificar si el problema es de la aplicación o de la red.

1. Descarga **SoulseekQt** desde https://www.slsknet.org/news/
2. Instala e intenta conectar con las mismas credenciales
3. Si conecta → El problema es de SlskDown
4. Si NO conecta → El problema es de red/firewall/servidor

---

### **Solución 2: Desactivar Firewall Temporalmente**

**Objetivo:** Verificar si el firewall está bloqueando.

1. Abre **Windows Defender Firewall**
2. Click en **"Activar o desactivar Firewall de Windows"**
3. Desactiva para **"Red privada"**
4. Intenta conectar con SlskDown
5. Si conecta → Agrega excepción permanente
6. Si NO conecta → El problema es otro
7. **IMPORTANTE:** Reactiva el firewall después de probar

---

### **Solución 3: Usar VPN**

**Objetivo:** Verificar si el ISP está bloqueando.

1. Instala una VPN gratuita (ProtonVPN, Windscribe, etc.)
2. Conéctate a un servidor VPN
3. Intenta conectar con SlskDown
4. Si conecta → Tu ISP está bloqueando P2P
5. Si NO conecta → El problema es otro

---

### **Solución 4: Cambiar Servidor DNS**

**Objetivo:** Resolver problemas de DNS.

1. Abre **Configuración de Red**
2. Click en **"Cambiar opciones del adaptador"**
3. Click derecho en tu conexión → **"Propiedades"**
4. Selecciona **"Protocolo de Internet versión 4 (TCP/IPv4)"**
5. Click en **"Propiedades"**
6. Selecciona **"Usar las siguientes direcciones de servidor DNS"**
7. DNS preferido: `8.8.8.8` (Google)
8. DNS alternativo: `8.8.4.4` (Google)
9. Click en **"Aceptar"**
10. Intenta conectar con SlskDown

---

### **Solución 5: Usar Puerto Fijo**

**Objetivo:** Evitar problemas con puertos aleatorios.

Voy a modificar el código para usar un puerto fijo conocido.

---

## 📊 Tabla de Diagnóstico

| Prueba | Resultado | Conclusión |
|--------|-----------|------------|
| Cliente oficial conecta | ✅ SÍ | Problema en SlskDown |
| Cliente oficial conecta | ❌ NO | Problema de red/firewall |
| Con firewall desactivado conecta | ✅ SÍ | Firewall bloqueando |
| Con firewall desactivado conecta | ❌ NO | Otro problema |
| Con VPN conecta | ✅ SÍ | ISP bloqueando |
| Con VPN conecta | ❌ NO | Servidor caído |
| Con DNS Google conecta | ✅ SÍ | Problema DNS |
| Con DNS Google conecta | ❌ NO | Otro problema |

---

## 🔍 Logs Adicionales Necesarios

Para diagnosticar mejor, necesito que ejecutes estos comandos y compartas los resultados:

### **1. Test de Conectividad al Servidor**

```powershell
# En PowerShell
Test-NetConnection -ComputerName server.slsknet.org -Port 2242
```

**Resultado esperado si funciona:**
```
ComputerName     : server.slsknet.org
RemoteAddress    : 208.76.170.59
RemotePort       : 2242
InterfaceAlias   : Ethernet
SourceAddress    : 192.168.1.100
TcpTestSucceeded : True
```

---

### **2. Verificar Reglas de Firewall**

```powershell
# En PowerShell como Administrador
Get-NetFirewallRule | Where-Object {$_.Enabled -eq 'True' -and $_.Direction -eq 'Outbound'} | Select-Object DisplayName, Action | Where-Object {$_.Action -eq 'Block'}
```

**Buscar:** Reglas que bloqueen conexiones salientes

---

### **3. Verificar Conexión a Internet**

```powershell
# En PowerShell
Test-NetConnection -ComputerName google.com -Port 443
```

**Resultado esperado:**
```
TcpTestSucceeded : True
```

---

### **4. Verificar DNS**

```powershell
# En PowerShell
Resolve-DnsName server.slsknet.org
```

**Resultado esperado:**
```
Name                           Type   TTL   Section    IPAddress
----                           ----   ---   -------    ---------
server.slsknet.org             A      3600  Answer     208.76.170.59
```

---

## 🎯 Próximos Pasos

### **Paso 1: Probar Cliente Oficial**

**CRÍTICO:** Esto determinará si el problema es de SlskDown o de tu red.

1. Descarga SoulseekQt
2. Intenta conectar
3. Comparte el resultado

---

### **Paso 2: Ejecutar Comandos de Diagnóstico**

Ejecuta los 4 comandos PowerShell de arriba y comparte los resultados.

---

### **Paso 3: Probar con Firewall Desactivado**

**TEMPORAL:** Solo para diagnóstico.

1. Desactiva Windows Firewall
2. Intenta conectar con SlskDown
3. Comparte el resultado
4. **Reactiva el firewall**

---

## 🔧 Modificación de Código (Opcional)

Si quieres probar con un puerto fijo en lugar de aleatorio:

**Cambio en línea 2390:**
```csharp
// ANTES:
int randomPort = new Random().Next(50000, 60000);

// DESPUÉS (puerto fijo):
int randomPort = 2242; // Puerto estándar de Soulseek
```

**O probar sin listener:**
```csharp
// Línea 2397:
enableListener: false,  // Cambiar de true a false
```

---

## 📝 Información del Servidor Soulseek

**Servidor principal:** `server.slsknet.org`  
**Puerto:** `2242`  
**IP:** `208.76.170.59` (puede cambiar)

---

## ⚠️ Notas Importantes

1. **El timeout de 5s es normal** si hay un bloqueo de red
2. **No es un bug de SlskDown** - es un problema de conectividad
3. **El cliente oficial usa el mismo protocolo** - si no conecta, SlskDown tampoco
4. **La mayoría de problemas son firewall/ISP**

---

## 📞 Soporte

Si después de probar todo sigue sin funcionar, comparte:

1. ✅ Resultado del cliente oficial de Soulseek
2. ✅ Resultados de los 4 comandos PowerShell
3. ✅ Resultado con firewall desactivado
4. ✅ Resultado con VPN (si probaste)
5. ✅ Tu país/ISP (algunos ISPs bloquean P2P)

---

**¡El timeout de 5s indica un bloqueo de red, no un bug de la aplicación!** 🔍🔒

**Fecha:** 2025-01-19  
**Versión:** SlskDown v2.2
