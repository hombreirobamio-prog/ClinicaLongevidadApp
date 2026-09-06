using System;
using System.Security.Cryptography;

namespace ClinicaLongevidadApp.Services
{
    public class KeyRotationService
    {
        private readonly IKeyRotationProvider? _rotationProvider;
        private readonly AuditoriaService? _auditoriaService;

        public KeyRotationService(IKeyRotationProvider? rotationProvider, AuditoriaService? auditoriaService)
        {
            _rotationProvider = rotationProvider;
            _auditoriaService = auditoriaService;
        }

        public void RotateHmacKey(int keySizeBytes = 32)
        {
            if (_rotationProvider is null)
                throw new InvalidOperationException("No rotation provider configured");

            // generate random key
            byte[] newKey = new byte[keySizeBytes];
            RandomNumberGenerator.Fill(newKey);

            try
            {
                LogService.Info("KeyRotationService", "Persisting new HMAC key (rotation) using configured provider.");
                _rotationProvider.PersistHmacKey(newKey);
                LogService.Info("KeyRotationService", "HMAC key persisted successfully.");
            }
            catch (Exception ex)
            {
                LogService.Error("KeyRotationService", "Failed persisting new HMAC key.", ex);
                throw;
            }

            // audit rotation
            try
            {
                _auditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                {
                    UsuarioAdmin = "System",
                    Accion = "KeyRotation.HMAC",
                    Modulo = "Auditoría",
                    UsuarioAfectado = "",
                    Resultado = true,
                    // Use local time to keep timestamps consistent with other audit entries
                    FechaHora = DateTime.Now,
                    Detalles = AuditoriaDetallesHelper.CrearJson(("KeySize", keySizeBytes)),
                    Tipo = "KeyRotation"
                });
            }
            catch
            {
                // ignore audit errors
            }
        }

        public void RotateEncryptionKey(int keySizeBytes = 32)
        {
            if (_rotationProvider is null)
                throw new InvalidOperationException("No rotation provider configured");

            byte[] newKey = new byte[keySizeBytes];
            RandomNumberGenerator.Fill(newKey);

            _rotationProvider.PersistEncryptionKey(newKey);

            try
            {
                _auditoriaService?.RegistrarEvento(new Models.AuditoriaEvento
                {
                    UsuarioAdmin = "System",
                    Accion = "KeyRotation.ENC",
                    Modulo = "Auditoría",
                    UsuarioAfectado = "",
                    Resultado = true,
                    // Use local time to keep timestamps consistent with other audit entries
                    FechaHora = DateTime.Now,
                    Detalles = AuditoriaDetallesHelper.CrearJson(("KeySize", keySizeBytes)),
                    Tipo = "KeyRotation"
                });
            }
            catch
            {
            }
        }
    }
}
