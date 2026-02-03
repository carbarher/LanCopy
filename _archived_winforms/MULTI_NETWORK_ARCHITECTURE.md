# Arquitectura Multi-Red de SlskDown

## Visión General

SlskDown ahora soporta múltiples redes P2P de forma simultánea mediante una arquitectura modular y extensible. Actualmente soporta:

- **Soulseek**: Red principal, especializada en música y contenido cultural
- **eMule/ed2k**: Red secundaria con gran catálogo de libros y documentos

## Componentes Principales

### 1. Interfaces Abstractas (Core/)

#### INetworkClient
Interfaz común para todos los clientes de redes P2P.

```csharp
public interface INetworkClient : IDisposable
{
    string NetworkName { get; }
    NetworkConnectionState State { get; }
    bool IsConnected { get; }
    
    event EventHandler<NetworkStateChangedEventArgs> StateChanged;
    
    Task ConnectAsync(NetworkCredentials credentials, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    NetworkStatistics GetStatistics();
}
```

**Implementaciones:**
- `SoulseekClientAdapter`: Envuelve el cliente Soulseek existente
- `EMuleClient`: Cliente nativo para eMule/ed2k

#### ISearchProvider
Interfaz para proveedores de búsqueda en redes P2P.

```csharp
public interface ISearchProvider
{
    string ProviderName { get; }
    bool IsReady { get; }
    
    event EventHandler<SearchResultsReceivedEventArgs> ResultsReceived;
    event EventHandler<SearchCompletedEventArgs> SearchCompleted;
    
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
    Task CancelSearchAsync(string searchId);
}
```

**Implementaciones:**
- `SoulseekSearchProvider`: Búsquedas en Soulseek
- `EMuleSearchProvider`: Búsquedas en eMule/ed2k

### 2. Orquestador Multi-Red

#### NetworkOrchestrator
Coordina múltiples redes P2P de forma transparente.

**Funcionalidades:**
- Registro de clientes y proveedores de búsqueda
- Búsquedas paralelas en múltiples redes
- Deduplicación inteligente de resultados
- Priorización automática de fuentes
- Estadísticas consolidadas

**Ejemplo de uso:**

```csharp
// Crear orquestador
var orchestrator = new NetworkOrchestrator();

// Registrar Soulseek
var slskAdapter = new SoulseekClientAdapter(existingSoulseekClient);
var slskProvider = new SoulseekSearchProvider(existingSoulseekClient);
orchestrator.RegisterClient("Soulseek", slskAdapter);
orchestrator.RegisterSearchProvider("Soulseek", slskProvider);

// Registrar eMule
var emuleClient = new EMuleClient();
var emuleProvider = new EMuleSearchProvider(emuleClient);
orchestrator.RegisterClient("eMule", emuleClient);
orchestrator.RegisterSearchProvider("eMule", emuleProvider);

// Conectar ambas redes
await slskAdapter.ConnectAsync(slskCredentials);
await emuleClient.ConnectAsync(emuleCredentials);

// Buscar en ambas redes en paralelo
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

var response = await orchestrator.SearchAsync(request);

// Resultados deduplicados y priorizados
foreach (var result in response.DeduplicatedResults)
{
    Console.WriteLine($"{result.FileName} - {result.NetworkSource}");
}
```

## Flujo de Búsqueda Multi-Red

```
┌─────────────────┐
│   MainForm UI   │
└────────┬────────┘
         │
         ▼
┌─────────────────────────┐
│  NetworkOrchestrator    │
└────────┬────────────────┘
         │
         ├──────────────────┬──────────────────┐
         ▼                  ▼                  ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│ SoulseekSearch   │ │  EMuleSearch     │ │  FutureNetwork   │
│    Provider      │ │    Provider      │ │    Provider      │
└────────┬─────────┘ └────────┬─────────┘ └────────┬─────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│ Soulseek Client  │ │  eMule Client    │ │  Future Client   │
└──────────────────┘ └──────────────────┘ └──────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
    Soulseek Net         ed2k/Kad Net        Other P2P Net
```

## Deduplicación de Resultados

El orquestador deduplica resultados usando múltiples estrategias:

### 1. Normalización de Nombres
```csharp
"Machine Learning - Introduction.pdf"
"Machine_Learning_Introduction.pdf"
"machine.learning.introduction.pdf"
```
Todos se normalizan a: `machinelearningintroduction`

### 2. Priorización por Puntuación

Factores que aumentan la puntuación:
- ✅ Slots libres disponibles (+100 puntos)
- ✅ Bitrate alto (para audio) (+bitrate/1000 puntos)
- ✅ Red Soulseek (+50 puntos, típicamente más rápida)

Factores que reducen la puntuación:
- ❌ Longitud de cola (-2 puntos por usuario en cola)

### 3. Metadata de Fuentes Alternativas

Cuando hay duplicados, el mejor resultado incluye:
```csharp
result.Metadata["AlternativeSources"] = 5; // 5 fuentes alternativas
result.Metadata["Networks"] = "Soulseek, eMule"; // Disponible en ambas redes
```

## Gestión de Estado

### Estados de Conexión

```
Disconnected → Connecting → Connected → LoggedIn
                    ↓            ↓
                  Failed    Reconnecting
```

### Eventos de Estado

