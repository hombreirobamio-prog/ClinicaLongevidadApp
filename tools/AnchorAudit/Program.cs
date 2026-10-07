using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Data.Sqlite;

namespace AnchorAuditTool;

internal static class Program
{
    private sealed record AuditTail(long Id, string EventId, string Hash);
    private sealed record AuditAnchor(
        int SchemaVersion,
        string Scope,
        DateTimeOffset AnchoredAtUtc,
        long AuditId,
        string EventId,
        string Hash);

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "/?")
        {
            PrintUsage();
            return 0;
        }

        var operation = args[0].ToLowerInvariant();
        var databasePath = GetOption(args, "--db");
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            Console.Error.WriteLine("Error: indique la base mediante --db <ruta>. La herramienta nunca selecciona una base automáticamente.");
            return 2;
        }

        if (!File.Exists(databasePath))
        {
            Console.Error.WriteLine("Error: no existe la base indicada.");
            return 2;
        }

        try
        {
            var container = CreateContainerClient();
            var scope = GetScope();
            return operation switch
            {
                "write" => await WriteAnchorAsync(container, scope, databasePath),
                "verify" => await VerifyAnchorAsync(container, scope, databasePath),
                _ => UnknownOperation(operation)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 4;
        }
    }

    private static BlobContainerClient CreateContainerClient()
    {
        var serviceUriText = Environment.GetEnvironmentVariable("AUDIT_ANCHOR_STORAGE_URI");
        var containerName = Environment.GetEnvironmentVariable("AUDIT_ANCHOR_CONTAINER");
        if (!Uri.TryCreate(serviceUriText, UriKind.Absolute, out var serviceUri) || serviceUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("AUDIT_ANCHOR_STORAGE_URI debe contener la URI HTTPS del servicio Azure Blob.");
        if (string.IsNullOrWhiteSpace(containerName))
            throw new InvalidOperationException("AUDIT_ANCHOR_CONTAINER no está configurado.");

        return new BlobServiceClient(serviceUri, new DefaultAzureCredential()).GetBlobContainerClient(containerName);
    }

    private static string GetScope()
    {
        var scope = Environment.GetEnvironmentVariable("AUDIT_ANCHOR_SCOPE") ?? "audit";
        if (!Regex.IsMatch(scope, "^[A-Za-z0-9_/-]{1,80}$"))
            throw new InvalidOperationException("AUDIT_ANCHOR_SCOPE solo puede contener letras, números, guion, guion bajo y barra.");
        return scope.Trim('/');
    }

    private static async Task<int> WriteAnchorAsync(BlobContainerClient container, string scope, string databasePath)
    {
        var tail = ReadTail(databasePath);
        var anchor = new AuditAnchor(1, scope, DateTimeOffset.UtcNow, tail.Id, tail.EventId, tail.Hash);
        var timestamp = anchor.AnchoredAtUtc.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", CultureInfo.InvariantCulture);
        var hashSuffix = tail.Hash[..Math.Min(12, tail.Hash.Length)].ToLowerInvariant();
        var blobName = $"{scope}/anchors/{timestamp}_{tail.Id:D20}_{hashSuffix}.json";
        var payload = JsonSerializer.SerializeToUtf8Bytes(anchor, new JsonSerializerOptions { WriteIndented = true });

        await container.GetBlobClient(blobName).UploadAsync(BinaryData.FromBytes(payload), overwrite: false);
        Console.WriteLine("Punto de control externo creado: " + blobName);
        Console.WriteLine("Id de auditoría: " + tail.Id);
        Console.WriteLine("No se modificó la base de datos.");
        return 0;
    }

    private static async Task<int> VerifyAnchorAsync(BlobContainerClient container, string scope, string databasePath)
    {
        var prefix = scope + "/anchors/";
        string? latestName = null;
        await foreach (var item in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
        {
            if (latestName is null || string.CompareOrdinal(item.Name, latestName) > 0)
                latestName = item.Name;
        }

        if (latestName is null)
        {
            Console.Error.WriteLine("Error: no existe ningún punto de control externo para este ámbito.");
            return 3;
        }

        var content = await container.GetBlobClient(latestName).DownloadContentAsync();
        var anchor = content.Value.Content.ToObjectFromJson<AuditAnchor>();
        if (anchor is null || anchor.SchemaVersion != 1 || !string.Equals(anchor.Scope, scope, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Error: el punto de control externo no tiene un formato válido.");
            return 3;
        }

        var anchoredRow = ReadRow(databasePath, anchor.AuditId);
        var currentTail = ReadTail(databasePath);
        if (anchoredRow is null
            || !string.Equals(anchoredRow.EventId, anchor.EventId, StringComparison.Ordinal)
            || !string.Equals(anchoredRow.Hash, anchor.Hash, StringComparison.OrdinalIgnoreCase)
            || currentTail.Id < anchor.AuditId)
        {
            Console.Error.WriteLine("Error: la base no contiene el punto de control externo esperado. Puede haber truncado o sustitución.");
            return 3;
        }

        Console.WriteLine("Punto de control externo verificado: " + latestName);
        Console.WriteLine("Id de auditoría anclado: " + anchor.AuditId);
        Console.WriteLine("La verificación de la cadena y firmas debe ejecutarse además con VerifyIntegrity.");
        return 0;
    }

    private static AuditTail ReadTail(string databasePath)
    {
        using var connection = OpenDatabase(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, EventId, Hash FROM Auditoria WHERE Hash IS NOT NULL AND Hash <> '' ORDER BY Id DESC LIMIT 1;";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("La base no contiene un registro de auditoría con hash para anclar.");
        return new AuditTail(reader.GetInt64(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1), reader.GetString(2));
    }

    private static AuditTail? ReadRow(string databasePath, long id)
    {
        using var connection = OpenDatabase(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, EventId, Hash FROM Auditoria WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", id);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new AuditTail(reader.GetInt64(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1), reader.IsDBNull(2) ? string.Empty : reader.GetString(2))
            : null;
    }

    private static SqliteConnection OpenDatabase(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int UnknownOperation(string operation)
    {
        Console.Error.WriteLine("Operación no admitida: " + operation);
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Uso:");
        Console.WriteLine("  dotnet run --project tools/AnchorAudit -- write --db <ruta-base>");
        Console.WriteLine("  dotnet run --project tools/AnchorAudit -- verify --db <ruta-base>");
        Console.WriteLine();
        Console.WriteLine("Variables requeridas:");
        Console.WriteLine("  AUDIT_ANCHOR_STORAGE_URI=https://<cuenta>.blob.core.windows.net");
        Console.WriteLine("  AUDIT_ANCHOR_CONTAINER=<contenedor-existente>");
        Console.WriteLine("  AUDIT_ANCHOR_SCOPE=audit (opcional)");
        Console.WriteLine("La herramienta utiliza DefaultAzureCredential, no muestra secretos y abre SQLite en solo lectura.");
    }
}
