using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Polly.CircuitBreaker;
using System.Text.Json.Serialization;

namespace SyncOne.Services
{
    public class ApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ConfigurationService _configService;
        private readonly ILogger<ApiService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _timeoutPolicy;
        private bool _disposed;

        public ApiService(
            ConfigurationService configService,
            ILogger<ApiService> logger)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure retry policy
            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            // Configure circuit breaker policy
            _circuitBreakerPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .Or<HttpRequestException>()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

            // Configure timeout policy
            _timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));

            _httpClient = new HttpClient();
        }

        public async Task<ApiResponse> ProcessMessageAsync(string from, string message)
        {
            if (string.IsNullOrEmpty(from))
                throw new ArgumentNullException(nameof(from));
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message));

            try
            {
                var config = await _configService.GetConfigAsync();
                if (string.IsNullOrEmpty(config?.ApiUrl))
                {
                    throw new InvalidOperationException("API URL is not configured");
                }

                if (!Uri.TryCreate(config.ApiUrl, UriKind.Absolute, out var apiUri))
                {
                    throw new InvalidOperationException("API URL is not a valid URI");
                }

                var payload = new
                {
                    from = from,
                    body = message,
                    timestamp = DateTime.UtcNow,
                    messageId = Guid.NewGuid().ToString()
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                // Combine policies
                var resiliencePipeline = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy, _timeoutPolicy);

                var response = await resiliencePipeline.ExecuteAsync(async () =>
                    await _httpClient.PostAsync($"{apiUri.AbsoluteUri.TrimEnd('/')}", jsonContent));

                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseContent, options);
         

                if (apiResponse?.Response == null)
                {
                    throw new InvalidOperationException("Invalid API response format");
                }

                return apiResponse;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed while processing message from {From}", from);
                throw new ApiException("HTTP request failed", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize API response for message from {From}", from);
                throw new ApiException("Failed to deserialize API response", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing message from {From}", from);
                throw new ApiException("Unexpected error", ex);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        ~ApiService()
        {
            Dispose(false);
        }
    }

 
        public class ApiResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("messageId")]
            public string MessageId { get; set; } = string.Empty;

            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; }
        }
    

    public class ApiException : Exception
    {
        public ApiException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}