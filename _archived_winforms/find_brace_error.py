with open('MainForm.cs', 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

balance = 0
for i, line in enumerate(lines, 1):
    opens = line.count('{')
    closes = line.count('}')
    balance += opens - closes
    
    # Mostrar solo las últimas 50 líneas y líneas con balance negativo
    if i > len(lines) - 50 or balance < 0:
        print(f"Line {i:5d}: balance={balance:3d} opens={opens} closes={closes} | {line.rstrip()[:80]}")

print(f"\nFinal balance: {balance}")
print(f"Total lines: {len(lines)}")

if balance > 0:
    print(f"\n❌ MISSING {balance} closing brace(s) }}")
elif balance < 0:
    print(f"\n❌ EXTRA {-balance} closing brace(s) }}")
else:
    print("\n✅ Braces are balanced!")
