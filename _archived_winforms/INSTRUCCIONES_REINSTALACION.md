# INSTRUCCIONES DE REINSTALACIÓN .NET SDK 8.0
# ===============================================

## 📋 RESUMEN DE SCRIPTS CREADOS:

### 1. **REINSTALAR_DOTNET_SDK.bat** (Recomendado)
- Script batch automático completo
- Desinstala versiones existentes
- Limpia directorios
- Descarga e instala .NET SDK 8.0
- Configura variables de entorno
- Prueba compilación de SlskDown

### 2. **REINSTALAR_DOTNET_SDK.ps1** (Alternativa PowerShell)
- Versión PowerShell con más detalles
- Mejor manejo de errores
- Logs más verbosos
- Misma funcionalidad que versión batch

### 3. **PROBAR_COMPILACION.bat** (Para diagnóstico)
- Verifica estado actual del entorno
- Crea proyecto de prueba simple
- Compara con compilación de SlskDown
- Útil para diagnosticar problemas específicos

## 🚀 INSTRUCCIONES DE USO:

### Opción A: Script Batch (Más simple)
```cmd
# Abrir CMD como Administrador
cd c:\p2p\SlskDown
REINSTALAR_DOTNET_SDK.bat
```

### Opción B: PowerShell (Más robusto)
```powershell
# Abrir PowerShell como Administrador
cd c:\p2p\SlskDown
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
.\REINSTALAR_DOTNET_SDK.ps1
```

### Opción C: Solo diagnóstico (Si crees que .NET ya funciona)
```cmd
cd c:\p2p\SlskDown
PROBAR_COMPILACION.bat
```

## ⚠️ REQUISITOS IMPORTANTES:

1. **Ejecutar como Administrador** - Necesario para instalar software y modificar variables del sistema
2. **Conexión a Internet** - Para descargar el instalador de .NET SDK
3. **Cerrar IDEs y terminales** - Reinicia después de la instalación
4. **Espacio en disco** - ~1GB para la instalación

## 🔍 SI FALLA LA INSTALACIÓN:

### Problemas comunes y soluciones:

1. **"Access Denied"**
   - Asegúrate de ejecutar como Administrador
   - Cierra Visual Studio y otros IDEs

2. **"No se puede descargar"**
   - Verifica conexión a Internet
   - Intenta descargar manualmente desde: https://dotnet.microsoft.com/download

3. **"dotnet no reconocido" después de instalar**
   - Reinicia el equipo completamente
   - Verifica variables de entorno: `echo %DOTNET_ROOT%`

4. **SlskDown no compila pero proyecto de prueba sí**
   - El problema es específico del código de SlskDown
   - Revisa los errores de compilación específicos

## 📞 PASOS ADICIONALES:

Si todo falla, puedes:

1. **Instalar Visual Studio 2022 Community** (Incluye .NET SDK)
2. **Usar Visual Studio Code** con extensión C#
3. **Compilar manualmente** con `csc.exe` si está disponible

## 🎯 RESULTADO ESPERADO:

Después de una instalación exitosa deberías ver:
```
✅ ¡INSTALACIÓN COMPLETADA CON ÉXITO!
✅ EJECUTABLE GENERADO: c:\p2p\SlskDown\bin\Debug\net8.0-windows\SlskDown.exe
🎉 ¡ÉXITO COMPLETO! El entorno .NET funciona correctamente
```

## 📝 NOTAS FINALES:

- Los scripts crean logs detallados del proceso
- Se limpian archivos temporales automáticamente
- La instalación toma ~5-10 minutos
- Requiere reinicio del terminal/IDE después
