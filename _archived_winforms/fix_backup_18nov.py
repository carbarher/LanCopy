#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Script para arreglar el backup del 18/11 comentando código incompatible
"""

import re

# Leer el archivo
with open('MainForm.cs.backup_20251118_153012', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Procesar línea por línea para mejor control
for i, line in enumerate(lines):
    # DownloadStatus.Pending -> DownloadStatus.Queued
    line = line.replace('DownloadStatus.Pending', 'DownloadStatus.Queued')
    
    # DownloadPriority.Urgent -> DownloadPriority.High
    line = line.replace('DownloadPriority.Urgent', 'DownloadPriority.High')
    
    # Comentar líneas con task.Priority o DownloadTask.Priority
    if 'task.Priority' in line or 'DownloadTask.Priority' in line:
        if not line.strip().startswith('//'):
            line = line.replace(line.lstrip(), '// ' + line.lstrip())
    
    # TimeSpan?.TotalSeconds -> (TimeSpan?.HasValue ? TimeSpan?.Value.TotalSeconds : 0)
    # Solo para TimeSpan nullable (?)
    if '?.Value.TotalSeconds' in line:
        # Encontrar el nombre de la variable antes de ?.Value.TotalSeconds
        match = re.search(r'(\w+)\?\.Value\.TotalSeconds', line)
        if match:
            var_name = match.group(1)
            line = line.replace(f'{var_name}?.Value.TotalSeconds', 
                              f'({var_name}?.HasValue ? {var_name}.Value.TotalSeconds : 0)')
    
    # Quitar .Value de TimeSpan que NO son nullable
    # Buscar patrones como (elapsed).Value.TotalSeconds donde elapsed es TimeSpan (no nullable)
    line = re.sub(r'\((\w+)\)\.Value\.TotalSeconds', r'(\1).TotalSeconds', line)
    
    # PerformanceMetrics.Instance -> comentar toda la línea using
    if 'PerformanceMetrics.Instance' in line and 'using' in line:
        line = line.replace('using (', '// using (')
    
    # PurgeAuthorsWithoutResultsOptimized -> PurgeAuthorsWithoutResults
    line = line.replace('PurgeAuthorsWithoutResultsOptimized', 'PurgeAuthorsWithoutResults')
    
    # QueuePrioritizationStrategy.RarestFirst -> QueuePrioritizationStrategy.Sequential
    line = line.replace('QueuePrioritizationStrategy.RarestFirst', 'QueuePrioritizationStrategy.Sequential')
    line = line.replace('QueuePrioritizationStrategy.FIFO', 'QueuePrioritizationStrategy.Sequential')
    
    lines[i] = line

# Guardar
with open('MainForm.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print("✅ Archivo arreglado y guardado como MainForm.cs")
print("Ahora ejecuta: dotnet build SlskDown.csproj -c Release")
