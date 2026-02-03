# Diagnóstico: Problemas de Conexión a Soulseek

## 🔍 Pasos de Diagnóstico

### **1. Revisar Logs de Conexión**

Busca en el log estos mensajes clave:

```
Intento 1/5 - Puerto: XXXXX
   Usuario: [tu_usuario]
   Configurando cliente con timeout de 60 segundos...
   Cliente creado, suscribiendo eventos...
   Iniciando ConnectAsync con timeout de 60 segundos...
```

**¿Qué mensaje aparece después?**

---

## ❌ Posibles Errores y Soluciones

### **Error 1: TimeoutException**

**Log:**
```
❌ Excepción capturada: Tipo=TimeoutException, Mensaje=The operation has timed out
```

**Causas:**
- Servidor de Soulseek no responde
- Firewall bloqueando conexión
- ISP bloqueando puertos

**Soluciones:**
1. ✅ Verificar que el servidor de Soulseek esté funcionando (visita slsknet.org)
2. ✅ Desactivar temporalmente firewall/antivirus
3. ✅ Probar con VPN si ISP bloquea P2P
4. ✅ Verificar conexión a Internet

---

### **Error 2: Invalid Credentials**

**Log:**
```
❌ Excepción capturada: Tipo=LoginException, Mensaje=Invalid credentials
⚠️ Error de autenticación - no se reintentará
```

**Causas:**
- Usuario o contraseña incorrectos
- Cuenta suspendida

**Soluciones:**
1. ✅ Verificar usuario y contraseña en config.json
2. ✅ Probar login en cliente oficial de Soulseek
3. ✅ Crear nueva cuenta si es necesaria

---

### **Error 3: Connection Refused**

**Log:**
```
❌ Excepción capturada: Tipo=SocketException, Mensaje=Connection refused
   InnerException: SocketException - No connection could be made
```

**Causas:**
- Puerto bloqueado por firewall
- Servidor rechaza conexión

**Soluciones:**
1. ✅ Abrir puerto en firewall de Windows
2. ✅ Abrir puerto en router (port forwarding)
3. ✅ Probar con puerto diferente manualmente

---

### **Error 4: Network Unreachable**

**Log:**
```
❌ Excepción capturada: Tipo=SocketException, Mensaje=Network is unreachable
```

**Causas:**
- Sin conexión a Internet
- Adaptador de red deshabilitado

**Soluciones:**
1. ✅ Verificar conexión a Internet
2. ✅ Reiniciar adaptador de red
3. ✅ Verificar DNS (ping 8.8.8.8)

---

### **Error 5: Timeout Interno (5 segundos)**

**Log:**
```
Intento 1/5 - Puerto: 54321
   Usuario: carbar
   Configurando cliente con timeout de 60 segundos...
   Cliente creado, suscribiendo eventos...
   Iniciando ConnectAsync con timeout de 60 segundos...
❌ Excepción capturada: Tipo=TimeoutException, Mensaje=...
(Se repite en ~5 segundos cada vez)
```

**Causa:**
- Timeout interno del cliente Soulseek.NET (no configurable)
- Servidor muy lento o sobrecargado

**Soluciones:**
1. ✅ Esperar y reintentar más tarde
2. ✅ Verificar que servidor esté disponible
3. ✅ Probar con VPN diferente

---

## 🔧 Comandos de Diagnóstico

### **1. Verificar Conectividad Básica**

```cmd
ping 8.8.8.8
```
**Esperado:** Respuestas exitosas  
**Si falla:** Problema de red local

---

### **2. Verificar DNS**

```cmd
nslookup slsknet.org
```
**Esperado:** IP del servidor  
**Si falla:** Problema de DNS

---

### **3. Verificar Puerto Abierto**

```cmd
netstat -an | findstr :50000
```
**Esperado:** Nada (puerto libre) o LISTENING  
**Si aparece ESTABLISHED:** Puerto ocupado

