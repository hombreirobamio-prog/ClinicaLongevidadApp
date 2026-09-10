using System;
using System.Threading.Tasks;
using ClinicaLongevidadApp.Services.KeyRotation;

namespace RotateKeysTool
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            var mode = args[0].ToLowerInvariant();
            var type = args.Length > 1 ? args[1].ToLowerInvariant() : "hmac";
            var version = args.Length > 2 ? args[2] : null;

            var svc = new KeyRotationService();

            try
            {
                if (mode == "preview")
                {
                    if (string.IsNullOrWhiteSpace(version))
                    {
                        Console.WriteLine("Provide a new version identifier for preview.");
                        return 2;
                    }

                    if (type == "hmac")
                    {
                        var plan = await svc.PreviewRotateHmacKeyAsync(version);
                        PrintPlan(plan);
                        return 0;
                    }
                    else
                    {
                        var plan = await svc.PreviewRotateEncryptionKeyAsync(version);
                        PrintPlan(plan);
                        return 0;
                    }
                }
                else if (mode == "apply")
                {
                    if (string.IsNullOrWhiteSpace(version))
                    {
                        Console.WriteLine("Provide a new version identifier for apply.");
                        return 2;
                    }

                    // Determine key material: prefer arg[3] if present, otherwise read from STDIN.
                    string? keyB64 = null;
                    if (args.Length > 3 && !string.IsNullOrWhiteSpace(args[3]))
                    {
                        keyB64 = args[3];
                    }
                    else
                    {
                        Console.WriteLine("Reading Base64 key from stdin. Send EOF (Ctrl+Z + Enter on Windows) when done.");
                        keyB64 = Console.In.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(keyB64)) keyB64 = keyB64.Trim();
                    }

                    if (string.IsNullOrWhiteSpace(keyB64))
                    {
                        Console.WriteLine("No key material provided. Use: apply <hmac|enc> <new-version> <base64-key> or pipe the key to stdin.");
                        return 2;
                    }

                    byte[] keyBytes;
                    try
                    {
                        keyBytes = Convert.FromBase64String(keyB64);
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("Provided key is not valid Base64.");
                        return 3;
                    }

                    Console.WriteLine("Apply mode: persisting provided key to Key Vault (if configured).\nThis operation will store the secret in Key Vault and will NOT re-encrypt existing DB rows.");

                    if (type == "hmac")
                    {
                        var plan = await svc.ApplyRotateHmacKeyAsync(keyBytes, version);
                        PrintPlan(plan);
                        return 0;
                    }
                    else
                    {
                        var plan = await svc.ApplyRotateEncryptionKeyAsync(keyBytes, version);
                        PrintPlan(plan);
                        return 0;
                    }
                }
                else
                {
                    PrintUsage();
                    return 2;
                }
            }
            catch (NotImplementedException nie)
            {
                Console.WriteLine("Not implemented: " + nie.Message);
                return 3;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return 4;
            }
        }

        static void PrintPlan(RotationPlan plan)
        {
            Console.WriteLine("Rotation plan:");
            Console.WriteLine($"  KeyType: {plan.KeyType}");
            Console.WriteLine($"  OldVersion: {plan.OldVersion ?? \"<unknown>\"}");
            Console.WriteLine($"  NewVersion: {plan.NewVersion ?? \"<unknown>\"}");
            Console.WriteLine($"  AffectedRowCountEstimate: {plan.AffectedRowCountEstimate}");
            Console.WriteLine($"  Notes: {plan.Notes}");
        }

        static void PrintUsage()
        {
            Console.WriteLine("RotateKeys tool scaffold for ClinicaLongevidadApp");
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project tools/RotateKeys -- preview <hmac|enc> <new-version>");
            Console.WriteLine("  dotnet run --project tools/RotateKeys -- apply <hmac|enc> <new-version>");
            Console.WriteLine();
            Console.WriteLine("This is a scaffold. Implement integration with KeyVault/LocalKeyProvider before using.");
        }
    }
}
