with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()
    print(f"Total lineas: {len(lines)}")
    print(f"Ultima linea ({len(lines)}): {lines[-1].strip()}")
    print(f"Penultima linea ({len(lines)-1}): {lines[-2].strip()}")
