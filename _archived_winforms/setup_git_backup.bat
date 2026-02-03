@echo off
echo ========================================
echo CONFIGURAR GIT PARA AUTO-BACKUP
echo ========================================
echo.

cd /d C:\p2p\SlskDown

REM Configurar usuario si no existe
git config user.name "SlskDown Dev"
git config user.email "slskdown@local.dev"

REM Crear .gitignore si no existe
if not exist ".gitignore" (
    echo Creando .gitignore...
    (
        echo bin/
        echo obj/
        echo *.exe
        echo *.dll
        echo *.pdb
        echo *.user
        echo *.suo
        echo .vs/
        echo *.log
        echo *.txt
        echo !README.txt
        echo auto_search_state.json
        echo config.json
        echo download_rules.json
    ) > .gitignore
)

REM Hacer commit inicial si no hay commits
git rev-parse HEAD >nul 2>&1
if errorlevel 1 (
    echo Creando commit inicial...
    git add .gitignore *.cs *.csproj *.md
    git commit -m "Initial commit: SlskDown project setup"
)

echo.
echo ========================================
echo ✓ Git configurado correctamente
echo ========================================
echo.
echo Usa estos comandos:
echo   compile_and_save.bat  - Compilar y guardar
echo   auto_commit.bat       - Solo guardar cambios
echo.
echo Para ver historial: git log --oneline -20
echo Para ver cambios: git diff
echo ========================================
pause
