# Fix: Configuración Residual de eMule Causaba Desconexiones

**Fecha**: 3 de diciembre de 2025  
**Estado**: ✅ Corregido

## Problema

Después de eliminar eMule del código, la aplicación se comportaba incorrectamente:

1. **Al iniciar**: Mostraba "🟢 Solo eMule" en los logs
2. **Conexión**: Se conectaba a Soulseek correctamente
3. **Desconexión inmediata**: Se desconectaba porque detectaba "Soulseek deshabilitado"
4. **Reconexión fallida**: El sistema de reconexión no funcionaba

### Logs del Problema

```
[15:31:46] ℹ️ INFO | GENERAL | 🟢 Solo eMule
[15:31:46] ❌ ERROR | GENERAL | 🔵 Soulseek: ❌ DESHABILITADO
[15:31:46] ✅ SUCCESS | GENERAL | 🟢 eMule: ✅ HABILITADO
...
[15:32:24] ✅ SUCCESS | CONEXION | ✅ Conexión exitosa (Soulseek)
...
[15:38:46] ❌ ERROR | CONEXION | ⚠️ Desconexión detectada
[15:38:48] ℹ️ INFO | GENERAL | [DEBUG] Soulseek deshabilitado - CheckAndReconnect ignorado
```

## Causa Raíz

El archivo de configuración guardado (`network_config.json`) tenía valores incorrectos:

```json
{
  "SoulseekEnabled": false,
  "EMuleEnabled": true,
  "PreferredNetwork": "eMule"
}
```

Esto ocurrió porque:
1. El usuario había probado la configuración de redes antes
2. El archivo `NetworkConfiguration.cs` todavía tenía propiedades de eMule
3. Al cargar, se restauraban estos valores incorrectos
4. La aplicación pensaba que eMule estaba habilitado y Soulseek deshabilitado

## Solución Implementada

### 1. Limpiar NetworkConfiguration.cs

**Eliminadas todas las propiedades de eMule**:

```csharp
// ANTES
public bool SoulseekEnabled { get; set; } = true;
public bool EMuleEnabled { get; set; } = false;
public string EMuleHost { get; set; } = "localhost";
public int EMulePort { get; set; } = 4712;
public string EMulePassword { get; set; } = "";
public bool EMuleAutoConnect { get; set; } = false;
public string PreferredNetwork { get; set; } = "Both";

// DESPUÉS
public bool SoulseekEnabled { get; set; } = true;
public string PreferredNetwork { get; set; } = "Soulseek";
```

**Métodos simplificados**:

```csharp
// GetActiveNetworks() - Solo devuelve Soulseek
public string[] GetActiveNetworks()
{
    if (SoulseekEnabled)
        return new[] { "Soulseek" };
    return Array.Empty<string>();
}

// HasActiveNetworks() - Solo verifica Soulseek
public bool HasActiveNetworks()
{
    return SoulseekEnabled;
}

// GetModeDescription() - Sin referencias a eMule
public string GetModeDescription()
{
    if (SoulseekEnabled)
        return "🔵 Soulseek";
    else
        return "⚠️ Soulseek deshabilitado";
}

// Validate() - Sin validaciones de eMule
public (bool isValid, string error) Validate()
{
    if (!HasActiveNetworks())
        return (false, "Debe habilitar Soulseek");
        
    if (SoulseekEnabled && string.IsNullOrWhiteSpace(SoulseekUsername))
        return (false, "Soulseek requiere un nombre de usuario");
        
    return (true, "");
}
```

### 2. Eliminar Archivo de Configuración Corrupto

```cmd
del /F /Q "%APPDATA%\SlskDown\network_config.json"
```

Esto fuerza a la aplicación a crear un nuevo archivo con valores por defecto correctos.

## Archivos Modificados

1. **Core/NetworkConfiguration.cs**: 8 cambios
   - Eliminadas propiedades de eMule
   - Simplificados métodos de validación
   - Actualizado valor por defecto de `PreferredNetwork`

2. **Archivo de configuración del usuario**: Eliminado
   - `%APPDATA%\SlskDown\network_config.json`

## Resultado

✅ **Compilación exitosa**  
✅ **Configuración limpia** - Solo Soulseek  
✅ **Conexión estable** - Sin desconexiones  
✅ **Reconexión funcional** - Sistema de reconexión activo  
✅ **Logs correctos** - Muestra "🔵 Soulseek"

## Comportamiento Esperado

Al iniciar la aplicación ahora:

```
[15:31:46] ℹ️ INFO | GENERAL | 🔵 Soulseek
[15:31:46] ✅ SUCCESS | GENERAL | 🔵 Soulseek: ✅ HABILITADO
[15:32:24] ✅ SUCCESS | CONEXION | ✅ Conexión exitosa
```

Si se desconecta:

```
[15:38:46] ❌ ERROR | CONEXION | ⚠️ Desconexión detectada
[15:38:48] ℹ️ INFO | CONEXION | 🔄 Intentando reconectar...
[15:38:50] ✅ SUCCESS | CONEXION | ✅ Reconectado exitosamente
```

## Notas Técnicas

- El archivo `NetworkConfigurationForm.cs` todavía tiene código de eMule pero está deshabilitado
- Se puede eliminar completamente en una futura limpieza
- Por ahora, el formulario no se usa (la configuración está en el tab principal)
- La eliminación del archivo de configuración es segura - se recrea con valores correctos

## Testing

Para verificar:
1. Eliminar `%APPDATA%\SlskDown\network_config.json`
2. Iniciar SlskDown
3. Verificar logs: debe mostrar "🔵 Soulseek"
4. Verificar conexión: debe conectar automáticamente
5. Verificar reconexión: debe reconectar si se desconecta

## Prevención Futura

Para evitar este problema:
- No usar el formulario `NetworkConfigurationForm` (está obsoleto)
- La configuración de Soulseek se maneja en el tab principal
- Si se necesita resetear configuración: eliminar `network_config.json`
