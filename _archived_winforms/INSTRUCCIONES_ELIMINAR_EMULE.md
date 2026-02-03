# Instrucciones para Eliminar eMule de MainForm.cs

## ✅ Completado
- Carpeta `EMule/` eliminada

## 📋 Pasos Restantes

### Opción A: Usar Visual Studio Code (Recomendado)

1. Abre `MainForm.cs` en VS Code
2. Presiona `Ctrl+H` para abrir buscar y reemplazar
3. Activa "Use Regular Expression" (icono `.*`)
4. Ejecuta estos reemplazos en orden:

#### 1. Eliminar métodos completos de eMule
```regex
Buscar: private\s+(bool|void|async\s+Task)\s+(ChangeEmulePasswordInConfig|ShowEmulePasswordHelp|ReconnectEmuleAsync|StartEmuleProgressTimer|StopEmuleProgressTimer|UpdateEmuleDownloadProgressAsync|MoveCompletedEmuleFileAsync|InitializeNetworkOrchestrator|UpdateNetworkOrchestrator)\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}
Reemplazar: (dejar vacío)
```

#### 2. Eliminar declaraciones de variables
```regex
Buscar: private\s+.*\s+(emuleWebClient|emuleECClient|emuleSearchProvider|emuleDownloadProvider|emuleProgressTimer|emuleProgressSemaphore|emuleDownloadCache|emuleRetryCount|emuleCompletedNotifications|enableEmule|useEmuleEC|emulePassword|chkEnableEmule|lblEmuleStatus|lblEmuleStats)\s*[=;].*?;
Reemplazar: (dejar vacío)
```

#### 3. Eliminar parámetros emuleCount
```regex
Buscar: ,\s*int\s+emuleCount\s*=\s*0
Reemplazar: (dejar vacío)
```

```regex
Buscar: ,\s*emuleCount
Reemplazar: (dejar vacío)
```

#### 4. Limpiar LoadConfig
```regex
Buscar: enableEmule\s*=\s*configManager\.GetValue\([^)]+\);\s*\n
Reemplazar: (dejar vacío)
```

```regex
Buscar: emulePassword\s*=\s*configManager\.GetValue\([^)]+\);\s*\n
Reemplazar: (dejar vacío)
```

#### 5. Limpiar SaveConfig
```regex
Buscar: configManager\.SetValue\("enableEmule"[^)]+\);\s*\n
Reemplazar: (dejar vacío)
```

```regex
Buscar: configManager\.SetValue\("emulePassword"[^)]+\);\s*\n
Reemplazar: (dejar vacío)
```

#### 6. Limpiar logs
```regex
Buscar: var\s+emuleStatus\s*=\s*\([^)]+\)\s*\?\s*"[^"]*"\s*:\s*"[^"]*"\s*;\s*\n
Reemplazar: (dejar vacío)
```

```regex
Buscar: \[{soulseekStatus}/{emuleStatus}\]
Reemplazar: [{soulseekStatus}]
```

```regex
Buscar: ,\s*enableEmule\s*=\s*{enableEmule}
Reemplazar: (dejar vacío)
```

#### 7. Eliminar llamadas a métodos
```regex
Buscar: (InitializeNetworkOrchestrator|UpdateNetworkOrchestrator|StartEmuleProgressTimer|StopEmuleProgressTimer)\(\);\s*\n
Reemplazar: (dejar vacío)
```

#### 8. Limpiar UI de eMule
```regex
Buscar: chkEnableEmule\s*=\s*CreateCheckBox\([^;]+;\s*\n
Reemplazar: (dejar vacío)
```

```regex
Buscar: var\s+(lblEmulePassword|txtEmulePassword|btnReconnectEmule|btnAutoChangePassword|btnEmuleHelp)\s*=\s*[^;]+;\s*\n
Reemplazar: (dejar vacío)
```

#### 9. Eliminar bloques de código de eMule en ProcessDownload
Buscar manualmente y eliminar el bloque que empieza con:
```csharp
if (network == "eMule" && emuleWebClient != null && emuleWebClient.IsConnected)
{
    // ... todo el bloque hasta el cierre }
}
```

#### 10. Limpiar referencias en búsquedas
```regex
Buscar: bool\s+hasEmule\s*=\s*[^;]+;\s*\n
Reemplazar: (dejar vacío)
```

```regex
Buscar: \|\|\s*hasEmule
Reemplazar: (dejar vacío)
```

#### 11. Limpiar líneas vacías múltiples
```regex
Buscar: \n\s*\n\s*\n+
Reemplazar: \n\n
```

### Opción B: Compilar con Errores y Corregir

1. Intenta compilar el proyecto: `dotnet build`
2. Los errores de compilación te mostrarán exactamente dónde están las referencias a eMule
3. Ve eliminando cada referencia una por una

### Opción C: Usar el Documento DESACTIVAR_EMULE.md

Si prefieres no eliminar el código:
1. Simplemente desmarca el checkbox "🌐 eMule/ed2k" en la UI
2. El código de eMule quedará inactivo pero presente

## ⚠️ Importante

Después de eliminar el código:
1. Compila: `dotnet build`
2. Corrige cualquier error de compilación restante
3. Prueba que la aplicación funciona solo con Soulseek

## 🎯 Archivos a Verificar

- `MainForm.cs` - Archivo principal
- `SlskDown.csproj` - Puede tener referencias a archivos de eMule
- Cualquier archivo `.cs` que importe `SlskDown.EMule`

## 📝 Notas

- La carpeta `EMule/` ya fue eliminada ✅
- Quedan ~200-300 referencias a eMule en `MainForm.cs`
- El proceso manual puede tomar 30-60 minutos
- Considera usar la Opción C si no necesitas eliminar el código permanentemente
