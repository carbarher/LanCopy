@echo off
echo Cerrando ScoreDown si esta en ejecucion...
taskkill /IM ScoreDown.exe /F >nul 2>&1
timeout /t 1 /nobreak >nul

echo Compilando ScoreDown...
dotnet build "C:\p2p\ScoreDown\ScoreDown.csproj"
if errorlevel 1 (
    echo ERROR: compilacion fallida. No se lanza ScoreDown.
    pause
    exit /b 1
)

echo Lanzando ScoreDown...
start "" "C:\p2p\ScoreDown\bin\Debug\net9.0-windows\ScoreDown.exe"
