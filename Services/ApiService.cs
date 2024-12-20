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
        private readonly ConfigurationService _configService;

        public ApiService(ConfigurationService configService)
        {
            _httpClient = new HttpClient();
            _configService = configService;
        }



        public async Task<string> ProcessMessageAsync(string from, string message)
        {
            try
            {
                var config = await _configService.GetConfigAsync();
                if (string.IsNullOrEmpty(config?.ApiUrl))
                {
                    throw new InvalidOperationException("API URL is not configured");
                }

                var formData = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("from", from),
                new KeyValuePair<string, string>("body", message)
            });

                var response = await _httpClient.PostAsync(
                    $"{config.ApiUrl.TrimEnd('/')}/process",
                    formData);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                    return result.GetProperty("response").GetString() ?? "No response received";
                }

                await _configService.AddLogEntryAsync(
                    "API_ERROR",
                    $"Failed to process message. Status: {response.StatusCode}");

                return "Error processing message";
            }
            catch (Exception ex)
            {
                await _configService.AddLogEntryAsync(
                    "API_ERROR",
                    $"Exception processing message: {ex.Message}");

                return "Error processing message";
            }
        }



        //public async Task<string> ProcessMessageAsync(string message)
        //{
        //    try
        //    {
        //        var config = await _configService.GetConfigAsync();
        //        if (string.IsNullOrEmpty(config?.ApiUrl))
        //        {
        //            throw new InvalidOperationException("API URL is not configured");
        //        }

        //        var response = await _httpClient.PostAsync(
        //            $"{config.ApiUrl.TrimEnd('/')}/process",
        //            new StringContent(
        //                JsonSerializer.Serialize(new { message }),
        //                Encoding.UTF8,
        //                "application/json"));

        //        if (response.IsSuccessStatusCode)
        //        {
        //            var result = await response.Content.ReadAsStringAsync();
        //            var deserializedResponse = JsonSerializer.Deserialize<JsonElement>(result);
        //            return deserializedResponse.GetProperty("response").GetString() ?? "No response received";
        //        }

        //        // Log the error
        //        await _configService.AddLogEntryAsync(
        //            "API_ERROR",
        //            $"Failed to process message. Status: {response.StatusCode}");

        //        return "Error processing message";
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log the exception
        //        await _configService.AddLogEntryAsync(
        //            "API_ERROR",
        //            $"Exception processing message: {ex.Message}");

        //        return "Error processing message";
        //    }
        //}
    }
}
