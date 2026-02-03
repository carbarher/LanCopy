# Ajustes de Layout Pendientes

## Problema Identificado
Los controles dentro de ModernCard se desplazan debido al Padding automático.

## Solución Aplicada
- ✅ Eliminado Padding(15) de ModernCard → Padding(0)
- ✅ Ajustadas posiciones en pestaña de Búsqueda
- ✅ Ajustado buttonPanel en pestaña de Descargas

## Pestañas por Revisar y Ajustar

### 1. Config Tab
- Verificar que todos los controles sean visibles
- Ajustar posiciones si es necesario

### 2. Authors Tab
- Verificar layout de ListView y botones

### 3. Files Tab (Archivos)
- Verificar que lvFiles se vea correctamente
- Ajustar posiciones de botones

### 4. Wishlist Tab
- Verificar layout de controles

### 5. Calibre Tab
- Verificar integración con Calibre
- Ajustar posiciones de botones

### 6. History Tab (Historial)
- Verificar ListView de historial

### 7. Auto Tab (Automático)
- Verificar controles de búsqueda automática

### 8. Log Tab
- Verificar TextBox de log

## Tamaños de Referencia
- Formulario: 1100x700
- Área útil de pestaña: ~1080x620
- ModernCard típico: 1060 ancho, altura variable
- Botones ModernButton: 80-150 ancho, 34-40 alto
