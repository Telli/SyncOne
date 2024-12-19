using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SyncOne.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiService(string baseUrl)
        {
            _httpClient = new HttpClient();
            _baseUrl = baseUrl;
        }

        public async Task<string> ProcessMessageAsync(string message)
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/process",
                new StringContent(JsonSerializer.Serialize(new { message }),
                Encoding.UTF8,
                "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<dynamic>(result).response.ToString();
            }

            return "Error processing message";
        }
    }
}
