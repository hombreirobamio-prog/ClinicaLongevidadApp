using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ClinicaLongevidadApp;
using ClinicaLongevidadApp.Services;
using ClinicaLongevidadApp.Views;
using Microsoft.Data.Sqlite;

internal static class Program
{
    private sealed class TestKeys : IKeyProvider
    {
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public bool Available = true;
        public byte[] GetHmacKey() => Available ? key : Array.Empty<byte>();
        public string GetHmacKeyVersion() => "manual-isolated-v1";
        public byte[]? GetHmacKeyByVersion(string? version) => version == GetHmacKeyVersion() ? key : null;
        public byte[]? GetEncryptionKey() => null;
        public string GetEncryptionKeyVersion() => "";
        public byte[]? GetEncryptionKeyByVersion(string? version) => null;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            // Process-only settings. Never run App.OnStartup or use its operational database.
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
            Environment.SetEnvironmentVariable("AUDIT_ENFORCE_AUTH", "1");
            Environment.SetEnvironmentVariable("AUDIT_FORWARD_ENABLED", "0");
            Environment.SetEnvironmentVariable("AUDIT_INTEGRITY_ENABLED", "0");
            Environment.SetEnvironmentVariable("AUDIT_WEBHOOK_URL", null);
            Environment.SetEnvironmentVariable("STORAGE_CONNECTION_STRING", null);
            Environment.SetEnvironmentVariable("STORAGE_ACCOUNT_URI", null);
            Environment.SetEnvironmentVariable("SILENT_MODE", null);
            string root = Path.Combine(Path.GetTempPath(), "ClinicaLongevidad-PruebaCola", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var connectionString = new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(root, "solo-pruebas.db"), Pooling = false, ForeignKeys = true }.ToString();
            var keys = new TestKeys();
            var audit = new AuditoriaService(connectionString, keys);
            if (!audit.IsInitialized) throw new InvalidOperationException("No se pudo crear la auditoría de pruebas.");
            // The real view calls the existing public service, which resolves this application property.
            typeof(App).GetProperty(nameof(App.AuditoriaService))!.GetSetMethod(true)!.Invoke(null, new object[] { audit });
            Sesion.UsuarioActual = "USUARIO-FICTICIO";
            Sesion.RolActual = "Administración";
            Sesion.AreaActual = "PRUEBAS";
            Seed(connectionString);
            var admin = new AuditAdminService(connectionString);

