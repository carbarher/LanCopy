# Testing de Integración eMule

Esta guía te ayudará a probar la integración de eMule con SlskDown antes de integrarla en la UI principal.

## Prerrequisitos

1. **aMule daemon instalado y configurado**
   - Seguir [INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)
   - amuled debe estar corriendo
   - Puerto EC (4712) configurado y accesible
   - Contraseña EC configurada

2. **.NET SDK 6.0 o superior**
   ```bash
   dotnet --version
   # Debería mostrar 6.0.x o superior
   ```

## Estructura de Tests

```
EMule/Tests/
├── EMuleClientTests.cs      # Tests de integración
├── run_tests.bat            # Script Windows
├── run_tests.sh             # Script Linux/macOS
└── TESTING_README.md        # Esta guía
```

## Ejecutar Tests

### Windows

```cmd
cd c:\p2p\SlskDown\EMule\Tests
run_tests.bat
```

### Linux/macOS

```bash
cd /path/to/SlskDown/EMule/Tests
chmod +x run_tests.sh
./run_tests.sh
```

### Manual (cualquier plataforma)

```bash
cd SlskDown/EMule/Tests
dotnet run --project EMuleClientTests.csproj
```

## Tests Incluidos

### 1. Test de Conexión
- Verifica que puede conectarse al puerto EC
- Valida que amuled responde
- Comprueba estado de conexión

**Salida esperada:**
```
📡 Test 1: Conexión a aMule daemon
----------------------------------
Conectando a localhost:4712...
   Estado: Disconnected → Connecting
   Estado: Connecting → Connected
✅ Conexión exitosa
   Red: eMule/ed2k
   Estado: Connected
   Uptime: 2s
✅ Desconexión exitosa
```

### 2. Test de Autenticación
- Prueba autenticación con protocolo EC
- Valida hash MD5 de contraseña
- Verifica respuesta EC_OP_AUTH_OK

**Salida esperada:**
```
🔐 Test 2: Autenticación EC
---------------------------
Ingresa la contraseña EC de aMule: ********
Autenticando...
   Estado: Disconnected → Connecting
   Estado: Connecting → Connected
   Estado: Connected → LoggedIn
✅ Autenticación exitosa
   Estado final: LoggedIn
```

### 3. Test de Búsqueda
- Realiza búsqueda en red ed2k/Kad
- Parsea resultados
- Muestra archivos encontrados

**Salida esperada:**
```
🔍 Test 3: Búsqueda básica
--------------------------
Buscando 'machine learning'...
   📄 Machine_Learning_Basics.pdf (2.5 MB)
   📄 Deep_Learning_Tutorial.pdf (5.1 MB)
   📄 AI_Introduction.pdf (1.8 MB)

   Búsqueda Completed: 3 resultados en 12.3s
✅ Búsqueda completada: 3 resultados
```

## Troubleshooting

### Error: "Connection refused"

**Problema:** No puede conectarse al puerto EC.

**Soluciones:**
1. Verificar que amuled está corriendo:
   ```bash
   # Linux/macOS
   ps aux | grep amuled
   
   # Windows
   tasklist | findstr amuled
   ```

2. Verificar puerto EC:
   ```bash
   netstat -tuln | grep 4712  # Linux/macOS
   netstat -an | findstr 4712  # Windows
   ```

3. Reiniciar amuled:
   ```bash
   killall amuled && amuled -f  # Linux/macOS
   ```

### Error: "Authentication failed"

**Problema:** Contraseña EC incorrecta.

**Soluciones:**
1. Verificar contraseña en `~/.aMule/amule.conf`:
   ```ini
   [ExternalConnect]
   ECPassword=<hash_md5>
   ```

2. Regenerar hash MD5:
   ```bash
   echo -n "tu_contraseña" | md5sum | cut -d ' ' -f 1
   ```

3. Actualizar `amule.conf` y reiniciar:
   ```bash
   killall amuled && amuled -f
   ```

### Error: "No results found"

**Problema:** Búsqueda no devuelve resultados.

**Causas posibles:**
1. No conectado a servidores ed2k
2. Kad no iniciado
3. Búsqueda muy específica

**Soluciones:**
1. Verificar conexión a red:
   ```bash
   amulecmd -h localhost -p 4712 -P tu_contraseña
   > status
   ```

2. Conectar a servidores:
   ```bash
   > connect
   ```

3. Iniciar Kad:
   ```bash
   > kad start
   ```

### Error: "Compilation failed"

**Problema:** No puede compilar el proyecto de tests.

**Soluciones:**
1. Verificar .NET SDK:
   ```bash
   dotnet --version
   ```

2. Restaurar dependencias:
   ```bash
   dotnet restore
   ```

3. Limpiar y recompilar:
   ```bash
   dotnet clean
   dotnet build
   ```

## Interpretar Resultados

### ✅ Test Exitoso
- Todos los tests pasan sin errores
- Conexión, autenticación y búsqueda funcionan
- Listo para integrar en UI principal

### ⚠️ Test Parcial
- Conexión y autenticación OK, pero búsqueda falla
- Puede deberse a red ed2k/Kad no conectada
- Verificar estado de red en amuleweb o amulecmd

### ❌ Test Fallido
- No puede conectarse o autenticarse
- Revisar configuración de amuled
- Verificar logs: `~/.aMule/logfile`

## Logs y Debugging

### Ver logs de tests
Los tests imprimen información detallada en consola. Para guardar:

```bash
# Linux/macOS
./run_tests.sh 2>&1 | tee test_results.log

# Windows
run_tests.bat > test_results.log 2>&1
```

### Ver logs de amuled

```bash
# Linux/macOS
tail -f ~/.aMule/logfile

# Windows
type %APPDATA%\aMule\logfile
```

### Habilitar debug en tests

Editar `EMuleClientTests.cs` y añadir logging adicional:

```csharp
client.StateChanged += (sender, e) =>
{
    Console.WriteLine($"[DEBUG] Estado: {e.PreviousState} → {e.CurrentState}");
    if (e.Error != null)
    {
        Console.WriteLine($"[DEBUG] Error: {e.Error.Message}");
    }
};
```

## Próximos Pasos

Una vez que los tests pasen exitosamente:

1. **Fase 3 - Integración UI**
   - Añadir pestaña "eMule" en MainForm
   - Reutilizar componentes de grilla virtualizada
   - Integrar logs con sistema existente

2. **Fase 4 - Orquestación Multi-Red**
   - Búsquedas paralelas Soulseek + eMule
   - Deduplicación de resultados
   - Priorización de fuentes

3. **Fase 5 - Refinamiento**
   - Optimización de rendimiento
   - Métricas y telemetría
   - Dashboard consolidado

## Reportar Problemas

Si encuentras bugs o problemas:

1. Capturar salida completa de tests
2. Incluir logs de amuled
3. Especificar:
   - Sistema operativo
   - Versión de .NET
   - Versión de aMule
   - Configuración EC (puerto, etc.)

## Referencias

- [Guía de Instalación aMule](INSTALLATION_GUIDE.md)
- [Plan de Integración](../EMULE_INTEGRATION_PLAN.md)
- [Protocolo EC](https://wiki.amule.org/wiki/EC_Protocol_HOWTO)
- [aMule Wiki](https://wiki.amule.org/)
