# ?? FASE 2: Implementación de Async/Await

**Versión:** 1.0  
**Fecha:** 2024  
**Status:** ? INICIADA

---

## ?? Objetivo

Mejorar la responsividad de la UI implementando operaciones asincrónicas para todas las operaciones de base de datos que puedan bloquear la interfaz.

---

## ?? Cambios Implementados

### 1. **CacheService.cs** (NUEVO)

**Propósito:** Caché en memoria para datos estáticos que no cambian frecuentemente.

```csharp
// Uso:
var profesionales = CacheService.GetOrSet(
    "Profesionales",
    () => ObtenerProfesionalesActivos(),
    durationMinutes: 60
);
```

**Características:**
- ? Thread-safe con locks
- ? Expiración automática
- ? Invalidación manual
- ? Estadísticas de caché
- ? Logging integrado

**Beneficios:**
- Reducción de llamadas a BD
- Mejora de performance
- Menos presión en servidor

### 2. **AsyncServiceExtensions.cs** (NUEVO)

**Propósito:** Extensiones para operaciones async con reintentos y timeouts.

```csharp
// Uso con reintentos:
var resultado = await AsyncServiceExtensions.ExecuteWithRetryAsync(
    async () => await CargarCitasDelDiaAsync(),
    "CargarCitasDelDia",
    maxRetries: 3
);

// Uso con timeout:
var resultado = await AsyncServiceExtensions.ExecuteWithTimeoutAsync(
    async () => await CargarPacientesAsync(),
    "CargarPacientes",
    timeout: TimeSpan.FromSeconds(10)
);
```

**Características:**
- ? Reintentos automáticos
- ? Delay entre reintentos
- ? Timeout configurable
- ? Logging de cada intento
- ? Manejo de excepciones

---

## ?? Comparación: Sincrónico vs Asincrónico

### Sincrónico (Actual)

```csharp
// ? BLOQUEA LA UI
private void CargarCitasDelDia()
{
    try
    {
        IEnumerable<Cita> citas = CitaService.ObtenerPorFecha(fecha);
        // ... procesamiento
        // ? UI BLOQUEADA mientras se obtienen datos
    }
    catch { }
}
```

**Problemas:**
- UI no responde a clicks
- Animaciones congeladas
- Mala experiencia de usuario

### Asincrónico (Propuesto)

```csharp
// ? NO BLOQUEA LA UI
private async Task CargarCitasDelDiaAsync()
{
    try
    {
        IEnumerable<Cita> citas = await Task.Run(() => 
            CitaService.ObtenerPorFecha(fecha));
        // ... procesamiento
        // ? UI RESPONSIVA mientras se obtienen datos
    }
    catch { }
}
```

**Ventajas:**
- UI siempre responsiva
- Animaciones fluidas
- Mejor experiencia de usuario

---

## ?? Patrón Async/Await Implementado

### Paso 1: Crear método async wrapper

```csharp
private async Task CargarCitasDelDiaAsync()
{
    try
    {
        // Ejecutar operación BD en thread pool (no bloquea UI)
        var citas = await Task.Run(() => 
            CitaService.ObtenerPorFecha(FechaConsultaCitas));

        // Actualizar UI en thread principal
        ActualizarCitasEnUI(citas);

        LogService.Info("CargarCitasDelDiaAsync", "Citas cargadas");
    }
    catch (Exception ex)
    {
        LogService.Error("CargarCitasDelDiaAsync", "Error", ex);
        MostrarError("Cargar citas", ex.Message);
    }
}
```

### Paso 2: Llamar desde commands/eventos

```csharp
_crearCitaCommand = new RelayCommand(async _ => 
{
    await AceptarCitaAsync();
});

// O desde PropertyChanged:
public string ProfesionalCita
{
    set
    {
        _profesionalCita = value ?? string.Empty;
        OnPropertyChanged();
        _ = CargarHorasDisponiblesAsync(); // Fire-and-forget
    }
}
```

### Paso 3: Usar con caché

```csharp
private async Task CargarProfesionalesAsync()
{
    try
    {
        var profesionales = CacheService.GetOrSet(
            "Profesionales",
            () => ObtenerProfesionalesActivos(),
            durationMinutes: 60
        );

        foreach (var prof in profesionales)
        {
            Profesionales.Add(prof);
        }
    }
    catch (Exception ex)
    {
        LogService.Error("CargarProfesionalesAsync", "Error", ex);
    }
}
```

---

## ?? Métodos a Convertir (Próximas Iteraciones)

### ALTA PRIORIDAD (Bloquean mucho)
- [ ] `CargarCitasDelDia()` ? `CargarCitasDelDiaAsync()`
- [ ] `CargarPacientes()` ? `CargarPacientesAsync()`
- [ ] `CargarProfesionales()` ? `CargarProfesionalesAsync()`
- [ ] `CargarHorasDisponibles()` ? `CargarHorasDisponiblesAsync()`
- [ ] `GuardarCita()` ? `GuardarCitaAsync()`
- [ ] `GuardarDatos()` ? `GuardarDatosAsync()`

