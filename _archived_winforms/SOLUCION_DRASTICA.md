# Solución Drástica - MainForm.cs Corrupto

## Situación
El archivo MainForm.cs está severamente corrupto con código duplicado y suelto que causa 232 errores.

## Solución Recomendada

### Opción 1: Usar Versión Limpia (RECOMENDADO)
Si tienes una versión anterior de MainForm.cs que compilaba (antes de esta sesión), úsala:

```cmd
# Buscar backups
dir MainForm.cs.backup* /O-D

# Restaurar el más reciente que funcionaba
copy MainForm.cs.backup_YYYYMMDD_HHMMSS MainForm.cs
```

### Opción 2: Proyecto Limpio Desde Cero
Crear un nuevo proyecto SlskDown2 con solo los archivos esenciales:
- MainForm.cs (versión limpia)
- MainForm.Config.cs
- Program.cs
- Servicios necesarios

### Opción 3: Edición Manual
Abrir MainForm.cs en un editor y:
1. Buscar línea 926: `// NOTA: AddConfigTab()`
2. Eliminar TODO desde línea 928 hasta encontrar `private void InitializeComponents()`
3. Asegurar que solo hay UNA definición de cada método

## Archivos de Backup Disponibles
```
MainForm.cs.backup_*
```

## Comando para Ver Backups
```cmd
dir /B /O-D MainForm.cs.backup*
```

## Próximos Pasos
1. Restaurar backup funcional
2. Aplicar solo las correcciones necesarias:
   - Excluir archivos problemáticos (ya hecho en .csproj)
   - Comentar PerformanceDashboard (ya hecho)
   - NO tocar el resto del código
