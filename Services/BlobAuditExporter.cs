using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace ClinicaLongevidadApp.Services
{
    public class BlobAuditExporter : IAuditExporter
    {
        private readonly BlobContainerClient _container;
        private readonly IKeyProvider? _keyProvider;

        public BlobAuditExporter(IKeyProvider? keyProvider = null)
        {
            _keyProvider = keyProvider;
            string? conn = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
            string? accountUri = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_URI");
            string containerName = Environment.GetEnvironmentVariable("AUDIT_BLOB_CONTAINER") ?? "audit-events";

            if (!string.IsNullOrWhiteSpace(conn))
            {
                var service = new BlobServiceClient(conn);
                _container = service.GetBlobContainerClient(containerName);
            }
            else if (!string.IsNullOrWhiteSpace(accountUri))
            {
                var service = new BlobServiceClient(new Uri(accountUri), new Azure.Identity.DefaultAzureCredential());
                _container = service.GetBlobContainerClient(containerName);
            }
            else
            {
                throw new InvalidOperationException("No storage configuration provided for BlobAuditExporter");
            }

            _container.CreateIfNotExists();
        }

        public async Task ExportEventAsync(string eventId, string jsonPayload, string signature)
        {
            try
            {
                string blobName = BuildBlobName(eventId, jsonPayload);
                var blob = _container.GetBlockBlobClient(blobName);
                using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonPayload));

                var headers = new BlobHttpHeaders { ContentType = "application/json" };
                var metadata = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["signature"] = signature ?? string.Empty,
                    ["eventId"] = eventId
                };

                try
                {
                    var kv = _keyProvider?.GetHmacKeyVersion();
                    if (!string.IsNullOrEmpty(kv)) metadata["keyVersion"] = kv;
                    var encv = _keyProvider?.GetEncryptionKeyVersion();
                    if (!string.IsNullOrEmpty(encv)) metadata["encKeyVersion"] = encv;
                }
                catch { }

                var options = new BlobUploadOptions
                {
                    HttpHeaders = headers,
                    Metadata = metadata,
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                };

                try
                {
                    await blob.UploadAsync(ms, options);
                }
                catch (RequestFailedException ex) when (ex.Status is 409 or 412)
                {
                    var existing = await blob.GetPropertiesAsync();
                    if (MetadataMatchesExistingEvent(existing.Value.Metadata, eventId, signature))
                        return;

                    throw new InvalidOperationException("Ya existe un blob de auditoría con el mismo EventId y metadatos distintos.", ex);
                }
            }
            catch (RequestFailedException ex)
            {
                LogService.Error("BlobAuditExporter", "Error uploading audit blob", ex);
                throw;
            }
            catch (Exception ex)
            {
                LogService.Error("BlobAuditExporter", "Unexpected error exporting audit", ex);
                throw;
            }
        }

        internal static bool MetadataMatchesExistingEvent(
            System.Collections.Generic.IDictionary<string, string> metadata,
            string eventId,
            string? signature)
        {
            return metadata.TryGetValue("eventId", out var existingEventId)
                && metadata.TryGetValue("signature", out var existingSignature)
                && string.Equals(existingEventId, eventId, StringComparison.Ordinal)
                && string.Equals(existingSignature, signature ?? string.Empty, StringComparison.Ordinal);
        }

        internal static string BuildBlobName(string eventId, string jsonPayload)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonPayload);
                if (document.RootElement.TryGetProperty("FechaHora", out var fechaHora)
                    && DateTimeOffset.TryParse(
                        fechaHora.GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var timestamp))
                {
                    return $"{timestamp.UtcDateTime:yyyyMMdd}/{eventId}.json";
                }
            }
            catch (JsonException)
            {
                // Legacy or malformed payloads retain the previous time-based layout.
            }

            return $"{DateTime.UtcNow:yyyyMMdd}/{eventId}.json";
        }
    }
}
