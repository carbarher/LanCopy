#!/usr/bin/env python3
import sys
sys.path.insert(0, r'c:\p2p\scripts')

from incorporar_libros_pre1900 import main

if __name__ == "__main__":
    try:
        result = main()
        print(f"\nScript finalizado con código: {result}")
    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()
