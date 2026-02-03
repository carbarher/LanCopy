#!/usr/bin/env python3
"""
Script para eliminar métodos y variables duplicados en MainForm.cs
"""

import re

# Leer el archivo
with open('MainForm.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Buscar duplicados
duplicates_found = {}
methods_to_remove = []

# Patrones a buscar
patterns = [
    (r'^\s*private bool autoSearchRunning\s*=', 'autoSearchRunning'),
    (r'^\s*private bool autoPurgeRunning\s*=', 'autoPurgeRunning'),
    (r'^\s*private CancellationTokenSource\?\s*autoSearchCts', 'autoSearchCts'),
    (r'^\s*private int batchSize\s*=', 'batchSize'),
    (r'^\s*private int sortColumn\s*=', 'sortColumn'),
    (r'^\s*private bool sortAscending\s*=', 'sortAscending'),
    (r'^\s*private async Task StartAutomaticSearch\(', 'StartAutomaticSearch'),
    (r'^\s*private void CreateLogTab\(', 'CreateLogTab'),
    (r'^\s*private void Log\(string message\)', 'Log'),
    (r'^\s*private async Task<string\?> GetCountryAsync\(', 'GetCountryAsync'),
    (r'^\s*private bool IsHispanic\(', 'IsHispanic'),
    (r'^\s*private void SaveCountryCache\(', 'SaveCountryCache'),
    (r'^\s*private void UpdateSelectionCount\(', 'UpdateSelectionCount'),
    (r'^\s*private bool MatchesCategory\(', 'MatchesCategory'),
    (r'^\s*private Color GetQualityColor\(', 'GetQualityColor'),
    (r'^\s*private string GetFileIcon\(', 'GetFileIcon'),
    (r'^\s*private Dictionary<string, List<string>> DetectSeries\(', 'DetectSeries'),
    (r'^\s*private Dictionary<string, List<SearchResult>> GroupBySeries\(', 'GroupBySeries'),
    (r'^\s*private void ShowSeriesDetection\(', 'ShowSeriesDetection'),
    (r'^\s*private async Task AnalyzeUserCollection\(', 'AnalyzeUserCollection'),
    (r'^\s*private void SortResults\(', 'SortResults'),
    (r'^\s*private void OpenDownloadedFile\(', 'OpenDownloadedFile'),
    (r'^\s*private void CreateAuthorsTab\(', 'CreateAuthorsTab'),
    (r'^\s*private void CreateWatchlistTab\(', 'CreateWatchlistTab'),
    (r'^\s*private void SaveAuthors\(', 'SaveAuthors'),
    (r'^\s*private void LoadAuthors\(', 'LoadAuthors'),
    (r'^\s*private void SaveWatchlist\(', 'SaveWatchlist'),
    (r'^\s*private async Task CheckWatchlist\(', 'CheckWatchlist'),
    (r'^\s*private async Task RetryDownload\(', 'RetryDownload'),
    (r'^\s*private string FormatDuration\(', 'FormatDuration'),
    (r'^\s*private void FilterResults\(', 'FilterResults'),
    (r'^\s*private void ExportToCSV\(', 'ExportToCSV'),
    (r'^\s*private async Task TestConnection\(', 'TestConnection'),
    (r'^\s*private void TabControl_DrawItem\(', 'TabControl_DrawItem'),
    (r'^\s*private async Task SearchMultipleTerms\(', 'SearchMultipleTerms'),
    (r'^\s*private void SaveStats\(', 'SaveStats'),
    (r'^\s*private void LoadStats\(', 'LoadStats'),
    (r'^\s*private async Task CheckAndReconnect\(', 'CheckAndReconnect'),
    (r'^\s*private void LvResults_DrawColumnHeader\(', 'LvResults_DrawColumnHeader'),
    (r'^\s*private void LvResults_DrawItem\(', 'LvResults_DrawItem'),
    (r'^\s*private void LvResults_DrawSubItem\(', 'LvResults_DrawSubItem'),
]

# Encontrar todas las ocurrencias
for i, line in enumerate(lines):
    for pattern, name in patterns:
        if re.match(pattern, line):
            if name not in duplicates_found:
                duplicates_found[name] = []
            duplicates_found[name].append(i + 1)  # Línea 1-indexed

# Mostrar duplicados encontrados
print("Duplicados encontrados:")
for name, line_numbers in duplicates_found.items():
    if len(line_numbers) > 1:
        print(f"  {name}: líneas {line_numbers}")

# Identificar qué líneas mantener (primera ocurrencia) y cuáles eliminar (resto)
lines_to_keep = set(range(len(lines)))

for name, line_numbers in duplicates_found.items():
    if len(line_numbers) > 1:
        # Mantener la primera, eliminar el resto
        first_line = line_numbers[0] - 1  # Convertir a 0-indexed
        
        # Para cada duplicado (excepto el primero), encontrar el rango completo del método
        for dup_line in line_numbers[1:]:
            dup_idx = dup_line - 1
            
            # Buscar el final del método/variable
            if 'private' in lines[dup_idx] and '(' in lines[dup_idx]:
                # Es un método - buscar hasta la llave de cierre
                brace_count = 0
                start_idx = dup_idx
                end_idx = dup_idx
                
                # Encontrar la llave de apertura
                for j in range(dup_idx, min(dup_idx + 10, len(lines))):
                    if '{' in lines[j]:
                        brace_count = 1
                        end_idx = j
                        break
                
                # Encontrar la llave de cierre
                if brace_count > 0:
                    for j in range(end_idx + 1, len(lines)):
                        brace_count += lines[j].count('{')
                        brace_count -= lines[j].count('}')
                        end_idx = j
                        if brace_count == 0:
                            break
                
                # Eliminar el rango completo
                for j in range(start_idx, end_idx + 1):
                    lines_to_keep.discard(j)
                    
                print(f"  Eliminando método {name} duplicado en líneas {start_idx + 1}-{end_idx + 1}")
            else:
                # Es una variable - eliminar solo esa línea
                lines_to_keep.discard(dup_idx)
                print(f"  Eliminando variable {name} duplicada en línea {dup_line}")

# Crear archivo limpio
clean_lines = [lines[i] for i in sorted(lines_to_keep)]

# Guardar backup
with open('MainForm.cs.backup_before_dedup', 'w', encoding='utf-8') as f:
    f.writelines(lines)

# Guardar archivo limpio
with open('MainForm.cs', 'w', encoding='utf-8') as f:
    f.writelines(clean_lines)

print(f"\n✅ Archivo limpiado: {len(lines)} líneas -> {len(clean_lines)} líneas")
print(f"✅ Backup guardado en: MainForm.cs.backup_before_dedup")