### MEDIA PRIORIDAD (Bloquean moderadamente)
- [ ] `FiltrarPacientes()` ? `FiltrarPacientesAsync()`
- [ ] `ActualizarFichaPaciente()` ? `ActualizarFichaPacienteAsync()`
- [ ] `ConfirmarCitaSeleccionada()` ? `ConfirmarCitaSeleccionadaAsync()`
- [ ] `CambiarEstadoCitaSeleccionada()` ? `CambiarEstadoCitaSeleccionadaAsync()`

### BAJA PRIORIDAD (Bloquean poco)
- [ ] Métodos de UI pura (LimpiarFormulario, etc.)

---

## ?? Mejores Prácticas Async/Await

### ? HACER

```csharp
// ? Usar async Task para void-returning methods
public async Task CargaCompleta()
{
    await CargarPacientesAsync();
    await CargarProfesionalesAsync();
}

// ? Usar ConfigureAwait(false) en libraries
var resultado = await operacion.ConfigureAwait(false);

// ? Manejar excepciones explícitamente
try 
{ 
    await operacion(); 
}
catch (Exception ex)
{
    LogService.Error(..., ex);
}

// ? Usar using para IAsyncDisposable
using (var resource = new AsyncResource())
{
    await resource.ProcessAsync();
}
```

### ? NO HACER

```csharp
// ? NO usar async void para event handlers (exceptions se pierden)
public async void Button_Click(object sender, EventArgs e) // ? MAL
{
    await operacion();
}

// ? Usar async void solo para event handlers
public async Task Button_Click(object sender, EventArgs e) // ? CORRECTO
{
    await operacion();
}

// ? NO hacer .Wait() o .Result() (deadlock)
var result = operacion.Result; // ? DEADLOCK

// ? NO mezclar sync y async sin cuidado
public async Task MixedAsync()
{
    var data = syncMethod(); // ? OK si es rápido
    await asyncMethod(); // ? OK
}
```

---

## ?? Impacto Esperado

### Performance

```
Operación: Cargar 100 citas

ANTES (Sincrónico):
?? Obtener citas: 500ms
?? Filtrar: 50ms
?? UI BLOQUEADA: 550ms
?? Total: 550ms de espera

DESPUÉS (Asincrónico):
?? Obtener citas (thread pool): 500ms
?? Filtrar (paralelo): 30ms
?? UI RESPONSIVA: 0ms de espera
?? Total: 500ms (UI siempre responsiva)
```

### Escalabilidad

```
Sincrónico: 1 usuario = 1 thread bloqueado
Asincrónico: 100 usuarios = thread pool eficiente
```

---

## ?? Testing Async

```csharp
[TestClass]
public class PanelRecepcionViewModelAsyncTests
{
    [TestMethod]
    public async Task CargarCitasDelDiaAsync_DebeLlenarColeccion()
    {
        // Arrange
        var vm = new PanelRecepcionViewModel();

        // Act
        await vm.CargarCitasDelDiaAsync();

        // Assert
        Assert.IsTrue(vm.CitasDelDia.Count > 0);
    }

    [TestMethod]
    [ExpectedException(typeof(TimeoutException))]
    public async Task CargarPacientesAsync_ConTimeout_LanzaTimeoutException()
    {
        // Arrange
        var vm = new PanelRecepcionViewModel();

        // Act
        await AsyncServiceExtensions.ExecuteWithTimeoutAsync(
            async () => await Task.Delay(15000),
            "TestOperacion",
            TimeSpan.FromSeconds(5)
        );
    }
}
```

---

## ?? Recursos Recomendados

- Microsoft Docs: [Async/Await Pattern](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/)
- Stephen Cleary: [Async/Await Best Practices](https://blog.stephencleary.com/2012/02/async-and-await.html)
- Pluralsight: [Asynchronous Programming in C#]()

---

## ?? Roadmap

### FASE 2.1: Métodos Core (Esta sprint)
- [ ] Implementar métodos async principales
- [ ] Integrar CacheService
- [ ] Agregar timeout/retry logic

### FASE 2.2: Testing (Próxima sprint)
- [ ] Unit tests para async methods
- [ ] Integration tests
- [ ] Performance benchmarks

### FASE 2.3: Monitoring (Futuro)
- [ ] Application Insights
- [ ] Métricas de performance
- [ ] Alertas de timeouts

---

## ? Checklist de Implementación

Cuando implementes cada método async:

- [ ] Crear método `XxxAsync()`
- [ ] Usar `Task.Run()` para operaciones BD
- [ ] Envolver en try-catch
- [ ] Loguear entrada/salida
- [ ] Actualizar UI en thread principal
- [ ] Considerar caché si aplica
- [ ] Documentar con XML comments
- [ ] Agregar test unitario
- [ ] Validar UI responsiva
- [ ] Documentar cambios

---

**Próximo paso:** Implementar métodos async principales en siguiente iteración.
