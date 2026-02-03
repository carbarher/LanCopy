from pathlib import Path
import re

# Leer MainForm.cs
text = Path('MainForm.cs').read_text(encoding='utf-8', errors='ignore')
lines = text.split('\n')

# Patrones a comentar
patterns_to_comment = [
    r'\.Priority\s*=',  # task.Priority = ...
    r'DownloadPriority\.',  # DownloadPriority.High
    r'DownloadStatus\.Pending',  # DownloadStatus.Pending
    r'vpnManager\.',  # vpnManager.anything
    r'networkOptimizations\.',  # networkOptimizations.anything
    r'PurgeAuthorsWithoutResults',  # Métodos de purga
    r'StartAutoSearchUIUpdateTimer',  # UI update timer
    r'StopAutoSearchUIUpdateTimer',
    r'FlushAutoSearchUIUpdates',
    r'QueuePrioritizationStrategy\.RarestFirst',  # Estrategia no existente
]

fixed_lines = []
commented_count = 0

for i, line in enumerate(lines, 1):
    # Si la línea contiene algún patrón problemático, comentarla
    should_comment = any(re.search(pattern, line) for pattern in patterns_to_comment)
    
    if should_comment and not line.strip().startswith('//'):
        # Comentar la línea manteniendo la indentación
        indent = len(line) - len(line.lstrip())
        fixed_lines.append(' ' * indent + '// DESACTIVADO: ' + line.lstrip())
        commented_count += 1
        if commented_count <= 10:  # Mostrar primeras 10
            print(f"Línea {i}: {line.strip()[:80]}")
    else:
        fixed_lines.append(line)

# Guardar archivo fijado
Path('MainForm.cs').write_text('\n'.join(fixed_lines), encoding='utf-8')
print(f"\n✅ {commented_count} líneas comentadas")
print("Archivo MainForm.cs actualizado")
