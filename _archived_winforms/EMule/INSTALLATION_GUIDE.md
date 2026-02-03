# Guía de Instalación y Configuración de aMule

Esta guía te ayudará a instalar y configurar aMule daemon para integrarlo con SlskDown.

## Requisitos Previos

- Sistema operativo: Linux, macOS o Windows
- Permisos de administrador para instalación
- Puerto TCP disponible (default: 4712 para EC)

## Instalación

### Linux (Debian/Ubuntu)

```bash
# Actualizar repositorios
sudo apt-get update

# Instalar aMule daemon
sudo apt-get install amule-daemon

# Verificar instalación
which amuled
# Debería mostrar: /usr/bin/amuled
```

### Linux (Fedora/RHEL)

```bash
# Instalar aMule daemon
sudo dnf install amule

# O desde repositorios EPEL
sudo dnf install epel-release
sudo dnf install amule
```

### macOS

```bash
# Usando Homebrew
brew install amule

# Verificar instalación
which amuled
```

### Windows

1. Descargar aMule desde: https://www.amule.org/
2. Ejecutar el instalador
3. Seleccionar "aMule Daemon" durante la instalación
4. Ubicación típica: `C:\Program Files\aMule\amuled.exe`

## Configuración Inicial

### 1. Primera Ejecución

```bash
# Ejecutar amuled por primera vez para generar archivos de configuración
amuled

# Esperar 5 segundos y detener (Ctrl+C)
```

Esto creará el directorio de configuración:
- Linux/macOS: `~/.aMule/`
- Windows: `%APPDATA%\aMule\`

### 2. Configurar External Connections (EC)

Editar el archivo `amule.conf`:

```bash
# Linux/macOS
nano ~/.aMule/amule.conf

# Windows
notepad %APPDATA%\aMule\amule.conf
```

Buscar la sección `[ExternalConnect]` y configurar:

```ini
[ExternalConnect]
AcceptExternalConnections=1
ECAddress=127.0.0.1
ECPort=4712
ECPassword=<TU_HASH_MD5>
```

### 3. Generar Hash MD5 de Contraseña

La contraseña EC debe estar en formato MD5. Usa uno de estos métodos:

#### Linux/macOS:
```bash
echo -n "tu_contraseña" | md5sum | cut -d ' ' -f 1
```

#### Windows (PowerShell):
```powershell
$password = "tu_contraseña"
$md5 = [System.Security.Cryptography.MD5]::Create()
$hash = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($password))
[BitConverter]::ToString($hash).Replace("-","").ToLower()
```

#### Online:
Visita: https://www.md5hashgenerator.com/

**Ejemplo:**
- Contraseña: `mipassword123`
- MD5: `5f4dcc3b5aa765d61d8327deb882cf99`

Copia el hash MD5 en `ECPassword=` en `amule.conf`.

### 4. Configurar Servidores ed2k (Opcional)

Editar `~/.aMule/server.met` o descargar lista actualizada:

```bash
# Descargar lista de servidores
wget -O ~/.aMule/server.met "http://www.gruk.org/server.met"
```

### 5. Habilitar Kad (Recomendado)

En `amule.conf`, asegurar:

```ini
[Kad]
KadEnabled=1
```

## Iniciar aMule Daemon

### Modo Foreground (para testing)

```bash
amuled
```

### Modo Background (producción)

```bash
# Linux/macOS
amuled -f

# O como servicio systemd (Linux)
sudo systemctl enable amule-daemon
sudo systemctl start amule-daemon
```

### Windows

Ejecutar `amuled.exe` desde:
```
C:\Program Files\aMule\amuled.exe
```

O crear un servicio con NSSM:
```cmd
nssm install aMule "C:\Program Files\aMule\amuled.exe"
nssm start aMule
```

## Verificar Conexión

### Usando amulecmd (CLI)

```bash
# Conectar a daemon
amulecmd -h localhost -p 4712 -P tu_contraseña

