with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()
    
print(f'Total lineas: {len(lines)}')
print(f'Ultimas 5 lineas:')
for i, line in enumerate(lines[-5:], len(lines)-4):
    print(f'  {i}: {line.rstrip()}')
