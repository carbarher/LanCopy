@echo off
echo ===================================
echo VERIFICACION Y COMPILACION
echo ===================================
echo.

echo Contando lineas del archivo...
powershell -NoProfile -Command "$lines = Get-Content 'MainForm.cs'; Write-Host 'Total de lineas:' $lines.Count"
echo.

echo Verificando lineas 20303-20308...
powershell -NoProfile -Command "$lines = Get-Content 'MainForm.cs'; Write-Host '20303:' $lines[20302]; Write-Host '20304:' $lines[20303]; Write-Host '20305:' $lines[20304]; Write-Host '20306:' $lines[20305]; Write-Host '20307:' $lines[20306]; Write-Host '20308:' $lines[20307]"
echo.

echo Compilando proyecto...
dotnet build SlskDown.csproj --no-incremental
echo.

echo Codigo de salida: %ERRORLEVEL%
echo ===================================
pause
