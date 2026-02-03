with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    content = f.read()
    
open_braces = content.count('{')
close_braces = content.count('}')

print(f'Llaves de apertura: {open_braces}')
print(f'Llaves de cierre: {close_braces}')
print(f'Diferencia: {open_braces - close_braces}')
