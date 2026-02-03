# Plan de Integración eMule en SlskDown

## Objetivo
Integrar la red eMule/ed2k en paralelo con Soulseek para maximizar la cobertura de libros disponibles, manteniendo Soulseek completamente operativo sin interrupciones.

## Arquitectura Implementada

### 1. Interfaces Abstractas (Core/)
- **INetworkClient**: Interfaz común para clientes P2P
  - Gestión de conexión/desconexión
  - Estados de red unificados
  - Estadísticas comunes
  
- **ISearchProvider**: Interfaz para proveedores de búsqueda
  - Búsquedas asíncronas con filtros
  - Eventos de resultados y progreso
  - Cancelación de búsquedas

### 2. Implementación eMule (EMule/)
- **EMuleClient**: Cliente que implementa INetworkClient
  - Gestiona proceso amuled (daemon)
  - Conexión al puerto EC (External Connections)
  - Autenticación mediante protocolo EC
  - Conexión a redes ed2k y Kad

- **ECProtocol**: Implementación del protocolo EC de aMule
  - Codificación/decodificación de paquetes
  - Soporte para UTF-8 compressed numbers
  - Tipos de datos EC (tags, opcodes)

- **EMuleSearchProvider**: Proveedor de búsqueda eMule
  - Implementa ISearchProvider
  - Traduce búsquedas a comandos EC
  - Parsea resultados de la red ed2k/Kad

## Fases de Implementación

### ✅ Fase 1: Proof of Concept (COMPLETADA)
- [x] Investigar protocolo EC de aMule
- [x] Diseñar interfaces abstractas
- [x] Implementar cliente básico eMule
- [x] Implementar protocolo EC
- [x] Crear proveedor de búsqueda

### ✅ Fase 2: Integración y Testing (COMPLETADA)
- [x] Exponer métodos públicos en EMuleClient (SendECPacketAsync, ReceiveECPacketAsync)
- [x] Crear tests de integración para protocolo EC
- [x] Documentar instalación y configuración de amuled
- [x] Crear scripts de testing (run_tests.bat/sh)
- [x] Guía completa de troubleshooting
- [ ] Probar conexión a amuled local (requiere instalación manual)
- [ ] Validar autenticación EC (requiere instalación manual)
- [ ] Probar búsquedas básicas (requiere instalación manual)

### 🔄 Fase 2.5: Orquestación Multi-Red (EN PROGRESO)
- [x] Crear SoulseekClientAdapter (implementa INetworkClient)
- [x] Crear SoulseekSearchProvider (implementa ISearchProvider)
- [x] Crear NetworkOrchestrator para gestión multi-red
- [x] Implementar búsquedas paralelas
- [x] Implementar deduplicación de resultados
- [x] Sistema de priorización de fuentes
- [ ] Integrar en MainForm
- [ ] Verificar que Soulseek sigue funcionando sin cambios

### 📋 Fase 3: Integración UI (PENDIENTE)
- [ ] Añadir pestaña "eMule" en MainForm
- [ ] Reutilizar componentes de grilla virtualizada
- [ ] Mostrar estado de conexión eMule
- [ ] Mostrar resultados de búsqueda eMule
- [ ] Integrar logs de eMule con sistema existente
- [ ] Añadir configuración eMule (puerto EC, contraseña, etc.)

### 📋 Fase 4: Orquestación Multi-Red (PENDIENTE)
- [ ] Crear NetworkOrchestrator para gestionar múltiples redes
- [ ] Implementar búsquedas paralelas (Soulseek + eMule)
- [ ] Deduplicar resultados entre redes
- [ ] Priorizar fuentes según disponibilidad
- [ ] Gestionar ancho de banda compartido
- [ ] Políticas de failover entre redes

### 📋 Fase 5: Refinamiento (PENDIENTE)
- [ ] Optimizar rendimiento de búsquedas
- [ ] Implementar caché de resultados
- [ ] Añadir métricas y telemetría
- [ ] Dashboard consolidado multi-red
- [ ] Documentación de usuario

## Requisitos Previos

### Software Necesario
1. **aMule daemon** (amuled)
   - Linux: `sudo apt-get install amule-daemon`
   - Windows: Descargar desde https://www.amule.org/
   - Configurar puerto EC (default: 4712)
   - Establecer contraseña EC en `~/.aMule/amule.conf`

