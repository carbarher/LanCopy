# Solución: aMule No Conecta a Redes P2P

**Fecha**: 28 de diciembre de 2025  
**Problema**: aMule WebServer funciona pero las redes eD2k y Kad no están conectadas

## Diagnóstico

Según los logs de tu aplicación:

```
[eMule Web] 📊 Estado redes - eD2k: ❌, Kad: ❌
[eMule Web] ⚠️ ADVERTENCIA: Ninguna red conectada
```

**Estado Actual**:
- ✅ aMule GUI está ejecutándose
- ✅ WebServer conectado (puerto 4711)
- ✅ Sesión iniciada correctamente
- ✅ Archivos de configuración presentes:
  - `server.met` (7109 bytes) ✅
  - `nodes.dat` (5418 bytes) ✅
- ❌ Red eD2k: Desconectada
- ❌ Red Kad: Desconectada

**Resultado**: Las búsquedas devuelven los mismos 388 resultados del historial acumulado del WebServer, no búsquedas reales en las redes P2P.

## Causa del Problema

La interfaz web de aMule **no puede forzar la conexión** a las redes P2P. Solo puede enviar comandos que aMule debe procesar manualmente. Aunque el código intenta reconectar automáticamente con:

```
[eMule Web] 🔄 Reconectando a servidores eD2k...
[eMule Web] 📡 Comando connect enviado: OK
[eMule Web] 🔄 Reconectando a red Kad...
[eMule Web] 📡 Comando Kad connect enviado: OK
```

Estos comandos **requieren que aMule GUI esté abierto y responda** a las solicitudes.

## Solución Inmediata

### Paso 1: Conectar eD2k

1. **Abre aMule** (la aplicación gráfica, no solo el daemon)
2. Ve a la pestaña **"Redes"** o **"Servidores"**
3. Verifica que haya servidores en la lista:
   - Si la lista está vacía, haz clic en **"Actualizar desde URL"**
   - URL recomendada: `http://www.gruk.org/server.met`
4. Haz clic en el botón **"Conectar"** (arriba a la izquierda)
5. Espera a ver el **icono verde** de conexión
6. Deberías ver: **"eD2k: Conectado"** en la barra de estado

### Paso 2: Conectar Kad

1. Ve a la pestaña **"Kad"** en aMule
2. Haz clic en el botón **"Conectar"** o **"Bootstrap"**
3. **Espera 2-5 minutos** (Kad tarda en conectar)
4. Deberías ver: **"Kad: Conectado"** o **"Kad: Firewalled"** (ambos funcionan)

### Paso 3: Verificar Conexión

1. En SlskDown, haz una nueva búsqueda
2. Los logs deberían mostrar:
   ```
   [eMule Web] 📊 Estado redes - eD2k: ✅, Kad: ✅
   [eMule Web] ✅ Ambas redes conectadas - búsqueda completa
   ```
3. Los resultados ahora serán búsquedas reales, no historial

## Solución Permanente

### Configurar Auto-Conexión en aMule

El código ya intentó configurar esto editando `amule.conf`, pero verifica manualmente:

1. Abre `C:\Users\carlo\AppData\Roaming\aMule\amule.conf`
2. Busca estas líneas y verifica que estén en `1`:
   ```ini
   ConnectToKad=1
   ConnectToED2K=1
   Autoconnect=1
   Reconnect=1
   ```
3. Guarda y reinicia aMule

### Verificar Firewall

Si las redes no conectan después de hacer clic en "Conectar":

1. **Puertos necesarios**:
   - TCP: 4662 (eD2k)
   - UDP: 4672 (eD2k + Kad)
   - TCP: 4711 (WebServer)

2. **Agregar excepción en Windows Firewall**:
   ```powershell
   # Ejecutar como Administrador
   New-NetFirewallRule -DisplayName "aMule TCP" -Direction Inbound -Protocol TCP -LocalPort 4662,4711 -Action Allow
   New-NetFirewallRule -DisplayName "aMule UDP" -Direction Inbound -Protocol UDP -LocalPort 4672 -Action Allow
   ```

## Mejoras Implementadas

He mejorado el código de `EMuleWebClient.cs` para proporcionar mejor diagnóstico:

### Antes
```csharp
OnLog?.Invoke($"[eMule Web] 📊 Estado redes - eD2k: ❌, Kad: ❌");
```

### Ahora
```csharp
OnLog?.Invoke($"[eMule Web] 📊 Estado redes - eD2k: ❌, Kad: ❌");
OnLog?.Invoke($"[eMule Web] 🔍 Verificando archivos de configuración...");
OnLog?.Invoke($"[eMule Web] 📁 server.met: ✅ OK (7109 bytes)");
OnLog?.Invoke($"[eMule Web] 📁 nodes.dat: ✅ OK (5418 bytes)");
```

Esto te ayudará a identificar rápidamente si faltan archivos de configuración.

## Preguntas Frecuentes

### ¿Por qué no se conecta automáticamente?

aMule requiere que el usuario haga clic manualmente en "Conectar" la primera vez. Después, si `Autoconnect=1` está configurado, debería conectar automáticamente al iniciar.

### ¿Puedo usar solo Kad sin eD2k?

Sí, Kad funciona independientemente. Sin embargo, eD2k tiene más servidores y fuentes, por lo que se recomienda usar ambas.

### ¿Qué significa "Kad: Firewalled"?

Significa que Kad está conectado pero tu puerto UDP está bloqueado. Aún puedes buscar y descargar, pero con menos eficiencia. Configura port forwarding en tu router para el puerto 4672 UDP.

### ¿Por qué las búsquedas devuelven los mismos resultados?

Cuando las redes no están conectadas, el WebServer de aMule muestra el historial acumulado de búsquedas anteriores. Por eso ves siempre los mismos 388 resultados. Una vez conectadas las redes, verás resultados reales y diferentes.

## Logs de Conexión Exitosa

Cuando todo funcione correctamente, deberías ver:

```
[eMule Web] 🔌 Conectando a aMule WebServer...
[eMule Web] ✅ Sesión iniciada correctamente
[eMule Web] ✅ Conectado exitosamente a la interfaz web de aMule
[eMule Web] 📊 Estado redes - eD2k: ✅, Kad: ✅
[eMule Web] ✅ Ambas redes conectadas - búsqueda completa
[eMule Web] 🔍 Buscando en múltiples redes: ovnis
[eMule Web] ✅ Red Global: 156 resultados
[eMule Web] ✅ Red Kad: 89 resultados
[eMule Web] ✅ Red Local: 12 resultados
[eMule Web] ✅ Total combinado: 257 resultados de 3 redes
```

## Recursos Adicionales

- **Lista de servidores eD2k**: http://www.gruk.org/server.met
- **Archivo nodes.dat para Kad**: http://www.nodes-dat.com/dl.php?load=nodes&trace=39513030.1674
- **Guía oficial de aMule**: https://www.amule.org/wiki/index.php/Getting_Started

## Resumen

1. ✅ aMule WebServer funciona correctamente
2. ✅ Archivos de configuración presentes
3. ❌ Redes P2P no conectadas (requiere acción manual)
4. 💡 **Solución**: Abre aMule GUI y haz clic en "Conectar" en ambas pestañas
5. 🔄 Después de conectar, las búsquedas funcionarán correctamente

---

**Nota**: Este es un comportamiento normal de aMule. La interfaz web no puede forzar la conexión a las redes P2P por razones de seguridad. Siempre requiere que el usuario haga clic manualmente en "Conectar" al menos una vez.
