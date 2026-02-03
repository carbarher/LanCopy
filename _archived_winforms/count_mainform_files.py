#!/usr/bin/env python3
import os
import glob

mainform_files = glob.glob(r"c:\p2p\SlskDown\MainForm*.cs")
total_lines = 0

print("Archivos parciales de MainForm:\n")
for filepath in sorted(mainform_files):
    filename = os.path.basename(filepath)
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = len(f.readlines())
    total_lines += lines
    print(f"{filename}: {lines} líneas")

print(f"\nTotal combinado: {total_lines} líneas")
