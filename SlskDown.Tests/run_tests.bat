@echo off
echo ========================================
echo EJECUTANDO TESTS UNITARIOS
echo ========================================
echo.

dotnet test --verbosity normal

echo.
echo ========================================
echo TESTS COMPLETADOS
echo ========================================
pause
