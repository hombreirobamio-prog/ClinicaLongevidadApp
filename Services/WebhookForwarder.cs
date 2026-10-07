using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ClinicaLongevidadApp.Services
{
    public class WebhookForwarder : IWebhookForwarder
    {
        private readonly HttpClient _http;
        private readonly string _url;
        private readonly IKeyProvider _keyProvider;

        public WebhookForwarder(IKeyProvider? keyProvider = null)
        {
            _keyProvider = keyProvider ?? new LocalKeyProvider();
            _http = new HttpClient();
            _url = Environment.GetEnvironmentVariable("AUDIT_WEBHOOK_URL") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_url))
                throw new InvalidOperationException("AUDIT_WEBHOOK_URL not configured");
        }

        public async Task ForwardEventAsync(string jsonPayload, string signature)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, _url);
                req.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                if (!string.IsNullOrEmpty(signature))
                {
                    req.Headers.Add("X-Audit-Signature", signature);
                }

                // Add simple retry
                int attempts = 0;
                Exception? lastError = null;
                while (attempts < 3)
                {
                    attempts++;
                    try
                    {
                        var res = await _http.SendAsync(req);
                        if (res.IsSuccessStatusCode) return;
                        lastError = new HttpRequestException($"Webhook returned {(int)res.StatusCode} ({res.ReasonPhrase}).");
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        LogService.Warning("WebhookForwarder", $"Attempt {attempts} failed: {ex.Message}");
                    }

                    await Task.Delay(500);
                }

                throw new InvalidOperationException("Failed to forward audit event after retries.", lastError);
            }
            catch (Exception ex)
            {
                LogService.Error("WebhookForwarder", "Unexpected error forwarding audit event", ex);
                throw;
            }
        }
    }
}
