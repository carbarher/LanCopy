@echo off
echo Dividiendo autores_sf_2500.txt en archivos de 500 lineas...

powershell -Command "$lines = Get-Content 'autores_sf_2500.txt'; $chunk = 1; for($i=0; $i -lt $lines.Count; $i+=500) { $end = [Math]::Min($i+499, $lines.Count-1); $lines[$i..$end] | Out-File \"autores_sf_2500_$chunk.txt\" -Encoding UTF8; Write-Host \"Creado: autores_sf_2500_$chunk.txt ($($end-$i+1) lineas)\"; $chunk++ }"

echo.
echo Listando archivos creados:
dir autores_sf_2500_*.txt /b

pause
