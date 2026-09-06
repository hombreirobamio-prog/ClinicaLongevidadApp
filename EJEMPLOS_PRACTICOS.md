# ?? Ejemplos Prácticos de Uso

## Ejemplo 1: Usar Constantes en lugar de Strings

### ? ANTES (Incorrecto)

```csharp
private void ValidarEstadoCita(Cita cita)
{
    if (cita.Estado == "Pendiente")
    {
        // ...
    }
    else if (cita.Estado == "Confirmada" || cita.Estado == "Confirmado")
    {
        // ...
    }
    else if (cita.Estado == "Sala espera" || cita.Estado == "Sala de espera")
    {
        // ...
    }
}
```

**Problemas:**
- Strings duplicados
- Inconsistencia: "Confirmada" vs "Confirmado"
- Typos difíciles de detectar
- No hay intellisense

### ? DESPUÉS (Correcto)

```csharp
private void ValidarEstadoCita(Cita cita)
{
    if (EstadoEs(cita, ESTADO_PENDIENTE))
    {
        // ...
    }
    else if (EstadoEs(cita, ESTADO_CONFIRMADA, "Confirmado"))
    {
        // ...
    }
    else if (EstadoEs(cita, ESTADO_SALA_ESPERA, "Sala de espera"))
    {
        // ...
    }
}
```

**Ventajas:**
- Una única fuente de verdad
- Intellisense disponible
- Refactoring seguro
- Código más limpio

---

## Ejemplo 2: Usar Logging en Métodos

### ? ANTES (Sin Logging)

```csharp
private void GuardarPaciente(Paciente paciente)
{
    try
    {
        // Validar
        if (string.IsNullOrEmpty(paciente.NombreCompleto))
            throw new Exception("Nombre requerido");

        // Guardar
        PacienteService.Guardar(paciente);

        // Actualizar UI
        CargarPacientes();
        LimpiarFormulario();

        MessageBox.Show("Paciente guardado", "Éxito");
    }
    catch (Exception ex)
    {
        MessageBox.Show("Error: " + ex.Message, "Error");
        // Sin registro de qué salió mal, difícil hacer debugging
    }
}
```

**Problemas:**
- No hay registro de operaciones
- Errores sin contexto
- Stack trace perdido
- Imposible hacer auditoría

### ? DESPUÉS (Con Logging)

```csharp
private void GuardarPaciente(Paciente paciente)
{
    try
    {
        // Validar
        if (string.IsNullOrEmpty(paciente.NombreCompleto))
        {
            LogService.Warning("GuardarPaciente", "Validación fallida: nombre vacío");
            MostrarAdvertencia("Datos del paciente", "El nombre es requerido");
            throw new Exception("Nombre requerido");
        }

        // Guardar
        LogService.Info("GuardarPaciente", $"Guardando paciente: {paciente.Id}");
        PacienteService.Guardar(paciente);

        // Actualizar UI
        CargarPacientes();
        LimpiarFormulario();

        LogService.Info("GuardarPaciente", $"Paciente guardado exitosamente: {paciente.Id}");
        MostrarInformacion("Datos del paciente", "Paciente guardado correctamente");
    }
    catch (Exception ex)
    {
        LogService.Error("GuardarPaciente", "Error al guardar paciente", ex);
        MostrarError("Datos del paciente", $"Error: {ex.Message}");
    }
}
```

**Ventajas:**
- Registro completo de operaciones
- Stack trace en archivo de log
- Auditoría disponible
- Debugging fácil

**Archivo de log generado:**
```
[2024-01-15 10:30:45.123] [INFO] [GuardarPaciente] Guardando paciente: 42
[2024-01-15 10:30:45.456] [INFO] [GuardarPaciente] Paciente guardado exitosamente: 42
```

---

## Ejemplo 3: Unificar Diálogos

### ? ANTES (Inconsistente)

```csharp
private void ProcesarCita()
{
    // Información
    MessageBox.Show("Cita creada", "Crear cita", 
        MessageBoxButton.OK, MessageBoxImage.Information);

    // Advertencia (diferente formato)
    MessageBox.Show("Sin cita seleccionada",
        "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);

    // Error (otro formato más)
    MessageBox.Show("No se pudo guardar la cita: " + ex.Message);
}
```

**Problemas:**
- Parámetros en orden diferente
- Títulos inconsistentes
- Difícil mantener cohesión visual
- Código duplicado

### ? DESPUÉS (Consistente)