```csharp
orchestrator.NetworkStatusChanged += (sender, e) =>
{
    Console.WriteLine($"{e.NetworkName}: {e.State}");
    if (e.Error != null)
    {
        Console.WriteLine($"Error: {e.Error.Message}");
    }
};
```

## Estadísticas Consolidadas

```csharp
var stats = orchestrator.GetStatistics();

Console.WriteLine($"Total descargado: {stats.TotalBytesDownloaded}");
Console.WriteLine($"Total subido: {stats.TotalBytesUploaded}");
Console.WriteLine($"Descargas activas: {stats.TotalActiveDownloads}");
Console.WriteLine($"Peers conectados: {stats.TotalConnectedPeers}");

// Estadísticas por red
foreach (var (network, networkStats) in stats.NetworkStats)
{
    Console.WriteLine($"\n{network}:");
    Console.WriteLine($"  Uptime: {networkStats.Uptime}");
    Console.WriteLine($"  Descargas: {networkStats.ActiveDownloads}");
}
```

## Extensibilidad

### Añadir Nueva Red P2P

Para añadir soporte para una nueva red (ej: BitTorrent):

1. **Crear Cliente**:
```csharp
public class BitTorrentClient : INetworkClient
{
    public string NetworkName => "BitTorrent";
    // Implementar interfaz...
}
```

2. **Crear Proveedor de Búsqueda**:
```csharp
public class BitTorrentSearchProvider : ISearchProvider
{
    public string ProviderName => "BitTorrent";
    // Implementar interfaz...
}
```

3. **Registrar en Orquestador**:
```csharp
var btClient = new BitTorrentClient();
var btProvider = new BitTorrentSearchProvider(btClient);
orchestrator.RegisterClient("BitTorrent", btClient);
orchestrator.RegisterSearchProvider("BitTorrent", btProvider);
```

4. **Listo**: Las búsquedas multi-red incluirán automáticamente BitTorrent.

## Integración con MainForm

### Fase Actual (Sin UI Dedicada)

```csharp
// En MainForm.cs, inicialización
private NetworkOrchestrator _networkOrchestrator;

private async Task InitializeNetworks()
{
    _networkOrchestrator = new NetworkOrchestrator();
    
    // Adaptar cliente Soulseek existente
    var slskAdapter = new SoulseekClientAdapter(client);
    var slskProvider = new SoulseekSearchProvider(client);
    _networkOrchestrator.RegisterClient("Soulseek", slskAdapter);
    _networkOrchestrator.RegisterSearchProvider("Soulseek", slskProvider);
    
    // eMule (si está habilitado en configuración)
    if (config.EnableEMule)
    {
        var emuleClient = new EMuleClient { Config = emuleConfig };
        var emuleProvider = new EMuleSearchProvider(emuleClient);
        _networkOrchestrator.RegisterClient("eMule", emuleClient);
        _networkOrchestrator.RegisterSearchProvider("eMule", emuleProvider);
        
        await emuleClient.ConnectAsync(emuleCredentials);
    }
}
```

### Fase Futura (Con UI Dedicada)

- Pestaña "Redes" con estado de cada red
- Checkbox para habilitar/deshabilitar redes individuales
- Búsquedas con selector de redes objetivo
- Resultados con indicador de red de origen
- Estadísticas por red en dashboard

## Ventajas de la Arquitectura

### 1. Separación de Responsabilidades
- Cada red tiene su propio cliente y proveedor
- Orquestador maneja coordinación sin conocer detalles de implementación
- MainForm interactúa solo con orquestador

### 2. Extensibilidad
- Añadir nuevas redes sin modificar código existente
- Interfaces claras y bien definidas
- Plug-and-play de nuevos proveedores

### 3. Mantenibilidad
- Código de cada red aislado en su namespace
- Tests independientes por red
- Fácil deshabilitar/habilitar redes

### 4. Rendimiento
- Búsquedas paralelas reales (no secuenciales)
- Deduplicación eficiente
- Priorización inteligente de fuentes

### 5. Resiliencia
- Fallo de una red no afecta a las demás
- Reconexión independiente por red
- Estadísticas y logs separados

## Limitaciones Actuales

1. **No hay UI dedicada**: Integración pendiente en MainForm
2. **eMule requiere amuled externo**: No está embebido
3. **Deduplicación básica**: Basada solo en nombre de archivo
4. **Sin gestión de descargas multi-red**: Solo búsquedas por ahora

## Próximos Pasos

1. **Fase 3**: Integrar UI en MainForm
   - Pestaña "Redes" con estado
   - Configuración por red
   - Logs consolidados

2. **Fase 4**: Descargas multi-red
   - Selección automática de mejor fuente
   - Failover entre redes
   - Descarga de chunks desde múltiples redes

3. **Fase 5**: Optimizaciones
   - Caché de resultados entre redes
   - Predicción de mejor red por tipo de archivo
   - Métricas avanzadas y analytics

## Referencias

- [Plan de Integración eMule](EMULE_INTEGRATION_PLAN.md)
- [Guía de Instalación aMule](EMule/INSTALLATION_GUIDE.md)
- [Tests de Integración](EMule/TESTING_README.md)
- [Protocolo EC](https://wiki.amule.org/wiki/EC_Protocol_HOWTO)
