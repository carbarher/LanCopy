@echo off
echo Iniciando compilacion...
dotnet publish SlskDown.csproj -c Release -r win-x64 --self-contained false -o bin\publish_hotfix12 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
echo.
echo Compilacion completada. Codigo de salida: %ERRORLEVEL%
echo.
if exist bin\publish_hotfix12 (
    echo Directorio creado correctamente
    dir bin\publish_hotfix12
) else (
    echo ERROR: Directorio no creado
)
pause
