# 📊 Cómo Aumentar Límites en SlskDown

## Pasos:

1. **Abrir SlskDown**
   - Ejecuta: `c:\p2p\slsk.bat`

2. **Ir a pestaña ⚙️ Configuración**
   - Es la última pestaña a la derecha

3. **Ajustar valores:**

   ```
   Response Limit:  5000  →  10000
   File Limit:      50000 →  100000
   Search Timeout:  30s   →  60s
   ```

4. **Hacer clic en 💾 Guardar**

## Beneficios:

- ✅ **Más resultados** por búsqueda (10,000 en lugar de 5,000)
- ✅ **Más archivos** procesados (100,000 en lugar de 50,000)
- ✅ **Menos timeouts** en redes lentas (60s en lugar de 30s)

## Recomendaciones:

- Si tu PC es lento, usa valores intermedios (7500, 75000, 45s)
- Si tienes buena conexión, puedes subir aún más (15000, 150000, 90s)
- Monitorea el uso de RAM en el Administrador de Tareas

## Valores por tipo de conexión:

### Conexión lenta (<10 Mbps):
- Response Limit: 3000
- File Limit: 30000
- Timeout: 90s

### Conexión media (10-50 Mbps):
- Response Limit: 10000
- File Limit: 100000
- Timeout: 60s

### Conexión rápida (>50 Mbps):
- Response Limit: 20000
- File Limit: 200000
- Timeout: 45s
