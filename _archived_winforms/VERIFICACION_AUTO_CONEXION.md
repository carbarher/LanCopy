# Verificación: Auto-Conexión al Inicio

**Fecha**: 3 de diciembre de 2025  
**Estado**: ✅ Configurado por Defecto

## Configuración Actual

La aplicación **YA está configurada** para conectarse automáticamente al inicio.

### Código Verificado

**MainForm.cs - Línea 2649**:
```csharp
private bool autoConnect = true; // Habilitado por defecto para auto-conexión al iniciar
```

**MainForm.cs - Línea 5098**:
```csharp
chkAutoConnect = CreateCheckBox("🔄 Conectar automáticamente al iniciar", 
    autoConnect, 
    (s, e) => { autoConnect = chkAutoConnect.Checked; SaveConfig(); });
```

**MainForm.cs - Líneas 3091-3095**:
```csharp
bool shouldAutoConnect = (_networkConfig?.SoulseekEnabled == true && 
                          autoConnect && 
                          !string.IsNullOrWhiteSpace(username) && 
                          !string.IsNullOrWhiteSpace(password));

if (shouldAutoConnect)
{
    Log("🔄 Auto-conexión habilitada, conectando en 2 segundos...");
    // ... código de conexión
}
```

## Condiciones para Auto-Conexión

Para que la auto-conexión funcione, se deben cumplir **TODAS** estas condiciones:

1. ✅ `autoConnect = true` (por defecto)
2. ✅ `SoulseekEnabled = true` (configurado en NetworkConfiguration)
3. ⚠️ `username` no vacío (debe configurarse)
4. ⚠️ `password` no vacío (debe configurarse)

## Logs Esperados al Iniciar

Si todo está correcto:
```
[15:31:46] 🔍 Verificando auto-conexión: autoConnect=True, username='carbar' (longitud: 6), password=*********
[15:31:46] 🔄 Auto-conexión habilitada, conectando en 2 segundos...
[15:31:46] ⏰ Timer de auto-conexión iniciado
[15:31:48] 🚀 Iniciando auto-conexión a redes habilitadas...
[15:31:48] 🔵 Auto-conectando a Soulseek...
[15:32:24] ✅ Conexión exitosa
```

Si falta algo:
```
[15:31:46] ⚠️ Auto-conexión deshabilitada o credenciales faltantes
[15:31:46]    - username está vacío
```

## Verificar Configuración

### 1. Verificar en la UI

Al abrir la aplicación:
1. Ir a pestaña **Configuración**
2. Buscar sección **🔌 CONEXIÓN Y RED**
3. Verificar que el checkbox **"🔄 Conectar automáticamente al iniciar"** esté marcado

### 2. Verificar Credenciales

En la pestaña **Configuración**:
1. Sección **👤 CUENTA SOULSEEK**
2. Verificar que **Usuario** y **Contraseña** estén llenos
3. Si están vacíos, llenarlos y hacer clic en **Guardar**

### 3. Verificar Archivo de Configuración

Archivo: `%APPDATA%\SlskDown\config.json`

Debe contener:
```json
{
  "autoConnect": true,
  "username": "tu_usuario",
  "password": "tu_contraseña"
}
```

## Solución de Problemas

### Problema: No se conecta automáticamente

**Causa 1: Credenciales vacías**
- Solución: Configurar usuario y contraseña en la pestaña Configuración

**Causa 2: autoConnect deshabilitado**
- Solución: Marcar checkbox "Conectar automáticamente al iniciar"

**Causa 3: SoulseekEnabled = false**
- Solución: Eliminar `%APPDATA%\SlskDown\network_config.json` y reiniciar

**Causa 4: Archivo de configuración corrupto**
- Solución: Eliminar `%APPDATA%\SlskDown\config.json` y reconfigurar

### Problema: Se conecta pero se desconecta inmediatamente

**Causa: Configuración de red incorrecta**
- Solución: Eliminar `%APPDATA%\SlskDown\network_config.json`
- La app creará uno nuevo con valores correctos

## Forzar Auto-Conexión

Si quieres asegurarte de que **siempre** se conecte al inicio, puedes modificar el código:

**MainForm.cs - Línea 3091**:
```csharp
// ANTES (requiere credenciales)
bool shouldAutoConnect = (_networkConfig?.SoulseekEnabled == true && 
                          autoConnect && 
                          !string.IsNullOrWhiteSpace(username) && 
                          !string.IsNullOrWhiteSpace(password));

// DESPUÉS (fuerza conexión si hay credenciales)
bool shouldAutoConnect = (_networkConfig?.SoulseekEnabled == true && 
                          !string.IsNullOrWhiteSpace(username) && 
                          !string.IsNullOrWhiteSpace(password));
```

Esto ignoraría el checkbox y siempre intentaría conectar si hay credenciales.

## Testing

Para probar la auto-conexión:

1. **Cerrar la aplicación** completamente
2. **Eliminar** `%APPDATA%\SlskDown\network_config.json` (si existe)
3. **Abrir la aplicación**
4. **Configurar** usuario y contraseña
5. **Marcar** checkbox "Conectar automáticamente al iniciar"
6. **Guardar** configuración
7. **Cerrar** la aplicación
8. **Abrir** de nuevo
9. **Verificar** que se conecte automáticamente

## Valores por Defecto

Al instalar por primera vez:
- ✅ `autoConnect = true` (habilitado)
- ✅ `SoulseekEnabled = true` (habilitado)
- ❌ `username = ""` (vacío - **debe configurarse**)
- ❌ `password = ""` (vacío - **debe configurarse**)

**Conclusión**: La auto-conexión está habilitada por defecto, pero **requiere** que el usuario configure sus credenciales la primera vez.

## Recomendación

Para una mejor experiencia de usuario, considera:

1. **Mostrar diálogo de bienvenida** en el primer inicio
2. **Solicitar credenciales** antes de continuar
3. **Explicar** que la auto-conexión está habilitada
4. **Guardar** automáticamente después de configurar

Esto evitaría que el usuario tenga que buscar dónde configurar las credenciales.
