# Nueva GUI Moderna para SlskDown

## Diseño General

### Layout Principal
```
┌─────────────────────────────────────────────────────────┐
│  SIDEBAR (200px)  │  CONTENT AREA                       │
│                   │  ┌──────────────────────────────┐   │
│  🎵 SlskDown      │  │  TOP BAR (60px)              │   │
│                   │  │  Título        [Conectar]    │   │
│  🔍 Búsqueda ◄    │  └──────────────────────────────┘   │
│  📥 Descargas     │                                      │
│  ⚙️ Config        │  ┌──────────────────────────────┐   │
│  👤 Autores       │  │                              │   │
│  📁 Archivos      │  │  PANEL DINÁMICO              │   │
│  ⭐ Wishlist      │  │  (Cambia según selección)    │   │
│  📊 Historial     │  │                              │   │
│  🤖 Automático    │  │                              │   │
│                   │  │                              │   │
│  ● Desconectado   │  └──────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## Ventajas del Nuevo Diseño

1. **Navegación Clara**: Sidebar fijo con todas las secciones visibles
2. **Espacio Optimizado**: Sin pestañas, más espacio para contenido
3. **Diseño Moderno**: Colores oscuros, bordes redondeados, espaciado limpio
4. **Responsive**: TableLayoutPanel asegura que todo se ajusta correctamente
5. **Sin Superposiciones**: FlowLayoutPanel para controles horizontales

## Colores del Tema

- **Fondo Principal**: #121212 (18, 18, 18)
- **Fondo Cards**: #1E1E1E (30, 30, 30)
- **Acento Azul**: #0078D7 (0, 120, 215)
- **Acento Verde**: #00C864 (0, 200, 100)
- **Texto Principal**: #FFFFFF (255, 255, 255)
- **Texto Secundario**: #B4B4B4 (180, 180, 180)

## Estructura de Paneles

### Panel de Búsqueda
- **Barra de búsqueda** (60px): Campo de texto + Botones
- **Barra de filtros** (45px): Tamaño, Tipo, etc.
- **Resultados** (resto): ListView con resultados

### Panel de Descargas
- **Barra de botones** (70px): Limpiar, Reintentar, Pausar, Reanudar
- **Lista de descargas** (resto): ListView con progreso

### Panel de Configuración
- **Card con campos**: Usuario, Contraseña, Carpeta
- **Scroll automático**: Para más opciones

## Implementación

La nueva GUI está en `UI/ModernMainForm.cs` y usa:
- TableLayoutPanel para layout principal
- FlowLayoutPanel para barras de botones
- ModernCard para paneles con bordes redondeados
- ModernButton para botones con estilo
- ModernListView para listas

## Próximos Pasos

1. Integrar la lógica existente de MainForm.cs en ModernMainForm.cs
2. Migrar todos los event handlers
3. Conectar con el cliente de Soulseek
4. Probar y ajustar
