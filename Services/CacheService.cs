using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicaLongevidadApp.Services
{
    /// <summary>
    /// Servicio de caché en memoria para datos estáticos.
    /// </summary>
    public static class CacheService
    {
        private static readonly Dictionary<string, (object data, DateTime expirationTime)> _cache = new();
        private static readonly object _lockObject = new();

        /// <summary>
        /// Duración por defecto del caché (15 minutos).
        /// </summary>
        private const int DEFAULT_CACHE_DURATION_MINUTES = 15;

        /// <summary>
        /// Obtiene un valor del caché.
        /// </summary>
        public static T? Get<T>(string key) where T : class
        {
            lock (_lockObject)
            {
                if (_cache.TryGetValue(key, out var cacheEntry))
                {
                    // Verificar si ha expirado
                    if (DateTime.Now < cacheEntry.expirationTime)
                    {
                        LogService.Info("CacheService", $"Caché HIT: {key}");
                        return cacheEntry.data as T;
                    }
                    else
                    {
                        // Eliminar entrada expirada
                        _cache.Remove(key);
                        LogService.Info("CacheService", $"Caché EXPIRADO: {key}");
                    }
                }
            }

            LogService.Info("CacheService", $"Caché MISS: {key}");
            return null;
        }

        /// <summary>
        /// Almacena un valor en el caché.
        /// </summary>
        public static void Set<T>(string key, T data, int durationMinutes = DEFAULT_CACHE_DURATION_MINUTES) where T : class
        {
            lock (_lockObject)
            {
                _cache[key] = (data, DateTime.Now.AddMinutes(durationMinutes));
                LogService.Info("CacheService", $"Caché SET: {key} ({durationMinutes} minutos)");
            }
        }

        /// <summary>
        /// Invalida una entrada del caché.
        /// </summary>
        public static void Invalidate(string key)
        {
            lock (_lockObject)
            {
                if (_cache.Remove(key))
                {
                    LogService.Info("CacheService", $"Caché INVALIDADO: {key}");
                }
            }
        }

        /// <summary>
        /// Limpia todo el caché.
        /// </summary>
        public static void Clear()
        {
            lock (_lockObject)
            {
                int count = _cache.Count;
                _cache.Clear();
                LogService.Info("CacheService", $"Caché LIMPIADO ({count} entradas)");
            }
        }

        /// <summary>
        /// Obtiene un valor del caché o lo genera si no existe.
        /// </summary>
        public static T GetOrSet<T>(string key, Func<T> valueFactory, int durationMinutes = DEFAULT_CACHE_DURATION_MINUTES) where T : class
        {
            T? cachedValue = Get<T>(key);
            if (cachedValue is not null)
            {
                return cachedValue;
            }

            T value = valueFactory();
            Set(key, value, durationMinutes);
            return value;
        }

        /// <summary>
        /// Obtiene estadísticas del caché.
        /// </summary>
        public static (int Count, int ValidEntries) GetStatistics()
        {
            lock (_lockObject)
            {
                int total = _cache.Count;
                int valid = _cache.Count(x => DateTime.Now < x.Value.expirationTime);
                return (total, valid);
            }
        }
    }
}
