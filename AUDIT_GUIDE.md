# Audit Guide

// Nota añadida automáticamente

### Nota operativa (2026-10-02) — ordenado de la vista de auditoría

- Implementadas utilidades para ordenar logs y para forzar orden en el DataGrid de manifiesto (`Views/AuditorMenuWindow.xaml.cs`).
- Para finalizar la mejora, asegúrate de que la columna temporal del grid tenga `SortMemberPath` asignado a una propiedad `DateTime` o expone una propiedad calculada `DateTime` en el modelo.
- Informe y tareas: `reports/audit_fix_report_20261002_011700.txt` / `reports/audit_fix_todo_20261002_011700.txt`.
