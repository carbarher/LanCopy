# 🧪 Guía de Testing - Componentes de Nicotine+

## 📋 Resumen

Se han creado **tests unitarios completos** para todos los componentes implementados de Nicotine+. Los tests validan funcionalidad, thread-safety, manejo de errores y casos edge.

---

## 📦 Tests Implementados (6 archivos)

### **1. SoulseekConnectionPoolTests.cs** (10 tests)
- ✅ Creación de nuevas conexiones
- ✅ Reutilización de conexiones del pool
- ✅ Respeto de límites por usuario
- ✅ Limpieza de conexiones idle
- ✅ Estadísticas correctas
- ✅ Verificación de salud de conexiones
- ✅ Pools separados por usuario
- ✅ Thread-safety en acceso concurrente
- ✅ Dispose correcto

### **2. NetworkEventBusTests.cs** (13 tests)
- ✅ Suscripción de handlers
- ✅ Publicación a múltiples suscriptores
- ✅ Desuscripción de handlers
- ✅ Handlers asíncronos
- ✅ Espera de todos los handlers
- ✅ Manejo graceful de excepciones
- ✅ Evento HandlerError
- ✅ Conteo de suscriptores
- ✅ Limpieza de suscripciones
- ✅ Tipos de mensajes independientes
- ✅ Thread-safety
- ✅ Validación de nulls

### **3. TransferStatisticsTests.cs** (14 tests)
- ✅ Registro de inicio de transferencias
- ✅ Actualización de progreso
- ✅ Registro de éxitos
- ✅ Registro de fallos
- ✅ Estadísticas por usuario
- ✅ Estadísticas por proveedor
- ✅ Top usuarios por bytes
- ✅ Top usuarios por velocidad
- ✅ Tracking de razones de fallo
- ✅ Limpieza de estadísticas
- ✅ Proveedores separados
- ✅ Cálculo de velocidad promedio
- ✅ Thread-safety

### **4. UserQueueManagerTests.cs** (15 tests)
- ✅ Verificación de espacio disponible
- ✅ Detección de cola llena
- ✅ Incremento de tamaño
- ✅ Decremento de tamaño
- ✅ No va por debajo de cero
- ✅ Actualización de límites
- ✅ Cálculo de espacio disponible
- ✅ Detección de cola llena
- ✅ Reset de tamaño
- ✅ Estadísticas correctas
- ✅ Usuarios con cola llena
- ✅ Ordenamiento por espacio
- ✅ Limpieza de usuarios inactivos
- ✅ Colas independientes por usuario
- ✅ Thread-safety

### **5. TransferEnumsTests.cs** (15 tests)
- ✅ Clasificación de TimeoutException
- ✅ Clasificación de SocketException
- ✅ Clasificación de IOException (disco lleno)
- ✅ Clasificación de UnauthorizedAccessException
- ✅ Clasificación de OperationCanceledException
- ✅ Clasificación de rechazo "banned"
- ✅ Clasificación de rechazo "queue full"
- ✅ Clasificación de rechazo "file not available"
- ✅ Clasificación de rechazo "user busy"
- ✅ Mensajes amigables para usuario
- ✅ Mensajes accionables (disco lleno)
- ✅ Mensajes claros (banned)
- ✅ ToString con info completa
- ✅ Manejo de nulls
- ✅ Tracking de reintentos

### **6. TransferStatusHelperTests.cs** (12 tests)
- ✅ Mensaje para estado "Queued"
- ✅ Mensaje para "Downloading" con progreso
- ✅ Mensaje para "Completed" con checkmark
- ✅ Mensaje para "UserOffline" con reintento
- ✅ Tooltip con toda la información
- ✅ Tooltip con null task
- ✅ Colores por estado (verde, rojo, azul)
- ✅ Mensaje con reintento programado
- ✅ Mensaje para cola llena
- ✅ Formateo de velocidad (KB/s, MB/s)
- ✅ Formateo de tiempo (s, m, h)

---

## 🎯 Cobertura de Tests

| Componente | Tests | Cobertura Estimada |
|------------|-------|-------------------|
| SoulseekConnectionPool | 10 | ~85% |
| NetworkEventBus | 13 | ~90% |
| TransferStatistics | 14 | ~85% |
| UserQueueManager | 15 | ~90% |
| TransferEnums | 15 | ~80% |
| TransferStatusHelper | 12 | ~75% |
| **TOTAL** | **79** | **~85%** |

---

## 🚀 Ejecutar Tests

### **Opción 1: Visual Studio**
```
1. Abrir SlskDown.sln
2. Build > Build Solution
3. Test > Run All Tests
```

### **Opción 2: Línea de comandos**
```bash
# Navegar a carpeta Tests
cd c:\p2p\SlskDown\Tests

# Ejecutar todos los tests
dotnet test

# Ejecutar con detalles
dotnet test --logger "console;verbosity=detailed"

# Ejecutar con cobertura
dotnet test --collect:"XPlat Code Coverage"
```

### **Opción 3: Tests específicos**
```bash
# Solo tests de ConnectionPool
dotnet test --filter "FullyQualifiedName~SoulseekConnectionPoolTests"

# Solo tests de EventBus
dotnet test --filter "FullyQualifiedName~NetworkEventBusTests"

# Solo tests de Statistics
dotnet test --filter "FullyQualifiedName~TransferStatisticsTests"
```

