@echo off
echo Buscando archivos con "partial class MainForm"...
echo.
for %%f in (*.cs) do (
    if not "%%f"=="MainForm.cs" (
        if not "%%f"=="MainForm.Designer.cs" (
            findstr /C:"partial class MainForm" "%%f" >nul 2>&1
            if not errorlevel 1 (
                echo ENCONTRADO: %%f
            )
        )
    )
)
echo.
echo Busqueda completada.
pause
