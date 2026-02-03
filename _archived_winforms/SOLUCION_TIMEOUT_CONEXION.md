# 🔧 Solución: Timeout de Conexión

## ⚠️ Problema

```
Conectando a Soulseek...
✗ Error de conexión: The wait timed out after 5000 milliseconds
```

### Causa
El timeout de conexión predeterminado de **5 segundos** es demasiado corto para:
- Servidores Soulseek lentos o saturados
- Conexiones de Internet lentas
- Firewalls que tardan en responder
- Horarios de alta demanda

## ✅ Solución Implementada

### 1. **Timeout Aumentado a 30 Segundos**
```csharp
var options = new SoulseekClientOptions(
    serverConnectionOptions: new ConnectionOptions(
        connectTimeout: 30000,  // 30 segundos (antes: 5s)
        inactivityTimeout: 300000  // 5 minutos
    )
);
```

### 2. **Sistema de Reintentos (3 intentos)**
```csharp
for (int attempt = 1; attempt <= 3; attempt++)
{
    try
    {
        await client.ConnectAsync(username, password);
        // Éxito
    }
    catch
    {
        // Reintento con delay incremental (3s, 6s)
    }
}
```

### 3. **Delays Incrementales**
- **Intento 1:** Inmediato
- **Intento 2:** Espera 3 segundos
- **Intento 3:** Espera 6 segundos

### 4. **Mejor Diagnóstico de Errores**
```
✗ Error en intento 1: The wait timed out after 30000 milliseconds
   Detalle: [información adicional]
   Reintentando en 3 segundos...
```

## 📊 Comparación

| Aspecto | Antes | Ahora |
|---------|-------|-------|
| **Timeout** | 5 segundos | 30 segundos |
| **Reintentos** | 0 (falla inmediato) | 3 intentos |
| **Tiempo total máximo** | 5 segundos | ~96 segundos (30s + 3s + 30s + 6s + 30s) |
| **Tasa de éxito** | Baja en redes lentas | Alta en la mayoría de casos |

## 🎯 Ventajas

### ✅ Mayor Tolerancia
- Funciona con servidores lentos
- Tolera picos de latencia
- Se adapta a conexiones variables

### ✅ Reintentos Automáticos
- No requiere intervención manual
- Delays incrementales evitan saturar
- Máximo 3 intentos para no esperar eternamente

### ✅ Mejor Feedback
- Muestra cada intento
- Indica tiempo de espera
- Sugiere causas posibles si falla

## 🔍 Diagnóstico de Problemas

### Si sigue fallando después de 3 intentos:

#### 1. **Verificar Credenciales**
```
❌ No se pudo conectar después de 3 intentos.
Posibles causas:
  - Credenciales incorrectas ← Verificar primero
```

**Solución:**
- Verifica usuario y contraseña en `config.json`
- Prueba conectar desde la aplicación principal
- Confirma que la cuenta esté activa

#### 2. **Servidor Soulseek Caído**
```
✗ Error: Connection refused
✗ Error: No connection could be made
```

**Solución:**
- Espera 10-15 minutos
- Verifica status en foros de Soulseek
- Prueba más tarde

#### 3. **Firewall Bloqueando**
```
✗ Error: A connection attempt failed
✗ Error: The wait timed out
```

**Solución:**
- Desactiva temporalmente firewall para probar
- Agrega excepción para la aplicación
- Verifica reglas de firewall de Windows

#### 4. **ISP Bloqueando P2P**
```
✗ Error: Connection timed out (consistentemente)
```

**Solución:**
- Usa VPN
- Cambia puerto en configuración
- Contacta a tu ISP

#### 5. **Red Inestable**
```
✗ Error en intento 1: timeout
✗ Error en intento 2: timeout
✗ Error en intento 3: timeout
```

**Solución:**
- Verifica velocidad de Internet
- Reinicia router
- Prueba con cable en lugar de WiFi

## 🚀 Cómo Usar

### Opción 1: Ejecutar Prueba
```batch
quick_test.bat
```

Ahora verás:
```
Conectando a Soulseek...
Intento 1/3...
✓ Conectado exitosamente
```

### Opción 2: Si Falla el Primer Intento
```
Conectando a Soulseek...
Intento 1/3...
✗ Error en intento 1: The wait timed out after 30000 milliseconds
   Reintentando en 3 segundos...

Intento 2/3...
✓ Conectado exitosamente
```

### Opción 3: Si Fallan Todos los Intentos
```
Conectando a Soulseek...
Intento 1/3...
✗ Error en intento 1: ...
   Reintentando en 3 segundos...

Intento 2/3...
✗ Error en intento 2: ...
   Reintentando en 6 segundos...

Intento 3/3...
✗ Error en intento 3: ...

❌ No se pudo conectar después de 3 intentos.
Posibles causas:
  - Credenciales incorrectas
  - Servidor Soulseek caído o lento
  - Firewall bloqueando la conexión
  - ISP bloqueando puertos P2P
```

## 📝 Recomendaciones

### ✅ Hacer:
1. **Esperar pacientemente** durante los reintentos
2. **Verificar credenciales** si falla consistentemente
3. **Probar en diferentes horarios** si el servidor está saturado
4. **Usar VPN** si tu ISP bloquea P2P

### ❌ Evitar:
1. **No cancelar** durante los reintentos
2. **No ejecutar múltiples instancias** simultáneamente
3. **No modificar timeout** a menos de 15 segundos
4. **No ignorar los mensajes de error** - contienen información útil

## 🎉 Resultado Esperado

Con estas mejoras:
- ✅ **Mayor tasa de éxito** en la conexión
- ✅ **Funciona con servidores lentos**
- ✅ **Reintentos automáticos** sin intervención
- ✅ **Mejor diagnóstico** de problemas
- ✅ **Feedback claro** en cada paso

## 🔧 Configuración Avanzada

Si necesitas ajustar los timeouts, edita `StressTest.cs`:

```csharp
// Línea 48-51
var options = new SoulseekClientOptions(
    serverConnectionOptions: new ConnectionOptions(
        connectTimeout: 30000,  // Ajustar aquí (milisegundos)
        inactivityTimeout: 300000
    )
);

// Línea 57
int maxRetries = 3;  // Ajustar número de reintentos

// Línea 79
int delay = attempt * 3000;  // Ajustar delay entre reintentos
```

### Valores Recomendados:

| Escenario | connectTimeout | maxRetries | delay |
|-----------|----------------|------------|-------|
| **Red rápida** | 15000 (15s) | 2 | 2000 (2s) |
| **Red normal** | 30000 (30s) | 3 | 3000 (3s) |
| **Red lenta** | 60000 (60s) | 5 | 5000 (5s) |
| **Servidor saturado** | 60000 (60s) | 5 | 10000 (10s) |

## 📞 Soporte

Si sigues teniendo problemas de conexión después de aplicar estas mejoras:

1. Verifica que la aplicación principal (MainForm) pueda conectar
2. Revisa los logs para errores específicos
3. Prueba con diferentes credenciales
4. Contacta al desarrollador con los logs completos

---

**Nota:** Estos cambios también benefician a la aplicación principal, ya que usa la misma lógica de conexión mejorada.
