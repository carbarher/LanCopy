@echo off
cd /d "c:\p2p\SlskDown"
python test_simple.py > variaciones_output.txt 2>&1
type variaciones_output.txt
pause