            if (args.Contains("--self-test"))
            {
                Check(admin.GetDeadLetter().Count == 4, "four synthetic records");
                Sesion.RolActual = "Recepcion";
                Check(!admin.RequeueDeadLetter(1) && !admin.DeleteDeadLetter(1), "role denial");
                Sesion.RolActual = "Administración";
                keys.Available = false;
                Check(!admin.RequeueDeadLetter(1) && !admin.DeleteDeadLetter(1), "audit failure rollback");
                Check(admin.GetDeadLetter().Count == 4 && admin.GetPending().Count == 0, "preserved queues");
                keys.Available = true;
                Check(admin.RequeueDeadLetter(1) && admin.DeleteDeadLetter(2), "public service success");
                Check(admin.GetPending().Count == 1 && admin.GetDeadLetter().Count == 2, "queue counts");
                Check(audit.GetRecentAudits(20).Count == 2 && audit.VerifyIntegrity().Count == 0, "audit integrity");
                // Construct the actual WPF view, but do not open it or claim visual verification.
                var smokeApp = new Application();
                smokeApp.Properties["AuditConnectionString"] = connectionString;
                var smokeView = new AuditAdminView(connectionString);
                Check(smokeView.DataContext != null, "real WPF view construction");
                smokeView.Close();
                smokeApp.Shutdown();
                Console.WriteLine("SELF-TEST OK: isolated data, public services and WPF view construction.");
                return 0;
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.Properties["AuditConnectionString"] = connectionString;
            var panel = new StackPanel { Margin = new Thickness(18) };
            var window = new Window
            {
                Title = "PRUEBA AISLADA — Cola de auditoría",
                Width = 810, Height = 720, Content = new ScrollViewer { Content = panel }
            };
            app.MainWindow = window;
            panel.Children.Add(new TextBlock
            {
                Text = "PRUEBA AISLADA: solo datos ficticios",
                FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "No necesitas usuario ni contraseña. Cada apertura crea cuatro registros nuevos. No se envía nada al exterior.\n" +
                    "La ventana Audit Admin que abrirás es la pantalla real de la aplicación.\n\n" +
                    "1. Pulsa Abrir cola. En Dead Letter selecciona PRUEBA-01 y pulsa Requeue: debe pasar a Pending Forwards.\n" +
                    "2. Selecciona PRUEBA-02 y pulsa Delete: debe desaparecer.\n" +
                    "3. Vuelve aquí y elige Recepción. Con PRUEBA-03 intenta Requeue y Delete: deben mostrar error y conservarlo.\n" +
                    "4. Elige Administración y activa Simular fallo. Repite con PRUEBA-03: debe seguir intacto.\n" +
                    "5. Desactiva Simular fallo y reencola PRUEBA-03: ahora debe funcionar.\n" +
                    "6. Pulsa Ver resultados: deben aparecer dos reencolados y una eliminación, sin eventos de éxito por los fallos.\n\n" +
                    "Refresh = actualizar; Requeue = reencolar; Delete = eliminar.\n" +
                    "Puedes cerrar y volver a abrir solo Audit Admin para comprobar que conserva los cambios de esta sesión.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14)
            });
            var roles = new ComboBox { ItemsSource = new[] { "Administración", "Recepción" }, SelectedIndex = 0, Margin = new Thickness(0, 5, 0, 5) };
            roles.SelectionChanged += (_, _) => Sesion.RolActual = roles.SelectedIndex == 0 ? "Administración" : "Recepcion";
            panel.Children.Add(new TextBlock { Text = "Rol de prueba:" });
            panel.Children.Add(roles);
            var failure = new CheckBox { Content = "Simular fallo de auditoría", Margin = new Thickness(0, 10, 0, 10) };
            failure.Checked += (_, _) => keys.Available = false;
            failure.Unchecked += (_, _) => keys.Available = true;
            panel.Children.Add(failure);
            AuditAdminView? queueWindow = null;
            AddButton(panel, "Abrir cola (Audit Admin)", () =>
            {
                if (queueWindow != null) { queueWindow.Activate(); return; }
                queueWindow = new AuditAdminView(connectionString) { Owner = window };
                queueWindow.Closed += (_, _) => queueWindow = null;
                queueWindow.Show();
            });
            var output = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 135, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            AddButton(panel, "Ver resultados y guardar informe", () =>
            {
                string report = $"Fecha UTC: {DateTime.UtcNow:O}\n" +
                    $"Pendientes: {admin.GetPending().Count}; Fallidos: {admin.GetDeadLetter().Count}\n" +
                    $"Errores de integridad: {audit.VerifyIntegrity().Count}\n" +
                    string.Join("\n", audit.GetRecentAudits(100).Select(e => $"{e.Accion} — {e.UsuarioAfectado} — {e.Resultado}"));
                File.WriteAllText(Path.Combine(root, "resultado.txt"), report, Encoding.UTF8);
                output.Text = report;
            });
            panel.Children.Add(output);
            panel.Children.Add(new TextBox
            {
                Text = "Carpeta de esta prueba: " + root, IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Al cerrar esta ventana termina la prueba. Guarda el informe antes. Una nueva apertura empieza desde cero; no cambia tu base habitual.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0)
            });
            app.Run(window);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("No se pudo iniciar la prueba: " + ex.Message);
            return 1;
        }
    }

    private static void AddButton(Panel panel, string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 4) };
        button.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Prueba aislada", MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        panel.Children.Add(button);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Self-test failed: " + name);
    }

    private static void Seed(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE AuditForwardQueue (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, EventId TEXT NOT NULL, Payload TEXT NOT NULL, Signature TEXT,
                Attempts INTEGER NOT NULL DEFAULT 0, LastError TEXT, NextAttemptAt TEXT NOT NULL, CreatedAt TEXT NOT NULL);
            CREATE TABLE AuditForwardDeadLetter (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, EventId TEXT NOT NULL, Payload TEXT NOT NULL, Signature TEXT,
                Attempts INTEGER NOT NULL DEFAULT 0, LastError TEXT, CreatedAt TEXT NOT NULL, FailedAt TEXT NOT NULL);
            """;
        command.ExecuteNonQuery();
        for (int i = 1; i <= 4; i++)
        {
            command.CommandText = """
                INSERT INTO AuditForwardDeadLetter (EventId, Payload, Signature, Attempts, LastError, CreatedAt, FailedAt)
                VALUES (@event, 'CONTENIDO FICTICIO', 'FIRMA FICTICIA', 5, 'Fallo simulado', @date, @date);
                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@event", $"PRUEBA-{i:00}");
            command.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
            command.ExecuteNonQuery();
        }
    }
}