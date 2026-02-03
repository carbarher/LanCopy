with open(r'c:\p2p\SlskDown\MainForm.Designer.cs', 'r', encoding='utf-8') as f:
    content = f.read()
    lines = content.split('\n')
    print(f"Lines: {len(lines)}")
    print(f"Open braces: {content.count('{')}")
    print(f"Close braces: {content.count('}')}")
    print(f"Balance: {content.count('{') - content.count('}')}")
