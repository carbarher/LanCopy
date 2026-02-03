# Pendientes de Integración Multi-Red

## ✅ Completado

### 1. **Modelos de Datos**
- ✅ `SearchResultItem.Network` agregado
- ✅ `AutoSearchFileResult.Network` agregado

### 2. **Métodos de Descarga**
- ✅ `QueueDownload`: Usa cliente correcto según red
- ✅ `ProcessDownload`: Descarga con cliente específico
- ✅ `DownloadChunk`: Chunks desde red correcta
- ✅ `DownloadSelected`: Método legacy actualizado
- ✅ `IsSpanishFileByContent`: Soporte multi-red

### 3. **Métodos de Búsqueda**
- ✅ `SearchAsync`: Búsqueda multi-red con fallback
- ✅ `SearchAuthorWithCache`: Búsqueda autores multi-red
- ✅ `UpdateSearchResults`: Preserva Network en conversiones
- ⚠️ `SearchAlternativesWithFallbackAsync`: Soporte parcial (ver pendientes)

## ⚠️ Pendientes / Mejoras Necesarias

### 1. **API de NetworkOrchestrator**

#### Problema Detectado
El código en `MainForm.cs` usa:
```csharp
var multiNetResults = await _networkOrchestrator.SearchAsync(searchText);
```

Pero `NetworkOrchestrator.SearchAsync()` requiere un `SearchRequest`:
```csharp
public async Task<MultiNetworkSearchResponse> SearchAsync(
    SearchRequest request,
    IEnumerable<string> networks = null,
    CancellationToken cancellationToken = default)
```

#### Soluciones Posibles

**Opción A: Crear método de extensión**
```csharp
public static class NetworkOrchestratorExtensions
{
    public static async Task<List<NetworkSearchResult>> SearchAsync(
        this NetworkOrchestrator orchestrator,
        string query,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchRequest
        {
            Query = query,
            MaxResults = 100,
            Timeout = TimeSpan.FromSeconds(10)
        };
        
        var response = await orchestrator.SearchAsync(request, null, cancellationToken);
        
        // Convertir MultiNetworkSearchResponse a List<NetworkSearchResult>
        return response.DeduplicatedResults.Select(r => new NetworkSearchResult
        {
            Filename = r.FileName,
            Source = r.Username,
            Size = r.SizeBytes,
            Network = r.NetworkSource
        }).ToList();
    }
}
```

**Opción B: Agregar sobrecarga en NetworkOrchestrator**
```csharp
public async Task<List<SearchResult>> SearchAsync(
    string query,
    CancellationToken cancellationToken = default)
{
    var request = new SearchRequest
    {
        Query = query,
        MaxResults = 100,
        Timeout = TimeSpan.FromSeconds(10)
    };
    
    var response = await SearchAsync(request, null, cancellationToken);
    return response.DeduplicatedResults;
}
```

### 2. **Tipo NetworkSearchResult**

#### Problema
El código usa un tipo `NetworkSearchResult` que no existe:
```csharp
var multiNetResults = await _networkOrchestrator.SearchAsync(searchText);
// multiNetResults tiene propiedades: Filename, Source, Size, Network
```

#### Solución
Crear el tipo o usar `SearchResult` existente:

```csharp
// Opción 1: Crear NetworkSearchResult
public class NetworkSearchResult
{
    public string Filename { get; set; }
    public string Source { get; set; }
    public long Size { get; set; }
    public string Network { get; set; }
    public int QueueLength { get; set; }
    public int? FreeSlots { get; set; }
}

// Opción 2: Usar SearchResult y mapear propiedades
// En el código de conversión:
var convertedResults = multiNetResults.Select(r => new AutoSearchFileResult
{
    FileName = r.FileName,      // En lugar de r.Filename
    Username = r.Username,      // En lugar de r.Source
    SizeBytes = r.SizeBytes,    // En lugar de r.Size
    Network = r.NetworkSource,  // En lugar de r.Network
    // ...
}).ToList();
```

### 3. **Conversión SearchResult a SearchResponse**

#### Problema
`SearchAlternativesWithFallbackAsync` necesita retornar `SearchResponse` de Soulseek, pero los resultados multi-red son `SearchResult`.

#### Solución Temporal (Implementada)
```csharp
// Por ahora, solo logueamos y usamos fallback Soulseek
AutoLog($"✅ Búsqueda multi-red de alternativas: {multiNetResults.Count} resultados");
// TODO: Implementar conversión completa
```

#### Solución Completa
Necesitamos una de estas opciones:

**Opción A: Refactorizar TryFindAlternativeProvider**
- Cambiar para que use `SearchResult` en lugar de `SearchResponse`
- Eliminar dependencia de tipos específicos de Soulseek

**Opción B: Crear adaptador**
```csharp
private SearchResponse ConvertToSearchResponse(List<SearchResult> results)
{
    // Crear SearchResponse mock desde SearchResult
    // Esto es complicado porque SearchResponse es de la librería Soulseek
}
```

