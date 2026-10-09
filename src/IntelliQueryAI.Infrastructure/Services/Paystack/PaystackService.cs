using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IntelliQueryAI.Application.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliQueryAI.Infrastructure.Services.Paystack;

/// <summary>
/// Implementation of IPaystackService interfacing with the live Paystack API.
/// </summary>
public sealed class PaystackService : IPaystackService
{
    private readonly HttpClient _httpClient;
    private readonly PaystackOptions _options;
    private readonly ILogger<PaystackService> _logger;

    public PaystackService(
        HttpClient httpClient,
        IOptions<PaystackOptions> options,
        ILogger<PaystackService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri("https://api.paystack.co/");
        }

        if (!string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.SecretKey);
        }

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string PublicKey => _options.PublicKey;

    public async Task<PaystackInitializeResult> InitializeTransactionAsync(
        PaystackInitializeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            _logger.LogError("Paystack SecretKey is not configured.");
            return new PaystackInitializeResult(
                Success: false,
                AuthorizationUrl: null,
                AccessCode: null,
                Reference: request.Reference,
                Message: "Paystack secret key is missing from configuration.");
        }

        try
        {
            var payload = new
            {
                email = request.Email,
                amount = request.AmountInSubunits,
                currency = request.Currency,
                reference = request.Reference,
                callback_url = request.CallbackUrl,
                metadata = request.Metadata
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("transaction/initialize", jsonContent, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paystack initialize failed with status {StatusCode}: {Body}",
                    response.StatusCode, responseBody);

                string failureMessage = $"Paystack initialization rejected ({response.StatusCode})";
                try
                {
                    var errNode = JsonNode.Parse(responseBody);
                    var msg = errNode?["message"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        failureMessage = msg;
                    }
                }
                catch
                {
                    // keep default failure message
                }

                return new PaystackInitializeResult(
                    Success: false,
                    AuthorizationUrl: null,
                    AccessCode: null,
                    Reference: request.Reference,
                    Message: failureMessage);
            }

            var node = JsonNode.Parse(responseBody);
            bool status = node?["status"]?.GetValue<bool>() ?? false;
            var data = node?["data"];

            if (!status || data is null)
            {
                return new PaystackInitializeResult(
                    Success: false,
                    AuthorizationUrl: null,
                    AccessCode: null,
                    Reference: request.Reference,
                    Message: node?["message"]?.GetValue<string>() ?? "Failed to initialize Paystack session.");
            }

            return new PaystackInitializeResult(
                Success: true,
                AuthorizationUrl: data["authorization_url"]?.GetValue<string>(),
                AccessCode: data["access_code"]?.GetValue<string>(),
                Reference: data["reference"]?.GetValue<string>() ?? request.Reference,
                Message: node?["message"]?.GetValue<string>() ?? "Authorization URL created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while calling Paystack transaction/initialize for reference {Reference}", request.Reference);
            return new PaystackInitializeResult(
                Success: false,
                AuthorizationUrl: null,
                AccessCode: null,
                Reference: request.Reference,
                Message: ex.Message);
        }
    }

    public async Task<PaystackVerifyResult> VerifyTransactionAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            _logger.LogError("Paystack SecretKey is not configured.");
            return new PaystackVerifyResult(
                Success: false,
                Status: "failed",
                Reference: reference,
                Amount: 0,
                Currency: "USD",
                GatewayResponse: null,
                Metadata: new Dictionary<string, string>(),
                Message: "Paystack secret key is missing from configuration.");
        }

        try
        {
            var response = await _httpClient.GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}", cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paystack verify returned {StatusCode}: {Body}", response.StatusCode, responseBody);

                string failMsg = $"Paystack verification failed with status {response.StatusCode}";
                try
                {
                    var errNode = JsonNode.Parse(responseBody);
                    var msg = errNode?["message"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        failMsg = msg;
                    }
                }
                catch
                {
                    // keep default
                }

                return new PaystackVerifyResult(
                    Success: false,
                    Status: "failed",
                    Reference: reference,
                    Amount: 0,
                    Currency: "USD",
                    GatewayResponse: "Verification call failed",
                    Metadata: new Dictionary<string, string>(),
                    Message: failMsg);
            }

            var node = JsonNode.Parse(responseBody);
            bool overallStatus = node?["status"]?.GetValue<bool>() ?? false;
            var data = node?["data"];

            if (!overallStatus || data is null)
            {
                return new PaystackVerifyResult(
                    Success: false,
                    Status: "failed",
                    Reference: reference,
                    Amount: 0,
                    Currency: "USD",
                    GatewayResponse: null,
                    Metadata: new Dictionary<string, string>(),
                    Message: node?["message"]?.GetValue<string>() ?? "Verification failed");
            }

            string txnStatus = data["status"]?.GetValue<string>() ?? "failed";
            int amount = data["amount"]?.GetValue<int>() ?? 0;
            string currency = data["currency"]?.GetValue<string>() ?? "USD";
            string? gatewayResponse = data["gateway_response"]?.GetValue<string>();

            var metadataDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var metaNode = data["metadata"];
            if (metaNode is JsonObject jsonObj)
            {
                foreach (var kvp in jsonObj)
                {
                    if (kvp.Value is not null)
                    {
                        metadataDict[kvp.Key] = kvp.Value.ToString();
                    }
                }
            }

            return new PaystackVerifyResult(
                Success: txnStatus.Equals("success", StringComparison.OrdinalIgnoreCase),
                Status: txnStatus,
                Reference: reference,
                Amount: amount,
                Currency: currency,
                GatewayResponse: gatewayResponse,
                Metadata: metadataDict,
                Message: node?["message"]?.GetValue<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while calling Paystack transaction/verify/{Reference}", reference);
            return new PaystackVerifyResult(
                Success: false,
                Status: "failed",
                Reference: reference,
                Amount: 0,
                Currency: "USD",
                GatewayResponse: null,
                Metadata: new Dictionary<string, string>(),
                Message: ex.Message);
        }
    }

    public bool VerifyWebhookSignature(string requestBody, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return false;
        }

        try
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.SecretKey));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
            var computedSignature = Convert.ToHexString(hashBytes).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
        }
        catch
        {
            return false;
        }
    }
}
