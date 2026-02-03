@echo off
echo ========================================
echo 🔧 COMPILACIÓN FORZADA DE SLSKDOWN
echo ========================================
echo.
echo 🗑️ Limpiando archivos antiguos...
if exist bin rmdir /S /Q bin
if exist obj rmdir /S /Q obj
echo.
echo 📦 Restaurando paquetes...
dotnet restore
echo.
echo 🔨 Compilando en modo Release...
dotnet build -c Release -v detailed > compile_output.txt 2>&1
echo.
echo 📊 Verificando salida...
if exist "bin\Release\net8.0-windows\SlskDown.exe" (
    echo ✅ ¡ÉXITO! Ejecutable generado
    echo 📂 Ubicación: bin\Release\net8.0-windows\SlskDown.exe
    dir "bin\Release\net8.0-windows\SlskDown.exe"
) else (
    echo ❌ ERROR: No se generó el ejecutable
    echo 📝 Revisando log de compilación...
    findstr /C:"error" /C:"Error" /C:"warning" compile_output.txt
    echo.
    echo 🔍 Intentando compilación directa con csc...
    
    REM Compilar directamente con el compilador de C#
    set CSC="C:\Program Files\dotnet\sdk\8.0.415\Roslyn\bincore\csc.dll"
    set REFS=C:\Users\carlo\.nuget\packages\soulseek\8.4.1\lib\net8.0\Soulseek.dll
    
    if exist %CSC% (
        echo 🔨 Compilando con CSC directamente...
        dotnet %CSC% /target:winexe /out:SlskDown.exe /reference:%REFS% /reference:"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.11\System.Windows.Forms.dll" Program.cs MainForm.cs MainForm.Config.cs MainForm.Ultra.cs
        
        if exist SlskDown.exe (
            echo ✅ ¡Compilación directa exitosa!
            if not exist "bin\Release\net8.0-windows" mkdir "bin\Release\net8.0-windows"
            move SlskDown.exe "bin\Release\net8.0-windows\"
            copy %REFS% "bin\Release\net8.0-windows\"
        )
    )
)
echo.
echo 📋 Archivos en bin\Release\net8.0-windows:
if exist "bin\Release\net8.0-windows" (
    dir "bin\Release\net8.0-windows"
) else (
    echo ❌ El directorio no existe
)
echo.
pause
