# ?? Guía Rápida de Implementación

## ?? Cambios Principales

### 1. Usar Constantes de Estado

```csharp
// ? ANTES - No hacer
if (EstadoEs(cita, "Pendiente", "Confirmada"))
{
    // ...
}

// ? DESPUÉS - Hacer
if (EstadoEs(cita, ESTADO_PENDIENTE, ESTADO_CONFIRMADA))
{
    // ...
}
```

### 2. Logging de Operaciones

```csharp
// Eventos de negocio
LogService.Info("NombreMetodo", "Operación completada");

// Advertencias/situaciones inusuales
LogService.Warning("NombreMetodo", "Validación fallida");

// Errores con excepción
LogService.Error("NombreMetodo", "Descripción del error", ex);
```

### 3. Diálogos Consistentes

```csharp
// ? ANTES - No hacer
MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Information);

// ? DESPUÉS - Hacer
MostrarInformacion(titulo, mensaje);
MostrarAdvertencia(titulo, mensaje);
MostrarError(titulo, mensaje);
```

### 4. Gestión de Recursos

```csharp
// ? ViewModel implementa IDisposable
public class MiViewModel : ViewModelBase, IDisposable
{
    public void Dispose() { /* limpieza */ }
}

// Usar con `using`
using (var vm = new MiViewModel())
{
    // Usar vm
} // Dispose() automático
```

---

## ?? Estructura de Constantes

```csharp
// Ubicación: Top de PanelRecepcionViewModel.cs

private const string ESTADO_PENDIENTE = "Pendiente";
private const string ESTADO_CONFIRMADA = "Confirmada";
private const string ESTADO_SALA_ESPERA = "Sala espera";
private const string ESTADO_EN_CONSULTA = "En consulta";
private const string ESTADO_FINALIZADA = "Finalizada";
private const string ESTADO_CANCELADA = "Cancelada";
private const string ESTADO_FACTURADA = "Facturada";
private const string PROFESIONAL_PENDIENTE = "Pendiente de asignar";
private const string FILTRO_TODOS = "Todos";
```

---

## ?? Ubicación de Logs

**Ruta de archivos de log:**
```
C:\Users\[NombreUsuario]\AppData\Roaming\ClinicaLongevidadApp\Logs\
```

**Formato de archivo:**
```
log_2024-01-15.txt
log_2024-01-16.txt
log_2024-01-17.txt
...
```

**Ejemplo de contenido:**
```
[2024-01-15 10:30:45.123] [INFO] [CargarCitasDelDia] Se cargaron 8 citas
[2024-01-15 10:31:02.456] [WARN] [ValidarProteccion] Paciente sin datos
[2024-01-15 10:32:15.789] [ERROR] [GuardarCita] Error al guardar
Exception: System.Exception: Inner message...
```

---

## ?? Nuevos Métodos Privados

| Método | Parámetros | Responsabilidad |
|--------|-----------|-----------------|
| `ObtenerProfesionalesActivos()` | - | Obtiene profesionales del sistema |
| `ObtenerCitasDisponibilidad()` | - | Citas para verificar disponibilidad |
| `AgregarHoraEdicionSiNecesario()` | - | Agrega hora en edición si corresponde |
| `ActualizarHoraCitaSiNecesario()` | - | Valida/actualiza hora actual |
| `ValidarCitaCompleta()` | - | Valida que cita tenga datos requeridos |
| `GuardarCita()` | - | Persiste cita y actualiza UI |
| `CrearNuevaCita()` | - | Factory para nueva cita |
| `CargarHistorialCitas()` | List<Cita> | Carga historial de citas pasadas |
| `CargarProximasCitas()` | List<Cita> | Carga próximas citas del paciente |
| `ValidarProteccionDatosDelPaciente()` | Cita | Verifica protección de datos |

---

## ?? Búsqueda y Reemplazo

Si necesita migrar código antiguo:

```csharp
// Buscar: EstadoEs(.*"Pendiente"
// Reemplazar: EstadoEs($1ESTADO_PENDIENTE

// Buscar: "Sala espera"
// Reemplazar: ESTADO_SALA_ESPERA

// Buscar: MessageBox.Show(.*MessageBoxButton.OK, MessageBoxImage.Information)
// Reemplazar: MostrarInformacion(titulo, mensaje)
```

---

## ?? Inicialización del ViewModel

```csharp
// En XAML.cs
public partial class MyWindow : Window
{
    private PanelRecepcionViewModel _viewModel;

    public MyWindow()
    {
        InitializeComponent();
        _viewModel = new PanelRecepcionViewModel();
        this.DataContext = _viewModel;
    }

    // Importante: Limpiar recursos
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel?.Dispose();
    }
}
```

---

## ?? Ejemplo Completo

```csharp
private void AjusteEjemploMetodo()
{
    try
    {
        // 1. Validar entrada
        if (!ValidarDatos())
        {
            LogService.Warning("AjusteEjemploMetodo", "Validación fallida");
            MostrarAdvertencia("Título", "Mensaje de advertencia");
            return;
        }

        // 2. Procesar lógica de negocio
        var resultado = ProcesarDatos();

        // 3. Persistir cambios
        PersistirCambios(resultado);

        // 4. Actualizar UI
        ActualizarUI();

        // 5. Registrar éxito
        LogService.Info("AjusteEjemploMetodo", "Operación completada exitosamente");
        MostrarInformacion("Título", "Operación completada");
    }
    catch (Exception ex)
    {
        // 6. Manejar errores
        LogService.Error("AjusteEjemploMetodo", "Error en operación", ex);
        MostrarError("Título", $"Error: {ex.Message}");
    }
}
```

---

## ?? Checklist para Nuevos Métodos

Al crear nuevos métodos en el ViewModel:

- [ ] Usar constantes en lugar de strings hardcoded
- [ ] Agregar documentación XML `/// <summary>`
- [ ] Incluir try-catch con LogService
- [ ] Usar `MostrarInformacion/Advertencia/Error()` para diálogos
- [ ] Mantener métodos simples (< 20 líneas)
- [ ] Una responsabilidad por método
- [ ] Extraer lógica compleja en métodos privados
- [ ] Testeable (sin dependencias globales)

---

## ?? Solución de Problemas

### Problema: "LogService no encontrado"
```csharp
// Agregar using
using ClinicaLongevidadApp.Services;
```

### Problema: "ESTADO_PENDIENTE no definido"
```csharp
// Asegurarse que esté definido en la clase
private const string ESTADO_PENDIENTE = "Pendiente";
```

### Problema: "No se crean archivos de log"
```csharp
// Verificar permisos en AppData
// Ruta: C:\Users\[Usuario]\AppData\Roaming\ClinicaLongevidadApp\Logs
// Crear manualmente si no existe
```

---

## ?? Referencias

- `Models/EstadoCita.cs` - Enum y extensiones de estados
- `Services/LogService.cs` - Sistema de logging centralizado
- `MEJORAS.md` - Documentación completa de cambios
- `RESUMEN_MEJORAS.md` - Resumen visual de mejoras

---

**Última actualización:** 2024  
**Versión:** 1.0  
**Autor:** Sistema de Auditoría de Código
