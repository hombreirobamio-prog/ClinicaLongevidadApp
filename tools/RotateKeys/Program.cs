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
                    Console.WriteLine("Apply mode will attempt to persist changes. This scaffold will persist keys to Key Vault when configured.");
                    // In a real implementation the new key material would be loaded from a secure source (Key Vault) or generated.
                    var dummyKey = new byte[32];

                    if (type == "hmac")
                    {
                        var plan = await svc.ApplyRotateHmacKeyAsync(dummyKey, version ?? "v-new");
                        PrintPlan(plan);
                        return 0;
                    }
                    else
                    {
                        var plan = await svc.ApplyRotateEncryptionKeyAsync(dummyKey, version ?? "v-new");
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
