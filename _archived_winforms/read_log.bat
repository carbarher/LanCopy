@echo off
cd /d c:\p2p\SlskDown
echo === LOG DE HOY ===
if exist logs\slskdown-2025-11-03.txt (
    type logs\slskdown-2025-11-03.txt
) else if exist logs\slskdown-2025-11-02.txt (
    type logs\slskdown-2025-11-02.txt
) else (
    echo No hay logs
)
pause