2. **Configuración aMule**
   ```ini
   [ExternalConnect]
   AcceptExternalConnections=1
   ECPort=4712
   ECPassword=<MD5_HASH_DE_TU_CONTRASEÑA>
   ```

### Dependencias .NET
- System.Net.Sockets (ya incluido)
- System.Security.Cryptography (ya incluido)

## Uso Básico

```csharp
// Crear cliente eMule
var emuleClient = new EMuleClient
{
    Config = new EMuleConfig
    {
        AmuleDaemonPath = "/usr/bin/amuled",
        ManageDaemon = true,
        EnableKad = true,
        ECPort = 4712
    }
};

// Conectar
var credentials = new NetworkCredentials
{
    Server = "127.0.0.1",
    Port = 4712,
    Password = "tu_contraseña_ec"
};

await emuleClient.ConnectAsync(credentials);

// Crear proveedor de búsqueda
var searchProvider = new EMuleSearchProvider(emuleClient);

// Buscar
var request = new SearchRequest
{
    Query = "machine learning",
    Filters = new SearchFilters
    {
        FileType = FileType.Document,
        MinSizeBytes = 1024 * 1024 // 1 MB
    },
    MaxResults = 100,
    Timeout = TimeSpan.FromSeconds(30)
};

var response = await searchProvider.SearchAsync(request);

foreach (var result in response.Results)
{
    Console.WriteLine($"{result.FileName} - {result.SizeBytes} bytes - {result.NetworkSource}");
}

// Desconectar
await emuleClient.DisconnectAsync();
```

## Puntos de No-Regresión

### Garantías de Compatibilidad
1. **Soulseek sigue funcionando**: Toda la lógica existente permanece intacta
2. **Sin cambios en MainForm.cs**: Nuevas funcionalidades en archivos separados
3. **Feature flags**: eMule puede deshabilitarse completamente
4. **Logs separados**: eMule tiene su propio sistema de logging
5. **Configuración independiente**: Settings de eMule no afectan Soulseek

### Tests de Smoke
- [ ] Soulseek conecta sin errores
- [ ] Búsquedas Soulseek funcionan normalmente
- [ ] Descargas Soulseek no se interrumpen
- [ ] UI Soulseek responde correctamente
- [ ] Métricas Soulseek se registran

## Próximos Pasos Inmediatos

1. **Corregir EMuleClient**:
   - Hacer públicos `SendECPacketAsync` y `ReceiveECPacketAsync`
   - Eliminar extensiones temporales en EMuleSearchProvider

2. **Testing Local**:
   - Instalar amuled
   - Configurar puerto EC y contraseña
   - Ejecutar test de conexión
   - Validar autenticación

3. **Documentación**:
   - Guía de instalación aMule
   - Configuración paso a paso
   - Troubleshooting común

## Referencias

- [Protocolo EC de aMule](https://wiki.amule.org/wiki/EC_Protocol_HOWTO)
- [External Connections](https://wiki.amule.org/wiki/External_Connections)
- [ECCodes.abstract](https://github.com/amule-project/amule/blob/master/src/libs/ec/abstracts/ECCodes.abstract)
- [aMule Documentation](https://wiki.amule.org/)

## Notas de Desarrollo

### Decisiones de Diseño
- **Wrapper sobre amuled**: Menor riesgo que portar protocolos ed2k/Kad completos
- **Interfaces abstractas**: Facilita añadir más redes en el futuro (BitTorrent, Gnutella, etc.)
- **Separación estricta**: Namespace `SlskDown.EMule` completamente aislado
- **Reutilización UI**: Componentes virtualizados existentes sirven para ambas redes

### Limitaciones Conocidas
- Requiere amuled instalado externamente (por ahora)
- Protocolo EC puede cambiar entre versiones de aMule
- No todas las features de eMule están expuestas vía EC

### Mejoras Futuras
- Embeber amuled como librería nativa
- Implementar protocolo ed2k/Kad directamente en C#
- Soporte para más redes (BitTorrent, Gnutella)
- Búsquedas federadas con ranking inteligente
