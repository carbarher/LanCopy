@echo off
python fix_commented_code.py > fix_output.txt 2>&1
type fix_output.txt
