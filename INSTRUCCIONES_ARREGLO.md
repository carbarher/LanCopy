# Instrucciones para Arreglar Bloques Comentados

## Problema Detectado

El proyecto tiene **417 errores de compilación** causados por bloques de código comentados incorrectamente con el patrón:

```csharp
// ERROR: funcion(
    parametros,
    mas_parametros
);
```

La primera línea está comentada pero las siguientes no, causando errores de sintaxis.

## Solución Automática

### Opción 1: Arreglo Automático + Compilación (RECOMENDADO)

Ejecuta este comando que hace todo automáticamente:

```batch
ARREGLAR_Y_COMPILAR.bat
```

Este script:
1. ✅ Crea backup automático de MainForm.cs
2. ✅ Arregla todos los bloques comentados
3. ✅ Compila el proyecto
4. ✅ Muestra resultado final

### Opción 2: Solo Arreglar (sin compilar)

Si solo quieres arreglar los bloques sin compilar:

```powershell
powershell -ExecutionPolicy Bypass -File fix_all_errors.ps1
```

### Opción 3: Modo Prueba (Dry Run)

Para ver qué cambios se harían SIN aplicarlos:

```powershell
powershell -ExecutionPolicy Bypass -File fix_all_errors.ps1 -DryRun
```

## Qué Hace el Script

El script `fix_all_errors.ps1` arregla automáticamente:

### Patrón 1: Comentario en misma línea
```csharp
// ERROR: if (condition)
```
↓
```csharp
if (condition)
```

### Patrón 2: Bloques multi-línea
```csharp
// ERROR: return RustCore.RegexMatch(
    @"patron",
    texto
);
```
↓
```csharp
return RustCore.RegexMatch(
    @"patron",
    texto
);
```

### Patrón 3: Asignaciones encadenadas
```csharp
// ERROR: duplicateAuthorGroups = duplicates
    .OrderByDescending(g => g.Members.Count)
    .ThenBy(g => g.NormalizedKey)
    .ToList();
```
↓
```csharp
duplicateAuthorGroups = duplicates
    .OrderByDescending(g => g.Members.Count)
    .ThenBy(g => g.NormalizedKey)
    .ToList();
```

## Backups

Cada vez que ejecutas el script, se crea un backup automático:

```
MainForm.cs.backup_20260102_104500
```

Si algo sale mal, puedes restaurar:

```batch
copy SlskDown\MainForm.cs.backup_YYYYMMDD_HHMMSS SlskDown\MainForm.cs
```

## Verificación Post-Arreglo

Después de arreglar, el script compila automáticamente. Si aún hay errores:

1. Revisa el output de compilación
2. Identifica líneas problemáticas
3. Ejecuta el script de nuevo (algunos bloques pueden requerir múltiples pasadas)

## Archivos Creados

- `fix_all_errors.ps1` - Script principal de arreglo
- `fix_commented_blocks.ps1` - Script alternativo con modo verbose
- `ARREGLAR_Y_COMPILAR.bat` - Script todo-en-uno (arreglo + compilación)
- `INSTRUCCIONES_ARREGLO.md` - Este documento

## Solución de Problemas

### "No se puede ejecutar scripts"
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy Bypass
```

### "Aún hay errores después de arreglar"
Ejecuta el script 2-3 veces. Algunos bloques anidados requieren múltiples pasadas.

### "Quiero revisar manualmente"
Usa el modo verbose:
```powershell
powershell -File fix_commented_blocks.ps1 -Verbose
```

## Siguiente Paso

**Ejecuta ahora:**
```batch
ARREGLAR_Y_COMPILAR.bat
```

Esto arreglará todos los bloques y compilará el proyecto automáticamente.