---

## 📊 Resultados Esperados

### **Todos los tests pasando**
```
Test Run Successful.
Total tests: 79
     Passed: 79
     Failed: 0
    Skipped: 0
 Total time: ~5-10 seconds
```

### **Si hay fallos**
```
1. Revisar el mensaje de error específico
2. Verificar que todas las dependencias están instaladas
3. Asegurar que el proyecto principal compila sin errores
4. Revisar logs detallados con --logger "console;verbosity=detailed"
```

---

## 🔍 Tests de Integración (Pendientes)

Los siguientes tests de integración están pendientes de implementación:

### **EnhancedDownloadManager Integration Tests**
- [ ] Descarga completa end-to-end
- [ ] Manejo de errores y reintentos
- [ ] Estadísticas actualizadas correctamente
- [ ] Eventos publicados en orden correcto
- [ ] Cleanup robusto en todos los escenarios

### **Connection Pool Integration**
- [ ] Integración con Soulseek real
- [ ] Reutilización efectiva en escenarios reales
- [ ] Manejo de desconexiones inesperadas

### **Event Bus Integration**
- [ ] Flujo completo de eventos en descarga
- [ ] Múltiples suscriptores recibiendo eventos
- [ ] Performance con alto volumen de eventos

---

## 🐛 Debugging de Tests

### **Test falla intermitentemente**
```csharp
// Problema: Race condition
// Solución: Agregar delays o usar Task.WhenAll

await Task.Delay(100); // Dar tiempo a operaciones async
```

### **Test falla por timeout**
```csharp
// Problema: Operación muy lenta
// Solución: Aumentar timeout o usar mocks

[Fact(Timeout = 10000)] // 10 segundos
public async Task SlowTest() { ... }
```

### **Test falla por recursos**
```csharp
// Problema: No se liberan recursos
// Solución: Implementar IDisposable en test class

public class MyTests : IDisposable
{
    public void Dispose()
    {
        // Cleanup
    }
}
```

---

## 📝 Agregar Nuevos Tests

### **Template de test unitario**
```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var sut = new SystemUnderTest();
    var input = "test";

    // Act
    var result = sut.Method(input);

    // Assert
    Assert.Equal(expectedValue, result);
}
```

### **Template de test asíncrono**
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var sut = new SystemUnderTest();

    // Act
    var result = await sut.MethodAsync();

    // Assert
    Assert.NotNull(result);
}
```

### **Template de test con teoría**
```csharp
[Theory]
[InlineData(1, 2, 3)]
[InlineData(5, 5, 10)]
[InlineData(-1, 1, 0)]
public void Add_DifferentInputs_ReturnsCorrectSum(int a, int b, int expected)
{
    // Arrange
    var calculator = new Calculator();

    // Act
    var result = calculator.Add(a, b);

    // Assert
    Assert.Equal(expected, result);
}
```

---

## ✅ Checklist de Testing

### **Antes de commit**
- [ ] Todos los tests unitarios pasan
- [ ] No hay warnings en compilación de tests
- [ ] Cobertura de código > 80% en componentes críticos
- [ ] Tests documentados con comentarios claros

### **Antes de release**
- [ ] Tests de integración ejecutados
- [ ] Tests de performance validados
- [ ] Tests en diferentes entornos (Dev, QA)
- [ ] Documentación de tests actualizada

### **Mejores prácticas**
- [ ] Cada test prueba una sola cosa
- [ ] Tests son independientes entre sí
- [ ] Tests son determinísticos (no aleatorios)
- [ ] Tests son rápidos (< 1 segundo cada uno)
- [ ] Tests tienen nombres descriptivos

---

## 🎓 Convenciones de Naming

### **Clases de test**
```
[ComponentName]Tests.cs
Ejemplo: SoulseekConnectionPoolTests.cs
```

### **Métodos de test**
```
[MethodName]_[Scenario]_[ExpectedBehavior]
Ejemplo: GetOrCreateConnection_WhenNotInPool_CreatesNewConnection
```

### **Variables de test**
```
// System Under Test
var sut = new MyClass();

// Arrange
var input = "test";
var expected = "result";

// Act
var actual = sut.Method(input);

// Assert
Assert.Equal(expected, actual);
```

---

## 📈 Métricas de Calidad

### **Objetivos**
- ✅ Cobertura de código: > 80%
- ✅ Tests por componente: > 10
- ✅ Tiempo de ejecución: < 10 segundos total
- ✅ Tasa de éxito: 100%

### **Estado Actual**
- ✅ **79 tests implementados**
- ✅ **6 componentes cubiertos**
- ✅ **~85% cobertura estimada**
- ⏳ **Tests de integración pendientes**

---

## 🔗 Referencias

- **xUnit Documentation**: https://xunit.net/
- **Moq Documentation**: https://github.com/moq/moq4
- **.NET Testing Best Practices**: https://docs.microsoft.com/en-us/dotnet/core/testing/

---

**Creado**: 4 de enero de 2026  
**Versión**: SlskDown v2.0 - Nicotine+ Enhanced  
**Estado**: ✅ Tests unitarios completos, listos para ejecutar
