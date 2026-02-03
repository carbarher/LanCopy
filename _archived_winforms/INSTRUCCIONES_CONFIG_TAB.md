# Instrucciones para reorganizar CreateConfigTab

## Problema
Los controles en la pestaña de Configuración se pisan porque usan posiciones absolutas (Location).

## Solución
Reemplazar el método `CreateConfigTab` en `MainForm.cs` (líneas 769-1100 aproximadamente) con el código del archivo `CreateConfigTab_NEW.txt`.

## Pasos:

1. Abre `MainForm.cs` en tu editor
2. Busca el método `private void CreateConfigTab(TabPage parent)` (línea ~769)
3. Selecciona TODO el método hasta el cierre `}` antes de `private async Task ConnectToSoulseek()` (línea ~1100)
4. Borra todo ese código
5. Copia el contenido de `CreateConfigTab_NEW.txt` (SOLO el método CreateConfigTab, NO los métodos helper)
6. Pégalo en el lugar donde estaba el método anterior
7. Guarda el archivo
8. Compila con: `dotnet build SlskDown.csproj -c Release`
9. Ejecuta: `bin\Release\net8.0-windows\SlskDown.exe`

## Nota
Los métodos helper `CreateConfigSection` y `CreateConfigRow` ya están agregados al final del archivo (después de la línea 1100), así que NO necesitas copiarlos de nuevo.

## Resultado
La pestaña de Configuración tendrá:
- Scroll vertical automático
- Controles organizados en secciones claras
- Sin superposiciones
- Mejor espaciado y legibilidad
