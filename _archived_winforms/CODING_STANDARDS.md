# 📋 Estándares de Código SlskDown

## Equivalente a PEP 8 para C#

**Basado en:** Microsoft C# Coding Conventions + StyleCop

---

## 📖 Índice

1. [Nomenclatura](#nomenclatura)
2. [Formato de Código](#formato-de-código)
3. [Organización de Archivos](#organización-de-archivos)
4. [Comentarios y Documentación](#comentarios-y-documentación)
5. [Mejores Prácticas](#mejores-prácticas)
6. [Ejemplos](#ejemplos)

---

## 1. Nomenclatura

### Clases y Structs: `PascalCase`
```csharp
✅ CORRECTO:
public class SearchResult { }
public class DownloadManager { }
public struct Point2D { }

❌ INCORRECTO:
public class searchResult { }
public class download_manager { }
```

### Interfaces: `IPascalCase`
```csharp
✅ CORRECTO:
public interface ISearchable { }
public interface IDownloadable { }

❌ INCORRECTO:
public interface Searchable { }
public interface downloadable { }
```

### Métodos: `PascalCase`
```csharp
✅ CORRECTO:
public void SearchFiles() { }
public async Task DownloadFileAsync() { }
private void InitializeComponents() { }

❌ INCORRECTO:
public void search_files() { }
public void downloadFile() { }
```

### Propiedades: `PascalCase`
```csharp
✅ CORRECTO:
public string Username { get; set; }
public int MaxResults { get; set; }

❌ INCORRECTO:
public string username { get; set; }
public int max_results { get; set; }
```

### Campos Privados: `_camelCase`
```csharp
✅ CORRECTO:
private string _username;
private int _maxResults;
private readonly ILogger _logger;

❌ INCORRECTO:
private string username;
private string m_username;
private string Username;
```

### Constantes: `UPPER_CASE` o `PascalCase`
```csharp
✅ CORRECTO (Opción 1 - Estilo C):
private const int MAX_RESULTS = 100;
private const string DEFAULT_USERNAME = "guest";

✅ CORRECTO (Opción 2 - Estilo C#):
private const int MaxResults = 100;
private const string DefaultUsername = "guest";

❌ INCORRECTO:
private const int maxResults = 100;
private const int max_results = 100;
```

### Parámetros y Variables Locales: `camelCase`
```csharp
✅ CORRECTO:
public void SearchFiles(string searchTerm, int maxResults)
{
    var fileName = "test.txt";
    int resultCount = 0;
}

❌ INCORRECTO:
public void SearchFiles(string SearchTerm, int MaxResults)
{
    var FileName = "test.txt";
    int result_count = 0;
}
```

### Enums: `PascalCase` (tipo y valores)
```csharp
✅ CORRECTO:
public enum SearchStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}

❌ INCORRECTO:
public enum searchStatus
{
    not_started,
    in_progress
}
```

---

## 2. Formato de Código

### Longitud de Línea
```csharp
// Máximo: 120 caracteres (PEP 8 usa 79, C# es más flexible)

✅ CORRECTO:
var result = await client.SearchAsync(query, timeout, cancellationToken);

❌ EVITAR (>120 caracteres):
var result = await client.SearchAsync(veryLongQueryStringThatExceedsTheRecommendedLineLengthAndShouldBeRefactored, timeout, cancellationToken);

✅ MEJOR:
var result = await client.SearchAsync(
    veryLongQueryString,
    timeout,
    cancellationToken
);
```

### Indentación
```csharp
// 4 espacios (NO tabs)

✅ CORRECTO:
public void Method()
{
    if (condition)
    {
        DoSomething();
    }
}

❌ INCORRECTO (tabs o 2 espacios):
public void Method()
{
  if (condition)
  {
    DoSomething();
  }
}
```

### Llaves (Allman Style)
```csharp
✅ CORRECTO:
if (condition)
{
    DoSomething();
}
else
{
    DoSomethingElse();
}

❌ INCORRECTO (K&R style):
if (condition) {
    DoSomething();
} else {
    DoSomethingElse();
}
```

### Espacios
```csharp
✅ CORRECTO:
// Espacios alrededor de operadores
int result = a + b * c;
bool isValid = (x > 0) && (y < 100);

// Espacio después de keywords
if (condition) { }
for (int i = 0; i < 10; i++) { }
while (running) { }

// Sin espacio después de cast
var number = (int)value;

❌ INCORRECTO:
int result=a+b*c;
if(condition){ }
var number = ( int ) value;
```

### Líneas en Blanco
```csharp
✅ CORRECTO:
public class MyClass
{
    private int _field;
                            // 1 línea en blanco entre miembros
    public void Method1()
    {
        // Código
    }
                            // 1 línea en blanco entre métodos
    public void Method2()
    {
        // Código
    }
}

❌ INCORRECTO:
public class MyClass
{
    private int _field;
    public void Method1()
    {
        // Código
    }


    public void Method2()  // Demasiadas líneas en blanco
    {
        // Código
    }
}
```

---

## 3. Organización de Archivos

### Orden de Elementos en una Clase
```csharp
public class MyClass
{
    // 1. Constantes
    private const int MAX_SIZE = 100;
    
    // 2. Campos estáticos
    private static int _instanceCount;
    
    // 3. Campos de instancia
    private readonly ILogger _logger;
    private string _name;
    
    // 4. Constructores
    public MyClass()
    {
    }
    
    // 5. Propiedades
    public string Name { get; set; }
    
    // 6. Métodos públicos
    public void PublicMethod()
    {
    }
    
    // 7. Métodos privados
    private void PrivateMethod()
    {
    }
    
    // 8. Clases anidadas
    private class NestedClass
    {
    }
}
```

### Organización de Usings
```csharp
✅ CORRECTO:
// System primero, luego terceros, luego propios
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Soulseek;

using SlskDown.Services;

❌ INCORRECTO:
using SlskDown.Services;
using System;
using Soulseek;
using System.Linq;
```

### Un Archivo por Clase
```csharp
✅ CORRECTO:
// SearchResult.cs
public class SearchResult { }

// DownloadManager.cs
public class DownloadManager { }

❌ EVITAR:
// Multiple.cs
public class SearchResult { }
public class DownloadManager { }
public class FileHelper { }
```

---

## 4. Comentarios y Documentación

### Comentarios XML (equivalente a docstrings de Python)
```csharp
✅ CORRECTO:
/// <summary>
/// Busca archivos en Soulseek según el término especificado.
/// </summary>
/// <param name="searchTerm">Término de búsqueda</param>
/// <param name="maxResults">Número máximo de resultados</param>
/// <returns>Lista de resultados encontrados</returns>
/// <exception cref="ArgumentNullException">Si searchTerm es null</exception>
public async Task<List<SearchResult>> SearchAsync(
    string searchTerm,
    int maxResults)
{
    // Implementación
}

❌ INCORRECTO:
// Busca archivos
public async Task<List<SearchResult>> SearchAsync(string searchTerm, int maxResults)
{
}
```

### Comentarios de Línea
```csharp
✅ CORRECTO:
// Verificar si el archivo ya fue descargado
if (IsAlreadyDownloaded(filename))
{
    return;
}

// TODO: Implementar reintentos automáticos
// HACK: Workaround temporal para bug en Soulseek.NET
// NOTE: Este código será refactorizado en v5.0

❌ INCORRECTO:
if (IsAlreadyDownloaded(filename)) // verifica si ya se descargo
{
    return;
}
```

### Comentarios de Sección
```csharp
✅ CORRECTO:
// ==========================================
// INICIALIZACIÓN
// ==========================================

// ==========================================
// BÚSQUEDA Y FILTRADO
// ==========================================

// ==========================================
// DESCARGA DE ARCHIVOS
// ==========================================
```

---

## 5. Mejores Prácticas

### Uso de `var`
```csharp
✅ CORRECTO:
// Usar var cuando el tipo es obvio
var name = "John";
var count = 10;
var result = GetSearchResults();

// Usar tipo explícito cuando no es obvio
IEnumerable<string> names = GetNames();
SearchResult result = ParseResult(data);

❌ INCORRECTO:
// Abusar de var
var x = GetData();  // ¿Qué tipo es x?
```

### Null Safety
```csharp
✅ CORRECTO:
// Usar null-conditional operator
var length = name?.Length ?? 0;

// Usar null-coalescing
var username = config["username"] ?? "guest";

// Pattern matching
if (result is SearchResult { Size: > 0 } validResult)
{
    Process(validResult);
}

❌ INCORRECTO:
if (name != null)
{
    var length = name.Length;
}
```

### Async/Await
```csharp
✅ CORRECTO:
public async Task<SearchResult> SearchAsync(string query)
{
    var result = await client.SearchAsync(query);
    return result;
}

// Sufijo Async en métodos asíncronos
public async Task DownloadFileAsync() { }

❌ INCORRECTO:
public async Task<SearchResult> Search(string query)  // Falta Async
{
    var result = client.SearchAsync(query).Result;  // No usar .Result
    return result;
}
```

### LINQ
```csharp
✅ CORRECTO:
// Query syntax para queries complejas
var results = from r in searchResults
              where r.Size > minSize
              orderby r.Bitrate descending
              select r;

// Method syntax para queries simples
var filtered = searchResults
    .Where(r => r.Size > minSize)
    .OrderByDescending(r => r.Bitrate)
    .ToList();

❌ INCORRECTO:
// Mezclar estilos sin razón
var results = (from r in searchResults
               where r.Size > minSize
               select r).OrderByDescending(r => r.Bitrate);
```

### Manejo de Excepciones
```csharp
✅ CORRECTO:
try
{
    await DownloadFileAsync(filename);
}
catch (FileNotFoundException ex)
{
    _logger.Error("Archivo no encontrado", ex);
    throw;
}
catch (Exception ex)
{
    _logger.Error("Error inesperado", ex);
    // Manejar o re-lanzar
}

❌ INCORRECTO:
try
{
    await DownloadFileAsync(filename);
}
catch (Exception)
{
    // Tragar excepciones sin logging
}
```

### IDisposable
```csharp
✅ CORRECTO:
// Usar using statement
using var client = new SoulseekClient();
await client.ConnectAsync();

// O using tradicional
using (var stream = File.OpenRead(path))
{
    // Usar stream
}

❌ INCORRECTO:
var client = new SoulseekClient();
await client.ConnectAsync();
// Olvidar Dispose()
```

---

## 6. Ejemplos

### Ejemplo Completo: Clase Bien Formateada

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown
{
    /// <summary>
    /// Gestor de búsquedas de archivos en Soulseek
    /// </summary>
    public class SearchManager : IDisposable
    {
        // Constantes
        private const int DEFAULT_TIMEOUT = 30;
        private const int MAX_RESULTS = 1000;
        
        // Campos
        private readonly ILogger _logger;
        private readonly SoulseekClient _client;
        private bool _disposed;
        
        // Constructor
        public SearchManager(ILogger logger, SoulseekClient client)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }
        
        // Propiedades
        public int Timeout { get; set; } = DEFAULT_TIMEOUT;
        public bool IsSearching { get; private set; }
        
        // Métodos públicos
        /// <summary>
        /// Busca archivos según el término especificado
        /// </summary>
        /// <param name="searchTerm">Término de búsqueda</param>
        /// <param name="maxResults">Número máximo de resultados</param>
        /// <returns>Lista de resultados encontrados</returns>
        public async Task<List<SearchResult>> SearchAsync(
            string searchTerm,
            int maxResults = MAX_RESULTS)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                throw new ArgumentException("Search term cannot be empty", nameof(searchTerm));
            }
            
            IsSearching = true;
            
            try
            {
                _logger.Info($"Iniciando búsqueda: {searchTerm}");
                
                var results = await _client.SearchAsync(searchTerm);
                var filtered = FilterResults(results, maxResults);
                
                _logger.Info($"Búsqueda completada: {filtered.Count} resultados");
                
                return filtered;
            }
            catch (Exception ex)
            {
                _logger.Error("Error en búsqueda", ex);
                throw;
            }
            finally
            {
                IsSearching = false;
            }
        }
        
        // Métodos privados
        private List<SearchResult> FilterResults(
            IEnumerable<SearchResult> results,
            int maxResults)
        {
            return results
                .Where(r => r.Size > 0)
                .OrderByDescending(r => r.Bitrate)
                .Take(maxResults)
                .ToList();
        }
        
        // IDisposable
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            _client?.Dispose();
        }
    }
}
```

---

## 📊 Comparación con PEP 8

| Aspecto | PEP 8 (Python) | C# Conventions |
|---------|----------------|----------------|
| **Clases** | `PascalCase` | `PascalCase` ✅ |
| **Funciones/Métodos** | `snake_case` | `PascalCase` ❌ |
| **Variables** | `snake_case` | `camelCase` ❌ |
| **Constantes** | `UPPER_CASE` | `PascalCase` o `UPPER_CASE` ⚠️ |
| **Privados** | `_leading_underscore` | `_camelCase` ⚠️ |
| **Longitud línea** | 79 caracteres | 120 caracteres ⚠️ |
| **Indentación** | 4 espacios | 4 espacios ✅ |
| **Llaves** | N/A (usa indentación) | Allman style ❌ |
| **Docstrings** | `"""docstring"""` | `/// XML comments` ❌ |

---

## 🔧 Herramientas

### EditorConfig
✅ Archivo `.editorconfig` creado con todas las reglas

### Formatear Código
```bash
# Formatear todo el proyecto
dotnet format

# Verificar formato sin cambios
dotnet format --verify-no-changes
```

### Análisis de Código
```bash
# Ejecutar análisis
dotnet build /p:EnforceCodeStyleInBuild=true

# Ver warnings
dotnet build /p:TreatWarningsAsErrors=true
```

---

## ✅ Checklist de Revisión de Código

- [ ] Nombres siguen convenciones (PascalCase, camelCase, etc.)
- [ ] Líneas no exceden 120 caracteres
- [ ] Indentación correcta (4 espacios)
- [ ] Llaves en nueva línea (Allman style)
- [ ] Comentarios XML en métodos públicos
- [ ] Uso correcto de `var`
- [ ] Métodos async tienen sufijo `Async`
- [ ] Excepciones manejadas correctamente
- [ ] IDisposable implementado donde corresponde
- [ ] Sin código comentado (usar git)
- [ ] Sin warnings del compilador

---

**Fecha:** 30 Octubre 2025  
**Versión:** 1.0  
**Basado en:** Microsoft C# Coding Conventions + StyleCop + PEP 8 principles
