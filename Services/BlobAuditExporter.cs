using System;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
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
                string blobName = $"{DateTime.UtcNow:yyyyMMdd}/{eventId}.json";
                var blob = _container.GetBlockBlobClient(blobName);
                using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonPayload));

                var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = "application/json" };
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

                await blob.UploadAsync(ms, headers, metadata);
            }
            catch (RequestFailedException ex)
            {
                LogService.Error("BlobAuditExporter", "Error uploading audit blob", ex);
            }
            catch (Exception ex)
            {
                LogService.Error("BlobAuditExporter", "Unexpected error exporting audit", ex);
            }
        }
    }
}
