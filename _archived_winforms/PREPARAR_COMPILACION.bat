@echo off
echo Preparando proyecto para compilación...
echo.

cd /d c:\p2p\SlskDown

echo 1. Creando archivos stub necesarios...

REM Crear VirtualListViewOptimization.cs si no existe
if not exist "VirtualListViewOptimization.cs" (
    echo Creating VirtualListViewOptimization.cs...
    echo namespace SlskDown { public class VirtualListViewOptimization { } } > VirtualListViewOptimization.cs
)

REM Crear Optimizations.cs si no existe  
if not exist "Optimizations.cs" (
    echo Creating Optimizations.cs...
    echo namespace SlskDown { public static class Optimizations { } } > Optimizations.cs
)

REM Crear ParallelAuthorSearch.cs si no existe
if not exist "ParallelAuthorSearch.cs" (
    echo Creating ParallelAuthorSearch.cs...
    echo namespace SlskDown { public static class ParallelAuthorSearch { } } > ParallelAuthorSearch.cs
)

REM Crear LazyTabLoader.cs si no existe
if not exist "LazyTabLoader.cs" (
    echo Creating LazyTabLoader.cs...
    echo namespace SlskDown { public static class LazyTabLoader { } } > LazyTabLoader.cs
)

REM Crear SearchCache.cs si no existe
if not exist "SearchCache.cs" (
    echo Creating SearchCache.cs...
    echo namespace SlskDown { public static class SearchCache { } } > SearchCache.cs
)

REM Crear LogCompressor.cs si no existe
if not exist "LogCompressor.cs" (
    echo Creating LogCompressor.cs...
    echo namespace SlskDown { public static class LogCompressor { } } > LogCompressor.cs
)

REM Crear SearchThrottler.cs si no existe
if not exist "SearchThrottler.cs" (
    echo Creating SearchThrottler.cs...
    echo namespace SlskDown { public static class SearchThrottler { } } > SearchThrottler.cs
)

REM Crear MemoryMonitor.cs si no existe
if not exist "MemoryMonitor.cs" (
    echo Creating MemoryMonitor.cs...
    echo namespace SlskDown { public static class MemoryMonitor { } } > MemoryMonitor.cs
)

echo.
echo 2. Verificando archivos de servicios...
if not exist "Services\CacheService.cs" echo WARNING: CacheService.cs no encontrado
if not exist "Services\ConfigService.cs" echo WARNING: ConfigService.cs no encontrado
if not exist "Services\LoggingService.cs" echo WARNING: LoggingService.cs no encontrado

echo.
echo ✅ Preparación completada
echo.
pause