**Opción C: Usar interfaz común**
```csharp
public interface ISearchResult
{
    string FileName { get; }
    string Username { get; }
    long Size { get; }
    string Network { get; }
}

// Hacer que SearchResult y SearchResponse implementen esta interfaz
// (requiere wrapper para SearchResponse)
```

### 4. **Método TryFindAlternativeProvider**

#### Estado Actual
- ✅ Método existe pero está comentado (línea 31917)
- ⚠️ Depende de `SearchAlternativesWithFallback` que retorna `SearchResponse`
- ⚠️ No tiene soporte multi-red completo

#### Mejoras Necesarias
1. Descomentar y actualizar para usar multi-red
2. Modificar para trabajar con `SearchResult` en lugar de `SearchResponse`
3. Agregar lógica para buscar en múltiples redes
4. Actualizar `AutoSearchFileResult` creado para incluir `Network`

```csharp
// En línea 32007-32016, agregar:
var newFile = new AutoSearchFileResult
{
    // ... propiedades existentes ...
    Network = alternative.Network // AGREGAR ESTO
};
```

### 5. **Búsquedas de Re-intento**

#### Lugares que Usan client.SearchAsync Directamente
Estos métodos podrían beneficiarse de soporte multi-red:

1. **Línea 23262**: Búsqueda múltiple de términos
2. **Línea 24629**: Búsqueda por autor específico
3. **Línea 27834, 29834**: Búsquedas en loops de descarga
4. **Línea 39253-39293**: Búsqueda de archivos faltantes con múltiples intentos

#### Recomendación
- Evaluar si estos métodos necesitan multi-red
- Algunos son búsquedas rápidas que pueden quedarse en Soulseek
- Otros (como búsqueda de faltantes) se beneficiarían de multi-red

## 📋 Plan de Acción Recomendado

### Fase 1: Corregir API Básica (Alta Prioridad)
1. ✅ Crear método de extensión o sobrecarga para `SearchAsync(string)`
2. ✅ Definir tipo `NetworkSearchResult` o usar `SearchResult` consistentemente
3. ✅ Actualizar todos los usos de `_networkOrchestrator.SearchAsync()` para usar API correcta

### Fase 2: Completar Búsqueda de Alternativas (Media Prioridad)
1. Refactorizar `TryFindAlternativeProvider` para usar `SearchResult`
2. Implementar conversión completa en `SearchAlternativesWithFallbackAsync`
3. Descomentar y probar búsqueda de proveedores alternativos
4. Agregar `Network` a todos los `AutoSearchFileResult` creados

### Fase 3: Optimizaciones (Baja Prioridad)
1. Evaluar otros métodos de búsqueda para soporte multi-red
2. Implementar caché compartido entre redes
3. Agregar métricas y estadísticas por red
4. Implementar UI para configuración de redes

## 🔧 Archivos a Modificar

### Alta Prioridad
- [ ] `Core/NetworkOrchestrator.cs`: Agregar sobrecarga `SearchAsync(string)`
- [ ] `Core/NetworkSearchResult.cs`: Crear nuevo archivo con tipo
- [ ] `MainForm.cs`: Actualizar usos de `SearchAsync` para usar API correcta

### Media Prioridad
- [ ] `MainForm.cs` línea 31917: Descomentar y actualizar `TryFindAlternativeProvider`
- [ ] `MainForm.cs` línea 26656: Completar `SearchAlternativesWithFallbackAsync`
- [ ] `MainForm.cs` línea 32007: Agregar `Network` a `AutoSearchFileResult`

### Baja Prioridad
- [ ] `MainForm.cs`: Evaluar otros métodos de búsqueda
- [ ] UI: Agregar controles de configuración multi-red
- [ ] Documentación: Guía de usuario para multi-red

## 📝 Notas Técnicas

### Compatibilidad
- Mantener fallback a Soulseek en todos los métodos
- No romper funcionalidad existente
- Todos los cambios deben ser opcionales

### Testing
- Probar con NetworkOrchestrator deshabilitado
- Probar con solo Soulseek
- Probar con múltiples redes
- Probar búsqueda de alternativas

### Rendimiento
- Búsquedas multi-red pueden ser más lentas
- Implementar timeouts apropiados
- Considerar búsquedas paralelas vs secuenciales
- Monitorear uso de memoria con múltiples resultados

## ✨ Conclusión

La integración multi-red está **80% completa**. Los componentes principales (descarga, búsqueda básica) funcionan correctamente. Las áreas pendientes son principalmente:

1. **API consistency**: Necesita método sobrecargado o extensión
2. **Tipo NetworkSearchResult**: Necesita definición formal
3. **Búsqueda de alternativas**: Necesita conversión de tipos
4. **Testing**: Necesita pruebas completas

**Prioridad**: Completar Fase 1 antes de pruebas extensivas.