```csharp
private void ProcesarCita()
{
    // Información
    MostrarInformacion("Crear cita", "Cita creada");

    // Advertencia
    MostrarAdvertencia("Citas del día", "Sin cita seleccionada");

    // Error
    MostrarError("Procesar cita", "No se pudo guardar la cita");
}
```

**Ventajas:**
- Consistencia garantizada
- Cambios globales fáciles
- Código más legible
- Menos líneas

---

## Ejemplo 4: Refactorizar Método Complejo

### ? ANTES (30+ líneas, difícil de entender)

```csharp
private void CargarHorasDisponibles()
{
    HorasDisponibles.Clear();

    if (string.IsNullOrWhiteSpace(ProfesionalCita))
    {
        HoraCita = string.Empty;
        return;
    }

    IEnumerable<Cita> citas = CitaService.ObtenerPorFecha(FechaCita)
        .Where(cita => string.Equals(
            cita.Profesional,
            ProfesionalCita,
            StringComparison.OrdinalIgnoreCase));

    if (_citaEnEdicion is not null)
    {
        citas = citas.Where(cita => cita.Id != _citaEnEdicion.Id);
    }

    foreach (string hora in HorarioProfesionalService.ObtenerHorasDisponibles(
                 ProfesionalCita,
                 FechaCita,
                 citas))
    {
        HorasDisponibles.Add(hora);
    }

    if (_citaEnEdicion is not null &&
        _citaEnEdicion.Fecha.Date == FechaCita.Date &&
        string.Equals(_citaEnEdicion.Profesional, ProfesionalCita, 
            StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(_citaEnEdicion.Hora) &&
        !HorasDisponibles.Contains(_citaEnEdicion.Hora, StringComparer.Ordinal))
    {
        HorasDisponibles.Add(_citaEnEdicion.Hora);
    }

    if (!HorasDisponibles.Contains(HoraCita, StringComparer.Ordinal))
    {
        HoraCita = HorasDisponibles.FirstOrDefault() ?? string.Empty;
    }
}
```

**Problemas:**
- Difícil de entender qué hace
- Múltiples responsabilidades
- Complejidad ciclomática = 8
- Difícil de testear

### ? DESPUÉS (Dividido en métodos especializados)

```csharp
private void CargarHorasDisponibles()
{
    HorasDisponibles.Clear();

    if (string.IsNullOrWhiteSpace(ProfesionalCita))
    {
        HoraCita = string.Empty;
        return;
    }

    // 1. Obtener citas relevantes
    IEnumerable<Cita> citas = ObtenerCitasDisponibilidad();

    // 2. Cargar horas disponibles
    foreach (string hora in HorarioProfesionalService.ObtenerHorasDisponibles(
                 ProfesionalCita, FechaCita, citas))
    {
        HorasDisponibles.Add(hora);
    }

    // 3. Agregar hora de edición si es necesaria
    AgregarHoraEdicionSiNecesario();

    // 4. Actualizar hora actual si es necesario
    ActualizarHoraCitaSiNecesario();
}

/// <summary>Obtiene las citas para verificar disponibilidad.</summary>
private IEnumerable<Cita> ObtenerCitasDisponibilidad()
{
    IEnumerable<Cita> citas = CitaService.ObtenerPorFecha(FechaCita)
        .Where(cita => string.Equals(
            cita.Profesional,
            ProfesionalCita,
            StringComparison.OrdinalIgnoreCase));

    if (_citaEnEdicion is not null)
    {
        citas = citas.Where(cita => cita.Id != _citaEnEdicion.Id);
    }

    return citas;
}

/// <summary>Agrega la hora de edición si es necesaria.</summary>
private void AgregarHoraEdicionSiNecesario()
{
    if (_citaEnEdicion is not null &&
        _citaEnEdicion.Fecha.Date == FechaCita.Date &&
        string.Equals(_citaEnEdicion.Profesional, ProfesionalCita, 
            StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(_citaEnEdicion.Hora) &&
        !HorasDisponibles.Contains(_citaEnEdicion.Hora, StringComparer.Ordinal))
    {
        HorasDisponibles.Add(_citaEnEdicion.Hora);
    }
}

/// <summary>Actualiza la hora de cita si la actual no está disponible.</summary>
private void ActualizarHoraCitaSiNecesario()
{
    if (!HorasDisponibles.Contains(HoraCita, StringComparer.Ordinal))
    {
        HoraCita = HorasDisponibles.FirstOrDefault() ?? string.Empty;
    }
}
```

