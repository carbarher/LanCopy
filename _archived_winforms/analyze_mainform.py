from pathlib import Path
import re

text = Path('MainForm.cs').read_text(encoding='utf-8', errors='ignore')

open_braces = text.count('{')
close_braces = text.count('}')
regions = len(re.findall(r'\#region', text))
endregions = len(re.findall(r'\#endregion', text))

report = [
    f"Open braces: {open_braces}",
    f"Close braces: {close_braces}",
    f"Brace diff: {open_braces - close_braces}",
    f"Regions: {regions}",
    f"Endregions: {endregions}"
]

Path('analyze_mainform.txt').write_text('\n'.join(report), encoding='utf-8')
