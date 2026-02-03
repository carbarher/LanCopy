#!/usr/bin/env python3
"""
Script para eliminar todas las referencias a eMule de MainForm.cs
"""
import re
import sys

def remove_emule_references(input_file, output_file):
    """Elimina todas las referencias a eMule del archivo"""
    
    with open(input_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    original_lines = len(content.split('\n'))
    
    # 1. Eliminar métodos completos relacionados con eMule
    methods_to_remove = [
        r'private\s+bool\s+ChangeEmulePasswordInConfig\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}',
        r'private\s+void\s+ShowEmulePasswordHelp\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}',
        r'private\s+async\s+Task\s+ReconnectEmuleAsync\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}',
        r'private\s+void\s+StartEmuleProgressTimer\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}',
        r'private\s+void\s+StopEmuleProgressTimer\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}',
        r'private\s+async\s+Task\s+UpdateEmuleDownloadProgressAsync\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}',
        r'private\s+async\s+Task\s+MoveCompletedEmuleFileAsync\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}',
        r'private\s+void\s+InitializeNetworkOrchestrator\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}',
        r'private\s+void\s+UpdateNetworkOrchestrator\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}',
    ]
    
    for pattern in methods_to_remove:
        content = re.sub(pattern, '', content, flags=re.DOTALL | re.MULTILINE)
    
    # 2. Eliminar declaraciones de variables relacionadas con eMule
    variable_patterns = [
        r'private\s+.*\s+emuleWebClient\s*[=;].*?;',
        r'private\s+.*\s+emuleECClient\s*[=;].*?;',
        r'private\s+.*\s+emuleSearchProvider\s*[=;].*?;',
        r'private\s+.*\s+emuleDownloadProvider\s*[=;].*?;',
        r'private\s+.*\s+emuleProgressTimer\s*[=;].*?;',
        r'private\s+.*\s+emuleProgressSemaphore\s*[=;].*?;',
        r'private\s+.*\s+emuleDownloadCache\s*[=;].*?;',
        r'private\s+.*\s+emuleRetryCount\s*[=;].*?;',
        r'private\s+.*\s+emuleCompletedNotifications\s*[=;].*?;',
        r'private\s+bool\s+enableEmule\s*[=;].*?;',
        r'private\s+bool\s+useEmuleEC\s*[=;].*?;',
        r'private\s+string\s+emulePassword\s*[=;].*?;',
        r'private\s+CheckBox\s+chkEnableEmule\s*[=;].*?;',
        r'private\s+Label\s+lblEmuleStatus\s*[=;].*?;',
        r'private\s+Label\s+lblEmuleStats\s*[=;].*?;',
    ]
    
    for pattern in variable_patterns:
        content = re.sub(pattern, '', content, flags=re.MULTILINE)
    
    # 3. Eliminar parámetros emuleCount de métodos
    content = re.sub(r',\s*int\s+emuleCount\s*=\s*0', '', content)
    content = re.sub(r',\s*emuleCount', '', content)
    
    # 4. Eliminar referencias a enableEmule en LoadConfig
    content = re.sub(r'enableEmule\s*=\s*configManager\.GetValue\([^)]+\);?\s*\n', '', content)
    content = re.sub(r'emulePassword\s*=\s*configManager\.GetValue\([^)]+\);?\s*\n', '', content)
    
    # 5. Eliminar referencias a enableEmule en SaveConfig
    content = re.sub(r'configManager\.SetValue\("enableEmule"[^)]+\);?\s*\n', '', content)
    content = re.sub(r'configManager\.SetValue\("emulePassword"[^)]+\);?\s*\n', '', content)
    
    # 6. Eliminar referencias en logs
    content = re.sub(r'var\s+emuleStatus\s*=\s*\([^)]+\)\s*\?\s*"[^"]*"\s*:\s*"[^"]*"\s*;?\s*\n', '', content)
    content = re.sub(r'\[{soulseekStatus}/{emuleStatus}\]', '[{soulseekStatus}]', content)
    content = re.sub(r',\s*enableEmule\s*=\s*{enableEmule}', '', content)
    content = re.sub(r'emuleSearchProvider\s*=\s*\([^)]+\)', '', content)
    
    # 7. Eliminar bloques de UI de eMule
    ui_patterns = [
        r'chkEnableEmule\s*=\s*CreateCheckBox\([^;]+;',
        r'var\s+lblEmulePassword\s*=\s*CreateLabel\([^;]+;',
        r'var\s+txtEmulePassword\s*=\s*new\s+TextBox\s*\{[^}]+\};',
        r'txtEmulePassword\.TextChanged\s*\+=\s*\([^}]+\};',
        r'var\s+btnReconnectEmule\s*=\s*new\s+Button\s*\{[^}]+\};',
        r'btnReconnectEmule\.Click\s*\+=\s*async\s*\([^}]+\};',
        r'var\s+btnAutoChangePassword\s*=\s*new\s+Button\s*\{[^}]+\};',
        r'btnAutoChangePassword\.Click\s*\+=\s*\([^}]+\};',
        r'var\s+btnEmuleHelp\s*=\s*new\s+Button\s*\{[^}]+\};',
        r'btnEmuleHelp\.Click\s*\+=\s*\([^}]+\};',
    ]
    
    for pattern in ui_patterns:
        content = re.sub(pattern, '', content, flags=re.DOTALL)
    
    # 8. Eliminar referencias en ProcessDownload
    content = re.sub(
        r'if\s*\(\s*network\s*==\s*"eMule"[^}]+\}',
        '',
        content,
        flags=re.DOTALL
    )
    
    # 9. Eliminar referencias en búsquedas
    content = re.sub(r'bool\s+hasEmule\s*=\s*[^;]+;', '', content)
    content = re.sub(r'\|\|\s*hasEmule', '', content)
    
    # 10. Eliminar llamadas a métodos de eMule
    content = re.sub(r'InitializeNetworkOrchestrator\(\);?\s*\n', '', content)
    content = re.sub(r'UpdateNetworkOrchestrator\(\);?\s*\n', '', content)
    content = re.sub(r'StartEmuleProgressTimer\(\);?\s*\n', '', content)
    content = re.sub(r'StopEmuleProgressTimer\(\);?\s*\n', '', content)
    
    # 11. Limpiar líneas vacías múltiples
    content = re.sub(r'\n\s*\n\s*\n+', '\n\n', content)
    
    # 12. Limpiar comentarios de eMule
    content = re.sub(r'//.*[Ee][Mm]ule.*\n', '', content)
    content = re.sub(r'/\*\*.*[Ee][Mm]ule.*?\*/', '', content, flags=re.DOTALL)
    
    final_lines = len(content.split('\n'))
    
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write(content)
    
    print(f"✅ Eliminación completada")
    print(f"   Líneas originales: {original_lines}")
    print(f"   Líneas finales: {final_lines}")
    print(f"   Líneas eliminadas: {original_lines - final_lines}")
    print(f"   Archivo guardado en: {output_file}")

if __name__ == '__main__':
    input_file = 'MainForm.cs'
    output_file = 'MainForm.cs.cleaned'
    
    print("🔧 Eliminando referencias a eMule de MainForm.cs...")
    remove_emule_references(input_file, output_file)
    print("\n⚠️  Revisa MainForm.cs.cleaned antes de reemplazar el original")
    print("    Si todo está bien, ejecuta: copy MainForm.cs.cleaned MainForm.cs")