**Ventajas:**
- Fácil de entender
- Cada método tiene una responsabilidad
- CC = 2-3 por método
- Altamente testeable

---

## Ejemplo 5: Implementar Dispose Correctamente

### ? ANTES (Sin Dispose)

```csharp
public class PanelRecepcionViewModel : ViewModelBase
{
    // No implementa IDisposable
    // Las colecciones nunca se limpian
    // Memory leaks posibles
}

// En usage
var viewModel = new PanelRecepcionViewModel();
// ... usar
// viewModel nunca se limpia
```

### ? DESPUÉS (Con Dispose)

```csharp
public class PanelRecepcionViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Limpiar recursos administrados
            Pacientes?.Clear();
            CitasDelDia?.Clear();
            HistorialCitasPaciente?.Clear();
            ProximasCitasPaciente?.Clear();
            HorasDisponibles?.Clear();
            Profesionales?.Clear();
            ProfesionalesFiltro?.Clear();

            LogService.Info("Dispose", "ViewModel dispuesto");
        }

        _disposed = true;
    }

    ~PanelRecepcionViewModel()
    {
        Dispose(false);
    }
}

// En usage
using (var viewModel = new PanelRecepcionViewModel())
{
    // Usar viewModel
} // Dispose() llamado automáticamente
```

**Ventajas:**
- Liberación explícita de recursos
- Prevención de memory leaks
- Patrón estándar .NET
- Compatible con `using`

---

## Ejemplo 6: Validación y Manejo de Errores

### ? ANTES (Validación incompleta)

```csharp
private void AceptarCita()
{
    // Mínimo de validación
    if (string.IsNullOrWhiteSpace(HoraCita))
    {
        MessageBox.Show("Seleccione una hora");
        return;
    }

    try
    {
        CitaService.Guardar(cita);
    }
    catch (Exception ex)
    {
        MessageBox.Show("Error: " + ex.Message);
    }
}
```

### ? DESPUÉS (Validación completa con logging)

```csharp
private void AceptarCita()
{
    try
    {
        // 1. Asegurar paciente
        if (!AsegurarPacienteParaCita())
            return;

        // 2. Validar cita completa
        if (!ValidarCitaCompleta())
            return;

        // 3. Validar fecha no festiva
        if (FestivoService.EsFestivo(FechaCita))
        {
            LogService.Warning("AceptarCita", "Fecha festiva seleccionada");
            MostrarAdvertencia("Crear cita", 
                "La fecha seleccionada es festiva");
            return;
        }

        // 4. Validar disponibilidad
        if (CitaService.EstaOcupada(FechaCita, HoraCita, ProfesionalCita, 
            _citaEnEdicion?.Id ?? 0))
        {
            LogService.Warning("AceptarCita", "Hora ocupada para profesional");
            MostrarAdvertencia("Crear cita", 
                "La hora ya está ocupada");
            return;
        }

        // 5. Guardar
        GuardarCita();
        LogService.Info("AceptarCita", "Cita procesada exitosamente");
    }
    catch (Exception ex)
    {
        LogService.Error("AceptarCita", "Error crítico al procesar cita", ex);
        MostrarError("Crear cita", $"Error: {ex.Message}");
    }
}
```

**Ventajas:**
- Validación exhaustiva
- Logging de cada paso
- Manejo centralizado de errores
- Fácil debugging

---

## Checklist: Migración de Código Antiguo

Si tienes código antiguo que necesita actualización:

```csharp
// ? 1. Reemplazar strings hardcoded
// Antes: EstadoEs(cita, "Pendiente")
// Después: EstadoEs(cita, ESTADO_PENDIENTE)

// ? 2. Agregar logging
// try { ... LogService.Info(...); }
// catch (ex) { LogService.Error(..., ex); }

// ? 3. Unificar diálogos
// Antes: MessageBox.Show(msg, title, ...);
// Después: MostrarInformacion(title, msg);

// ? 4. Extraer métodos grandes
// Antes: Método de 50 líneas
// Después: 5 métodos de 10 líneas cada uno

// ? 5. Agregar documentación XML
// /// <summary>Descripción del método</summary>

// ? 6. Implementar try-catch con logging
// try { ... } catch (ex) { LogService.Error(...); throw; }
```

---

**Próximo paso:** Revisar el código y aplicar estas prácticas en nuevos métodos.
