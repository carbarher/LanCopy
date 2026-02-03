# 🔥 Instrucciones: Agregar SlskDown al Firewall

## 🔴 Problema

SlskDown no puede conectar a Soulseek debido al **Firewall de Windows** bloqueando la aplicación.

**Test de conectividad:**
- ✅ Servidor accesible: `server.slsknet.org:2242`
- ✅ Puerto abierto: `TcpTestSucceeded: True`
- ❌ SlskDown bloqueado por firewall

---

## ✅ Solución Automática (RECOMENDADA)

### **Paso 1: Abrir PowerShell como Administrador**

1. Presiona **Windows + X**
2. Selecciona **"Windows PowerShell (Administrador)"** o **"Terminal (Administrador)"**
3. Si aparece UAC (Control de Cuentas de Usuario), click en **"Sí"**

### **Paso 2: Ejecutar el Script**

Copia y pega este comando en PowerShell:

```powershell
cd C:\p2p\SlskDown
.\agregar_firewall.ps1
```

**Si aparece error de "ejecución de scripts deshabilitada"**, ejecuta primero:

```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force
cd C:\p2p\SlskDown
.\agregar_firewall.ps1
```

### **Paso 3: Verificar**

El script mostrará:
```
================================================
  Agregando SlskDown al Firewall de Windows
================================================

Archivo encontrado:
  C:\p2p\SlskDown\bin\Release\net8.0-windows\SlskDown.exe

Eliminando reglas antiguas (si existen)...
  OK - No habia reglas antiguas

Creando regla de ENTRADA (Inbound)...
  OK - Regla de entrada creada

Creando regla de SALIDA (Outbound)...
  OK - Regla de salida creada

================================================
  Configuracion completada!
================================================
```

---

## ✅ Solución Manual (Alternativa)

Si prefieres hacerlo manualmente:

### **Paso 1: Abrir Firewall de Windows**

1. Presiona **Windows + R**
2. Escribe: `wf.msc`
3. Presiona **Enter**

### **Paso 2: Crear Regla de Entrada**

1. En el panel izquierdo, click en **"Reglas de entrada"**
2. En el panel derecho, click en **"Nueva regla..."**
3. Selecciona **"Programa"** → **Siguiente**
4. Selecciona **"Esta ruta de acceso del programa:"**
5. Click en **"Examinar..."** y busca:
   ```
   C:\p2p\SlskDown\bin\Release\net8.0-windows\SlskDown.exe
   ```
6. Click en **Siguiente**
7. Selecciona **"Permitir la conexión"** → **Siguiente**
8. Marca **todas** las casillas (Dominio, Privado, Público) → **Siguiente**
9. Nombre: `SlskDown - Soulseek Client (Entrada)`
10. Click en **Finalizar**

### **Paso 3: Crear Regla de Salida**

1. En el panel izquierdo, click en **"Reglas de salida"**
2. Repite los pasos 2-10 del Paso 2
3. En el paso 9, usa el nombre: `SlskDown - Soulseek Client (Salida)`

---

## 🔍 Verificar Reglas Creadas

Para verificar que las reglas se crearon correctamente:

```powershell
Get-NetFirewallRule -DisplayName "*SlskDown*" | Select-Object DisplayName, Direction, Action, Enabled
```

**Resultado esperado:**
```
DisplayName                          Direction Action Enabled
-----------                          --------- ------ -------
SlskDown - Soulseek Client (Entrada) Inbound   Allow  True
SlskDown - Soulseek Client (Salida)  Outbound  Allow  True
```

---

## 🎯 Después de Agregar las Reglas

1. **Cierra SlskDown** si está abierto
2. **Abre SlskDown** de nuevo
3. **Intenta conectar**
4. Deberías ver:
   ```
   [17:35:00] Intento 1/5 - Puerto: 54321
   [17:35:01] ✅ Conexión exitosa en puerto 54321
   [17:35:01] 🔔 Keep-alive timer iniciado (ping cada 5 minutos)
   ```

---

## ⚠️ Si Sigue Sin Funcionar

Si después de agregar las reglas de firewall **aún no conecta**:

### **1. Verificar Antivirus**

Tu antivirus (Avast, AVG, Norton, Kaspersky, etc.) puede estar bloqueando:

1. Abre tu antivirus
2. Busca **"Configuración"** o **"Settings"**
3. Busca **"Excepciones"** o **"Exclusiones"** o **"Whitelist"**
4. Agrega la carpeta: `C:\p2p\SlskDown\`

### **2. Desactivar Firewall Temporalmente (Solo para Test)**

**SOLO PARA DIAGNÓSTICO:**

```powershell
# Desactivar firewall (como Administrador)
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled False

# Probar SlskDown

# REACTIVAR INMEDIATAMENTE:
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True
```

Si funciona con el firewall desactivado → El problema es el firewall  
Si NO funciona → El problema es otro (antivirus, ISP, etc.)

### **3. Verificar Puerto del Router**

Si estás detrás de un router, puede que necesites:

1. Abrir el panel de administración del router (usualmente `192.168.1.1` o `192.168.0.1`)
2. Buscar **"Port Forwarding"** o **"Reenvío de puertos"**
3. Agregar regla:
   - **Puerto externo:** 50000-60000
   - **Puerto interno:** 50000-60000
   - **IP local:** `192.168.1.34` (tu IP)
   - **Protocolo:** TCP

---

## 📝 Logs Esperados Después del Fix

### **Antes (Bloqueado):**
```
[17:29:28] Intento 1/5 - Puerto: 59068
[17:29:33] ❌ Timeout de 5 segundos
```

### **Después (Funcionando):**
```
[17:35:00] Intento 1/5 - Puerto: 54321
[17:35:01] ✅ Conexión exitosa en puerto 54321
[17:35:01] 🔔 Keep-alive timer iniciado (ping cada 5 minutos)
[17:40:01] 💓 Keep-alive: Conexión activa
[17:45:01] 💓 Keep-alive: Conexión activa
```

---

## 🆘 Ayuda Adicional

Si después de todo esto **aún no funciona**, comparte:

1. Screenshot del Firewall mostrando las reglas de SlskDown
2. Logs completos de SlskDown
3. Nombre de tu antivirus (si tienes)
4. Resultado de:
   ```powershell
   Get-NetFirewallRule -DisplayName "*SlskDown*" | Select-Object DisplayName, Direction, Action, Enabled
   ```

---

**¡Ejecuta el script `agregar_firewall.ps1` y comparte el resultado!** 🔥
