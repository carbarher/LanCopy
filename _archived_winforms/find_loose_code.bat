@echo off
echo Buscando archivos .cs con codigo suelto...
echo.
for %%f in (*.cs) do (
    if not "%%f"=="MainForm.cs" (
        if not "%%f"=="MainForm.Designer.cs" (
            if not "%%f"=="Program.cs" (
                echo Revisando: %%f
                findstr /C:"namespace" "%%f" >nul 2>&1
                if errorlevel 1 (
                    echo   ^-^> NO TIENE NAMESPACE: %%f
                )
            )
        )
    )
)
echo.
pause