# Comandos útiles:
> status          # Ver estado de conexión
> stats           # Estadísticas
> search test     # Búsqueda de prueba
> exit
```

### Usando amuleweb (Web UI)

1. Abrir navegador: http://localhost:4711
2. Usuario: `admin`
3. Contraseña: la misma que configuraste en EC

## Configuración para SlskDown

Una vez que amuled esté corriendo, configurar SlskDown:

```csharp
var emuleClient = new EMuleClient
{
    Config = new EMuleConfig
    {
        AmuleDaemonPath = "/usr/bin/amuled",  // Ruta a amuled
        ManageDaemon = false,                  // false si ya está corriendo
        EnableKad = true,                      // Habilitar red Kad
        ECPort = 4712                          // Puerto EC
    }
};

var credentials = new NetworkCredentials
{
    Server = "127.0.0.1",
    Port = 4712,
    Password = "tu_contraseña"  // Contraseña en texto plano (se hasheará automáticamente)
};

await emuleClient.ConnectAsync(credentials);
```

## Troubleshooting

### Error: "Connection refused"

**Causa:** amuled no está corriendo o el puerto EC está bloqueado.

**Solución:**
```bash
# Verificar si amuled está corriendo
ps aux | grep amuled

# Verificar puerto
netstat -tuln | grep 4712

# Reiniciar daemon
killall amuled
amuled -f
```

### Error: "Authentication failed"

**Causa:** Contraseña EC incorrecta.

**Solución:**
1. Verificar hash MD5 en `amule.conf`
2. Regenerar hash con la contraseña correcta
3. Reiniciar amuled

### Error: "AcceptExternalConnections disabled"

**Causa:** EC no está habilitado en configuración.

**Solución:**
```bash
# Editar amule.conf
nano ~/.aMule/amule.conf

# Cambiar a:
AcceptExternalConnections=1

# Reiniciar
killall amuled && amuled -f
```

### No se conecta a servidores ed2k

**Causa:** Lista de servidores desactualizada o vacía.

**Solución:**
```bash
# Descargar lista actualizada
wget -O ~/.aMule/server.met "http://www.gruk.org/server.met"

# Reiniciar daemon
killall amuled && amuled -f
```

### Kad no se conecta

**Causa:** Falta archivo nodes.dat o está corrupto.

**Solución:**
```bash
# Descargar nodes.dat
wget -O ~/.aMule/nodes.dat "http://www.nodes-dat.com/dl.php?load=nodes&trace=39513030.3240"

# Reiniciar
killall amuled && amuled -f
```

## Logs y Debugging

### Ver logs de amuled

```bash
# Linux/macOS
tail -f ~/.aMule/logfile

# Ver solo errores
grep -i error ~/.aMule/logfile
```

### Habilitar debug en amule.conf

```ini
[eMule]
VerboseLevel=3
```

Reiniciar daemon para aplicar cambios.

## Seguridad

### Recomendaciones

1. **Contraseña fuerte:** Usa una contraseña compleja para EC
2. **Bind local:** Mantén `ECAddress=127.0.0.1` para acceso solo local
3. **Firewall:** Si necesitas acceso remoto, configura firewall:
   ```bash
   sudo ufw allow from 192.168.1.0/24 to any port 4712
   ```
4. **No exponer a Internet:** EC no tiene cifrado, solo usar en red local

## Próximos Pasos

Una vez configurado amuled:

1. Ejecutar tests de integración:
   ```bash
   cd SlskDown/EMule/Tests
   dotnet run
   ```

2. Verificar conexión desde SlskDown

3. Probar búsquedas básicas

4. Integrar en UI principal

## Referencias

- [aMule Wiki](https://wiki.amule.org/)
- [External Connections](https://wiki.amule.org/wiki/External_Connections)
- [EC Protocol](https://wiki.amule.org/wiki/EC_Protocol_HOWTO)
- [FAQ aMule](https://wiki.amule.org/wiki/FAQ)

## Soporte

Si encuentras problemas:

1. Revisar logs de amuled
2. Verificar configuración EC
3. Probar con amulecmd primero
4. Consultar documentación oficial de aMule
