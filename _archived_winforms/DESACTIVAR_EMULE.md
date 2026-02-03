# Cómo Desactivar eMule Completamente en SlskDown

## Opción 1: Desactivar desde la Interfaz (Recomendado)

1. Abre SlskDown
2. Ve a la pestaña **Configuración**
3. Desmarca el checkbox **"Habilitar eMule"**
4. Guarda la configuración

Esto desactivará todas las funciones de eMule sin necesidad de modificar código.

## Opción 2: Desactivar en el Archivo de Configuración

Si prefieres desactivarlo permanentemente sin usar la interfaz:

1. Cierra SlskDown
2. Abre el archivo de configuración (normalmente en `%APPDATA%\SlskDown\config.json`)
3. Busca la línea `"enableEmule": true`
4. Cámbiala a `"enableEmule": false`
5. Guarda el archivo

## Opción 3: Eliminar Código de eMule (Avanzado)

Si quieres eliminar completamente el código de eMule del proyecto, necesitarás:

### Archivos a eliminar:
- `c:\p2p\SlskDown\EMule\` (carpeta completa)

### Archivos a modificar:
- `MainForm.cs`: Eliminar todas las referencias a `emule*` (variables, métodos, eventos)
- `MainForm.Designer.cs`: Eliminar controles UI relacionados con eMule
- Proyecto `.csproj`: Eliminar referencias a archivos de eMule

### Pasos detallados:

1. **Eliminar carpeta EMule**
   ```
   rd /s /q "c:\p2p\SlskDown\EMule"
   ```

2. **Buscar y eliminar en MainForm.cs**:
   - Buscar: `emule` (case insensitive)
   - Eliminar: todas las variables, métodos y referencias
   - Variables principales a buscar:
     - `emuleWebClient`
     - `emuleECClient`
     - `emuleSearchProvider`
     - `emuleDownloadProvider`
     - `emuleProgressTimer`
     - `emuleProgressSemaphore`
     - `emuleDownloadCache`
     - `emuleRetryCount`
     - `emuleCompletedNotifications`
     - `enableEmule`
     - `useEmuleEC`
     - `emulePassword`

3. **Eliminar controles UI**:
   - `chkEnableEmule`
   - `lblEmuleStatus`
   - `lblEmuleStats`
   - Cualquier otro control relacionado con eMule

4. **Recompilar**:
   ```
   dotnet build
   ```

## Recomendación

**Usa la Opción 1 o 2**. No es necesario eliminar el código de eMule del proyecto. Simplemente desactívalo desde la configuración y funcionará perfectamente solo con Soulseek.

El código de eMule está diseñado para ser opcional y no interfiere con Soulseek cuando está desactivado.

## Verificación

Después de desactivar eMule, verifica que:
- ✅ Soulseek se conecta correctamente
- ✅ Las búsquedas funcionan solo en Soulseek
- ✅ Las descargas de Soulseek funcionan normalmente
- ✅ No aparecen errores relacionados con eMule en los logs

## Soporte

Si tienes problemas después de desactivar eMule, revisa los logs en la pestaña de **Log** para identificar cualquier error.
