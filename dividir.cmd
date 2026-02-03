@echo off
cd /d c:\p2p
powershell -Command "$l=Get-Content 'autores_sf_2500.txt';$l[0..499]|Out-File 'autores_sf_2500_1.txt' -Encoding UTF8;$l[500..999]|Out-File 'autores_sf_2500_2.txt' -Encoding UTF8;$l[1000..1499]|Out-File 'autores_sf_2500_3.txt' -Encoding UTF8;$l[1500..1905]|Out-File 'autores_sf_2500_4.txt' -Encoding UTF8"
echo.
echo Archivos creados:
dir autores_sf_2500_*.txt
echo.
echo Listo!
pause
