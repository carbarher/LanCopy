with open(r"c:\p2p\SlskDown\MainForm.cs", 'r', encoding='utf-8') as f:
    lines = f.readlines()

balance = 0
for i, line in enumerate(lines[:36480], 1):
    balance += line.count('{') - line.count('}')
    if i >= 18300 and i <= 18310:
        print(f"{i:5d} [{balance:+4d}]: {line.rstrip()}")
    if i >= 36470 and i <= 36480:
        print(f"{i:5d} [{balance:+4d}]: {line.rstrip()}")

print(f"\nBalance de llaves hasta línea 36480: {balance}")
if balance > 0:
    print(f"Faltan {balance} llaves de cierre")
elif balance < 0:
    print(f"Sobran {-balance} llaves de cierre")
