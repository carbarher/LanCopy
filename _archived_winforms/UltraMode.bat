@echo off
echo 🚀 Activando SlskDown MODO ULTRA-OPTIMIZADO...
echo.

echo 📦 Compilando versión Ultra...
cd c:\p2p\SlskDown
dotnet build -c Release -p:DefineConstants=ULTRA_MODE

echo.
echo ✅ Modo Ultra activado!
echo 🏆 Características Élite:
echo    🔥 SIMD Processing (AVX2)
echo    🧠 Zero-Allocation Architecture  
echo    ⚡ Lock-Free Channels
echo    🌊 Memory-Mapped Files
echo    🚀 Unsafe Optimizations
echo.

echo 🎯 Iniciando SlskDown Ultra...
cd c:\p2p
start c:\p2p\ejecutar_limpio.bat

echo.
echo 🏁 SlskDown Ultra listo para rendimiento máximo!
pause
