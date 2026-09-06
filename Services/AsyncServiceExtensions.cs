using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Extensiones para operaciones asincrónicas de servicios.
    /// </summary>
    public static class AsyncServiceExtensions
    {
        /// <summary>
        /// Ejecuta una operación de forma asincrónica con reintentos automáticos.
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            int maxRetries = 3,
            int delayMilliseconds = 500)
        {
            int attemptCount = 0;
            Exception? lastException = null;

            while (attemptCount < maxRetries)
            {
                try
                {
                    attemptCount++;
                    LogService.Info(operationName, $"Intento {attemptCount} de {maxRetries}");

                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LogService.Warning(operationName, $"Intento {attemptCount} falló: {ex.Message}");

                    if (attemptCount < maxRetries)
                    {
                        await Task.Delay(delayMilliseconds);
                    }
                }
            }

            LogService.Error(operationName, $"Falló después de {maxRetries} intentos", lastException);
            throw new InvalidOperationException(
                $"La operación '{operationName}' falló después de {maxRetries} intentos", 
                lastException);
        }

        /// <summary>
        /// Ejecuta una operación de forma asincrónica con timeout.
        /// </summary>
        public static async Task<T> ExecuteWithTimeoutAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            TimeSpan timeout)
        {
            var task = operation();
            var delayTask = Task.Delay(timeout);

            var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
            if (completed == task)
            {
                return await task.ConfigureAwait(false);
            }

            LogService.Error(operationName, $"Operación cancelada por timeout ({timeout.TotalSeconds}s)", null);
            throw new TimeoutException($"La operación '{operationName}' excedió el tiempo límite");
        }

        /// <summary>
        /// Versión con soporte a CancellationToken para reintentos.
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<System.Threading.CancellationToken, Task<T>> operation,
            string operationName,
            int maxRetries = 3,
            int delayMilliseconds = 500,
            System.Threading.CancellationToken cancellationToken = default)
        {
            int attemptCount = 0;
            Exception? lastException = null;

            while (attemptCount < maxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    attemptCount++;
                    LogService.Info(operationName, $"Intento {attemptCount} de {maxRetries}");

                    return await operation(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LogService.Warning(operationName, $"Intento {attemptCount} falló: {ex.Message}");

                    if (attemptCount < maxRetries)
                    {
                        try
                        {
                            await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                    }
                }
            }

            LogService.Error(operationName, $"Falló después de {maxRetries} intentos", lastException);
            throw new InvalidOperationException(
                $"La operación '{operationName}' falló después de {maxRetries} intentos",
                lastException);
        }

        /// <summary>
        /// Versión con soporte a CancellationToken para timeout.
        /// </summary>
        public static async Task<T> ExecuteWithTimeoutAsync<T>(
            Func<System.Threading.CancellationToken, Task<T>> operation,
            string operationName,
            TimeSpan timeout,
            System.Threading.CancellationToken cancellationToken = default)
        {
            using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var task = operation(cts.Token);
            var delayTask = Task.Delay(timeout, cts.Token);

            var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
            if (completed == task)
            {
                cts.Cancel(); // cancel delayTask
                return await task.ConfigureAwait(false);
            }

            LogService.Error(operationName, $"Operación cancelada por timeout ({timeout.TotalSeconds}s)", null);
            throw new TimeoutException($"La operación '{operationName}' excedió el tiempo límite");
        }
    }
}
