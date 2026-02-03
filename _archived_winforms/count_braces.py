balance = 0
with open('MainForm.cs', 'r', encoding='utf-8') as f:
    for line_num, line in enumerate(f, 1):
        opens = line.count('{')
        closes = line.count('}')
        balance += opens - closes
        if balance < 0:
            print(f'Line {line_num}: NEGATIVE BALANCE ({balance})')
            print(f'  {line[:100]}')
        if line_num > 40710 and balance != 0:
            print(f'Line {line_num}: balance = {balance}')
            
print(f'\nFinal balance: {balance}')
if balance > 0:
    print(f'MISSING {balance} closing brace(s)')
elif balance < 0:
    print(f'EXTRA {-balance} closing brace(s)')