---

### **4. Test de Firewall**

```cmd
netsh advfirewall show allprofiles state
```
**Si está ON:** Puede estar bloqueando

**Desactivar temporalmente:**
```cmd
netsh advfirewall set allprofiles state off
```

**Reactivar después:**
```cmd
netsh advfirewall set allprofiles state on
```

---

## 📊 Checklist de Diagnóstico

### **Verificaciones Básicas:**

- [ ] **Internet funciona** (puedes navegar)
- [ ] **Usuario y contraseña correctos** (verificar en config.json)
- [ ] **Servidor Soulseek disponible** (visitar slsknet.org)
- [ ] **Firewall desactivado temporalmente** (para probar)
- [ ] **Antivirus desactivado temporalmente** (para probar)

---

### **Verificaciones Avanzadas:**

- [ ] **Puerto no bloqueado por ISP** (probar con VPN)
- [ ] **Router con port forwarding** (si es necesario)
- [ ] **Sin proxy/VPN problemático** (probar sin VPN)
- [ ] **Hora del sistema correcta** (importante para SSL)

---

## 🎯 Soluciones Rápidas

### **Solución 1: Reiniciar Todo**

```
1. Cerrar SlskDown
2. Reiniciar router
3. Reiniciar PC
4. Abrir SlskDown
5. Intentar conectar
```

---

### **Solución 2: Cambiar Puerto Manualmente**

Editar `config.json`:
```json
{
  "listenPort": 55555
}
```

Probar con: 50000, 54321, 55555, 56789, 59999

---

### **Solución 3: Probar con VPN**

1. Activar VPN (ProtonVPN, Windscribe, etc.)
2. Conectar a servidor en otro país
3. Intentar conectar a Soulseek

---

### **Solución 4: Verificar Credenciales**

1. Ir a slsknet.org
2. Crear cuenta nueva o recuperar contraseña
3. Actualizar config.json con nuevas credenciales

---

## 📝 Información para Reportar

Si el problema persiste, proporciona:

```
1. Logs completos de conexión (desde "Intento 1/5" hasta el error)
2. Tipo de excepción (TimeoutException, SocketException, etc.)
3. Mensaje de error completo
4. InnerException (si aparece)
5. Sistema operativo y versión
6. ¿Firewall/antivirus activo?
7. ¿Usando VPN?
8. ¿ISP conocido por bloquear P2P?
```

---

## 🔍 Logs Esperados (Conexión Exitosa)

```
🔄 Conectando a Soulseek...
Intento 1/5 - Puerto: 54321
   Usuario: carbar
   Configurando cliente con timeout de 60 segundos...
   Cliente creado, suscribiendo eventos...
   Iniciando ConnectAsync con timeout de 60 segundos...
   ConnectAsync completado en 2.3s
✅ Conexión exitosa en puerto 54321
✅ CONECTADO A SOULSEEK - Usuario: carbar
```

---

## 🔍 Logs Esperados (Error de Timeout)

```
🔄 Conectando a Soulseek...
Intento 1/5 - Puerto: 54321
   Usuario: carbar
   Configurando cliente con timeout de 60 segundos...
   Cliente creado, suscribiendo eventos...
   Iniciando ConnectAsync con timeout de 60 segundos...
❌ Excepción capturada: Tipo=TimeoutException, Mensaje=The operation has timed out
❌ Intento 1 falló: The operation has timed out
⏳ Esperando 3s antes del siguiente intento...
   (Tipo de error: TimeoutException)

Intento 2/5 - Puerto: 56789
   Usuario: carbar
   ...
```

---

## 💡 Tip: Modo Verbose

Para obtener más información, revisa el archivo de log completo en:
```
c:\p2p\SlskDown\logs\[fecha].log
```

---

**¿Qué error específico estás viendo en los logs?** 🔍

Comparte los logs y te ayudaré a identificar el problema exacto.
