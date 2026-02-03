# 🧪 GUÍA DE TESTS UNITARIOS - SlskDown

## 📋 RESUMEN

Este proyecto contiene **27 tests unitarios** para validar la funcionalidad de los managers principales de SlskDown.

---

## 🎯 COBERTURA DE TESTS

### **DownloadManagerTests.cs** (12 tests)
- ✅ Constructor validation
- ✅ Add/Remove from queue
- ✅ Active downloads count
- ✅ Provider blacklist management
- ✅ Blacklist snapshot

### **StatisticsManagerTests.cs** (15 tests)
- ✅ Constructor validation
- ✅ Record search/download
- ✅ History management (add, check, clear)
- ✅ Provider stats (success, failure)
- ✅ Top providers ranking
- ✅ Save/Load persistence
- ✅ Reset statistics

---

## 🚀 CÓMO EJECUTAR LOS TESTS

### **Opción 1: Línea de Comandos**

```bash
# Navegar a la carpeta de tests
cd c:\p2p\SlskDown.Tests

# Ejecutar todos los tests
dotnet test

# Ejecutar con más detalle
dotnet test --verbosity detailed

# Listar tests disponibles
dotnet test --list-tests
```

### **Opción 2: Visual Studio**

1. Abrir **SlskDown.sln** en Visual Studio
2. Ir a **Test** → **Test Explorer**
3. Click en **Run All** (▶️)
4. Ver resultados en tiempo real

### **Opción 3: Visual Studio Code**

1. Instalar extensión **".NET Core Test Explorer"**
2. Abrir carpeta del proyecto
3. Click en icono de tests en la barra lateral
4. Ejecutar tests individuales o todos

### **Opción 4: Rider**

1. Abrir proyecto en JetBrains Rider
2. Click derecho en carpeta **Tests**
3. Seleccionar **Run Unit Tests**
4. Ver resultados en ventana de tests

---

## 📊 GENERAR REPORTE DE COBERTURA

```bash
# Ejecutar tests con cobertura
dotnet test --collect:"XPlat Code Coverage"

# Instalar herramienta de reportes (una vez)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generar reporte HTML
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html

# Abrir reporte
start coveragereport/index.html
```

---

## 🔧 SOLUCIÓN DE PROBLEMAS

### **Error: No se encuentran los tests**

```bash
# Restaurar paquetes NuGet
dotnet restore

# Limpiar y reconstruir
dotnet clean
dotnet build
```

### **Error: Falta referencia a SlskDown**

Verificar que `SlskDown.Tests.csproj` tenga:

```xml
<ItemGroup>
  <ProjectReference Include="..\SlskDown\SlskDown.csproj" />
</ItemGroup>
```

### **Error: Falta xUnit**

```bash
# Restaurar paquetes
dotnet restore

# O agregar manualmente
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
```

---

## 📝 ESTRUCTURA DE UN TEST

```csharp
[Fact]
public void NombreDelTest_Condicion_ResultadoEsperado()
{
    // Arrange (Preparar)
    var config = new DownloadManagerConfig();
    var manager = new DownloadManager(config);
    
    // Act (Actuar)
    manager.AddToQueue(task);
    
    // Assert (Verificar)
    Assert.Equal(1, manager.GetQueueSnapshot().Count);
}
```

---

## 🎯 AGREGAR NUEVOS TESTS

### **1. Crear archivo de test**

```csharp
using Xunit;
using SlskDown.Core;

namespace SlskDown.Tests
{
    public class SearchManagerTests
    {
        [Fact]
        public void MiNuevoTest()
        {
            // Arrange
            // Act
            // Assert
        }
    }
}
```

### **2. Ejecutar**

```bash
dotnet test
```

---

## 📈 MÉTRICAS OBJETIVO

| Métrica | Objetivo | Actual |
|---------|----------|--------|
| **Cobertura de Código** | >80% | TBD |
| **Tests Pasando** | 100% | TBD |
| **Tiempo de Ejecución** | <10s | TBD |

---

## 🏆 MEJORES PRÁCTICAS

### **✅ DO (Hacer)**
- Usar nombres descriptivos para tests
- Seguir patrón Arrange-Act-Assert
- Un assert por test (idealmente)
- Tests independientes entre sí
- Usar datos de prueba realistas

### **❌ DON'T (No Hacer)**
- Tests que dependen de orden de ejecución
- Tests con sleeps o delays
- Tests que modifican estado global
- Tests sin asserts
- Tests que prueban implementación interna

---

## 🔄 INTEGRACIÓN CONTINUA (CI/CD)

### **GitHub Actions**

Crear `.github/workflows/tests.yml`:

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: 8.0.x
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal
```

---

## 📚 RECURSOS ADICIONALES

- [xUnit Documentation](https://xunit.net/)
- [.NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/)
- [Moq Framework](https://github.com/moq/moq4) (para mocks avanzados)

---

## 🎉 RESULTADO ESPERADO

Al ejecutar `dotnet test`, deberías ver:

```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    27, Skipped:     0, Total:    27, Duration: < 1 s
```

---

**¡Tests listos para usar! 🚀**
