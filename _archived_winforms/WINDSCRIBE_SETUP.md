# 🌐 Instalación y Configuración de Windscribe VPN

## 📥 Paso 1: Descargar e Instalar

1. **Descargar Windscribe**:
   - Ve a: https://windscribe.com/download
   - Descarga la versión para Windows
   - O descarga directamente: https://windscribe.com/install/desktop/windows

2. **Instalar**:
   - Ejecuta el instalador
   - Sigue las instrucciones
   - **IMPORTANTE**: Durante la instalación, asegúrate de que se instale el CLI

3. **Crear Cuenta Gratuita**:
   - Abre Windscribe
   - Click en "Get Started"
   - Crea cuenta (email opcional)
   - **Con email confirmado**: 10GB/mes gratis
   - **Sin email**: 2GB/mes gratis

## 🔧 Paso 2: Verificar CLI

Abre CMD o PowerShell y ejecuta:

```bash
windscribe --help
```

Si ves la ayuda, el CLI está instalado correctamente.

**Ubicación típica del CLI**:
- `C:\Program Files\Windscribe\windscribe-cli.exe`
- `C:\Program Files (x86)\Windscribe\windscribe-cli.exe`

## 🎯 Paso 3: Comandos Básicos

### Login (primera vez)
```bash
windscribe login
```
Ingresa tu usuario y contraseña.

### Conectar
```bash
# Conectar al mejor servidor
windscribe connect

# Conectar a país específico
windscribe connect US
windscribe connect ES
windscribe connect FR
windscribe connect DE
```

### Ver ubicaciones disponibles
```bash
windscribe locations
```

### Ver estado
```bash
windscribe status
```

### Desconectar
```bash
windscribe disconnect
```

## ✅ Paso 4: Probar Manualmente

Antes de que SlskDown lo use automáticamente, prueba manualmente:

```bash
# 1. Conectar
windscribe connect US

# 2. Verificar IP
curl https://api.ipify.org

# 3. Cambiar país
windscribe connect ES

# 4. Verificar nueva IP
curl https://api.ipify.org

# 5. Desconectar
windscribe disconnect
```

## 🚀 Paso 5: Integración con SlskDown

Una vez instalado y probado, SlskDown detectará automáticamente Windscribe y lo usará para cambiar IP cuando detecte bloqueo.

**Logs que verás**:
```
🔍 Windscribe CLI detectado en: C:\Program Files\Windscribe\windscribe-cli.exe
🌐 IP actual: 79.155.224.30
🚨 BLOQUEO DETECTADO: 3 timeouts consecutivos
🔄 Cambiando IP con Windscribe...
✅ Conectado a: US
🌐 Nueva IP: 192.168.1.100
🔄 Reintentando conexión con nueva IP...
```

## 💡 Tips

1. **Primer uso**: Asegúrate de hacer login manual la primera vez
2. **Países recomendados**: US, ES, FR, DE, NL (más servidores)
3. **Límite de datos**: 10GB/mes con email confirmado
4. **Velocidad**: Cambio de IP en ~3-5 segundos

## 🐛 Troubleshooting

### "windscribe: command not found"
- Verifica que Windscribe esté instalado
- Busca `windscribe-cli.exe` en Program Files
- Agrega la ruta al PATH de Windows

### "Login required"
- Ejecuta: `windscribe login`
- Ingresa credenciales

### "No data left"
- Has usado los 10GB del mes
- Espera al siguiente mes o actualiza a plan pago

## 📊 Monitoreo de Uso

Ver cuántos datos has usado:
```bash
windscribe account
```

---

**¡Listo!** Una vez instalado, SlskDown lo usará automáticamente.
