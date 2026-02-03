#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import sys

def check_braces(filename):
    try:
        with open(filename, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
            lines = content.split('\n')
            
        open_braces = content.count('{')
        close_braces = content.count('}')
        
        print(f"Total lines: {len(lines)}")
        print(f"Open braces: {open_braces}")
        print(f"Close braces: {close_braces}")
        print(f"Difference: {open_braces - close_braces}")
        
        if open_braces != close_braces:
            print("\nERROR: Braces are not balanced!")
            
            # Find where imbalance occurs
            balance = 0
            for i, line in enumerate(lines, 1):
                balance += line.count('{') - line.count('}')
                if balance < 0:
                    print(f"First negative balance at line {i}: {line[:80]}")
                    break
        else:
            print("\nOK: Braces are balanced")
            
    except Exception as e:
        print(f"Error: {e}")
        return 1
    
    return 0

if __name__ == '__main__':
    sys.exit(check_braces('MainForm.cs'))
