with open(r'c:\p2p\SlskDown\MainForm.cs', 'r', encoding='utf-8') as f:
    content = f.read()
    open_braces = content.count('{')
    close_braces = content.count('}')
    lines = content.split('\n')
    print(f"Total lines: {len(lines)}")
    print(f"Open braces: {open_braces}")
    print(f"Close braces: {close_braces}")
    print(f"Balance: {open_braces - close_braces}")
