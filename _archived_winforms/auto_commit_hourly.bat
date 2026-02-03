@echo off
REM Auto-commit cada hora para no perder cambios
:loop
git add MainForm.cs *.cs *.csproj
git commit -m "Auto-save: %date:~-4,4%%date:~-10,2%%date:~-7,2%_%time:~0,2%%time:~3,2%%time:~6,2%"
echo Commit realizado: %date% %time%
timeout /t 3600 /nobreak
goto loop
