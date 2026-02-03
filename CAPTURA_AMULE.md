# Captura de paquetes aMule EC con Wireshark

## Pasos para capturar:

1. **Abre Wireshark**
2. **Selecciona la interfaz de loopback** (Adapter for loopback traffic capture)
3. **Inicia la captura** (botón azul de tiburón)
4. **Aplica el filtro:** `tcp.port == 4712`
5. **Conecta un cliente a aMule:**
   - Opción A: Usa `amuleweb` (si está instalado)
   - Opción B: Usa `amulecmd` (si está instalado)
   - Opción C: Usa nuestro test y captura lo que aMule espera ver

## Para usar amulecmd:

```cmd
cd C:\amule
amulecmd -h localhost -p 4712 -P Carlos66*
```

## Qué buscar en Wireshark:

1. **Paquete TCP con datos del cliente → aMule**
   - Este es el paquete AUTH_REQ
   - Clic derecho → Follow → TCP Stream
   - Verás el hex completo del paquete

2. **Compara con nuestro paquete:**
   ```
   Nuestro: 00 00 00 22 00 00 00 22 02 04 82 00 06 09 53 6C 73 6B 44 6F 77 6E 00 82 02 06 07 30 78 30 30 30 31 00 08 03 02 00 02 10 00 00
   Real:    [lo que capture Wireshark]
   ```

3. **Identifica las diferencias** y ajusta el código

## Si no tienes amulecmd/amuleweb:

Busca en `C:\amule\` los ejecutables:
- `amulecmd.exe`
- `amuleweb.exe`
- `amulegui.exe`

Si no existen, necesitas descargar la versión completa de aMule que incluya estas herramientas.
