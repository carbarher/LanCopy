# INSTRUCCIONES MANUALES (si fallan los scripts)

## 🚨 PROBLEMAS IDENTIFICADOS:

1. **Comandos de desinstalación incorrectos** - `dotnet sdk uninstall` no existe
2. **Error de descarga** - Gateway error al intentar descargar desde Microsoft

## 📋 SOLUCIÓN MANUAL PASO A PASO:

### Paso 1: Desinstalar manualmente (Opcional pero recomendado)
1. **Abrir "Apps y características"** (Win + I → Apps → Apps y características)
2. **Buscar y desinstalar:**
   - "Microsoft .NET SDK 8.0.x"
   - "Microsoft Windows Desktop Runtime 8.0.x"
   - "Microsoft ASP.NET Core Runtime 8.0.x"
3. **Reiniciar el equipo**

### Paso 2: Descargar manualmente
1. **Abrir navegador** (Chrome, Firefox, Edge)
2. **Ir a:** https://dotnet.microsoft.com/download/dotnet/8.0
3. **Buscar sección:** "SDK .NET 8.0.100"
4. **Descargar:** "SDK .NET 8.0.100 (Windows x64)"
5. **Guardar en:** `c:\p2p\SlskDown\dotnet-sdk-8.0.100-win-x64.exe`

### Paso 3: Instalar manualmente
1. **Ejecutar como Administrador** el archivo descargado
2. **Seleccionar instalación "Típica"**
3. **Esperar finalización**
4. **Reiniciar terminal**

### Paso 4: Verificar instalación
```cmd
# Abrir CMD nuevo
dotnet --version
# Debe mostrar: 8.0.100

cd c:\p2p\SlskDown
dotnet build SlskDown.csproj --verbosity minimal
```

## 🔧 COMANDOS ALTERNATIVOS:

### Si PowerShell falla (problema de red):
```cmd
# Usar curl en lugar de PowerShell
curl -L -o dotnet-sdk-8.0.100-win-x64.exe https://download.visualstudio.microsoft.com/download/pr/8a38614b-9d81-43b2-8a2f-3eb2bfaa6c1e/dotnet-sdk-8.0.100-win-x64.exe
```

### Si curl también falla:
```cmd
# Usar bitsadmin (más robusto en redes corporativas)
bitsadmin /transfer dotnetDownload /download /priority normal https://download.visualstudio.microsoft.com/download/pr/8a38614b-9d81-43b2-8a2f-3eb2bfaa6c1e/dotnet-sdk-8.0.100-win-x64.exe %cd%\dotnet-sdk-8.0.100-win-x64.exe
```

## 🌐 MIRRORS ALTERNATIVOS:

Si el servidor principal de Microsoft no funciona:
1. **GitHub Releases:** https://github.com/dotnet/installer/releases
2. **NuGet Gallery:** https://www.nuget.org/packages/Microsoft.NET.Sdk/
3. **Archivos offline:** Descargar el "Offline Installer" completo

## 📊 DIAGNÓSTICO RÁPIDO:

Ejecuta estos comandos para entender el estado actual:
```cmd
# Ver qué tienes instalado
dotnet --list-sdks
dotnet --list-runtimes

# Verificar variables
echo %DOTNET_ROOT%
where dotnet

# Probar compilación simple
mkdir test_manual
cd test_manual
dotnet new console
dotnet build
```

## ⚠️ NOTAS IMPORTANTES:

1. **Requiere reinicio completo** después de instalar
2. **Ejecutar como Administrador** es obligatorio
3. **Antivirus puede bloquear** la descarga/installación
4. **Firewall corporativo** puede impedir la descarga automática
5. **Espacio necesario:** ~1GB libre en C:\

## 🎯 RESULTADO ESPERADO:

Después de instalación manual exitosa:
```
C:\>dotnet --version
8.0.100

C:\>cd c:\p2p\SlskDown
C:\p2p\SlskDown>dotnet build SlskDown.csproj --verbosity minimal
MSBuild version 17.8.3+195e7f5a3 for .NET
  Determining projects to restore...
  All projects are up-to-date for restore.
  SlskDown -> c:\p2p\SlskDown\bin\Debug\net8.0-windows\SlskDown.exe

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:15.32
```

## 🆘 SI TODO FALLA:

1. **Instalar Visual Studio 2022 Community** (incluye .NET SDK automáticamente)
2. **Usar chocolatey:** `choco install dotnet-sdk`
3. **Usar winget:** `winget install Microsoft.DotNet.SDK.8`
4. **Contactar al administrador de sistemas** si estás en entorno corporativo
