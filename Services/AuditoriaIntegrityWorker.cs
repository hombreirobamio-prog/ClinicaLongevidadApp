using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Worker that periodically verifies the integrity of the audit chain and reports findings.
    /// Can be hosted in an IHost or invoked manually from a scheduler.
    /// </summary>
    public class AuditoriaIntegrityWorker : IDisposable
    {
        private readonly AuditoriaService _auditoriaService;
        private readonly TimeSpan _interval;
        private readonly string _reportDirectory;
        private Timer? _timer;
        private bool _running;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private CancellationTokenSource _cts = new();
        private bool _disposed;

        public AuditoriaIntegrityWorker(AuditoriaService auditoriaService, TimeSpan? interval = null, string? reportDirectory = null)
        {
            _auditoriaService = auditoriaService ?? throw new ArgumentNullException(nameof(auditoriaService));
            _interval = interval ?? TimeSpan.FromMinutes(60);
            _reportDirectory = reportDirectory ?? AppPaths.IntegrityReportsDir;
        }

        /// <summary>
        /// Starts the periodic integrity checks.
        /// Safe to call multiple times.
        /// </summary>
        public void Start()
        {
            if (_running) return;

            if (_disposed) throw new ObjectDisposedException(nameof(AuditoriaIntegrityWorker));

            // Ensure we have a fresh, not-cancelled token source for the timer callbacks.
            if (_cts == null || _cts.IsCancellationRequested)
            {
                try { _cts?.Dispose(); } catch { }
                _cts = new CancellationTokenSource();
            }

            // Timer callback schedules a background task. Use a non-async TimerCallback to avoid
            // unobserved exceptions and async-void style behavior. The lambda reads the current
            // CancellationToken from `_cts` so replacing the CTS on Stop/Start is safe.
            _timer = new Timer(_ => _ = RunOnceSafeAsync(_cts.Token), null, TimeSpan.Zero, _interval);
            _running = true;
        }

        /// <summary>
        /// Stops the periodic checks.
        /// </summary>
        public void Stop()
        {
            if (_disposed) return;

            try
            {
                _cts.Cancel();
                try { _cts.Dispose(); } catch { }
                // leave a fresh CTS so Start can be called again
                _cts = new CancellationTokenSource();
            }
            catch { }

            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _timer?.Dispose();
            _timer = null;
            _running = false;
        }

        /// <summary>
        /// Runs a single integrity check immediately and waits for completion.
        /// </summary>
        public async Task RunOnceAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                await RunOnceInternalAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Internal runner used by the timer. It tries to acquire the semaphore without waiting;
        /// if another run is active it will skip this execution to avoid overlap.
        /// </summary>
        private async Task RunOnceSafeAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            // Try synchronous acquisition to avoid queueing timer callbacks.
            if (!_semaphore.Wait(0))
            {
                LogService.Info("AuditoriaIntegrityWorker", "Previous integrity check still running; skipping this interval.");
                return;
            }

            try
            {
                await RunOnceInternalAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task RunOnceInternalAsync(CancellationToken ct)
        {
            await Task.Yield();

            try
            {
                var errors = _auditoriaService.VerifyIntegrity();
                if (errors == null || errors.Count == 0)
                {
                    LogService.Info("AuditoriaIntegrityWorker", "Verificaci?n de integridad: OK");
                    return;
                }

                LogService.Error("AuditoriaIntegrityWorker", $"Verificaci?n de integridad: {errors.Count} issue(s) found: {string.Join("; ", errors)}");

                // Attempt to create a structured, atomic report in CommonApplicationData. Use a temp file
                // and move it into place to avoid partially written files being considered authoritative.
                try
                {
                    string basePath = _reportDirectory;
                    Directory.CreateDirectory(basePath);

                    string fileName = $"integrity_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.log";
                    string tempFile = Path.Combine(basePath, fileName + ".tmp");
                    string finalFile = Path.Combine(basePath, fileName);

                    var sb = new StringBuilder();
                    sb.AppendLine($"TimestampUtc: {DateTime.UtcNow:O}");
                    sb.AppendLine($"Machine: {Environment.MachineName}");
                    sb.AppendLine($"User: {Environment.UserName}");
                    sb.AppendLine($"ErrorsCount: {errors.Count}");
                    sb.AppendLine("--- Details ---");
                    foreach (var e in errors)
                    {
                        sb.AppendLine(e);
                    }

                    // Write atomically
                    await File.WriteAllTextAsync(tempFile, sb.ToString(), ct).ConfigureAwait(false);
                    if (File.Exists(finalFile))
                    {
                        // Keep historical files; collision is unlikely because timestamp includes milliseconds.
                    }
                    File.Move(tempFile, finalFile);
                }
                catch (Exception ex)
                {
                    LogService.Error("AuditoriaIntegrityWorker", "No se pudo escribir el reporte de integridad", ex);
                }

                // TODO: escalate to SIEM / alerting (email, webhook, etc.)
                // Expose an event so hosting code can subscribe and perform escalation.
                try
                {
                    OnIntegrityFailure?.Invoke(errors);
                }
                catch (Exception ex)
                {
                    LogService.Error("AuditoriaIntegrityWorker", "Error invoking OnIntegrityFailure handlers", ex);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("AuditoriaIntegrityWorker", "Error durante verificaci?n de integridad", ex);
            }
        }

        /// <summary>
        /// Event raised when integrity verification finds issues. Hosts can subscribe to escalate alerts.
        /// </summary>
        public event Action<IList<string>>? OnIntegrityFailure;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                Stop();
            }
            catch { }

            try { _semaphore.Dispose(); } catch { }
            try { _cts.Dispose(); } catch { }
        }
    }
}
