using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using fuji_barcode.Models;
using Microsoft.Extensions.Configuration;

namespace fuji_barcode.Services
{
    public class RpaEngineClient
    {
        private readonly HttpClient _httpClient;
        private readonly string? _targetName;

        public RpaEngineClient(IConfiguration configuration)
            : this(BuildHttpClient(configuration), configuration["RpaEngine:TargetName"])
        {
        }

        public RpaEngineClient(HttpClient httpClient, string? targetName = null)
        {
            _httpClient = httpClient;
            _targetName = targetName;
        }

        private static HttpClient BuildHttpClient(IConfiguration configuration)
        {
            var baseUrl = configuration["RpaEngine:BaseUrl"]
                ?? throw new InvalidOperationException("RpaEngine:BaseUrl is not configured");
            var apiKey = configuration["RpaEngine:ApiKey"];

            var client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };

            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            }

            return client;
        }

        public async Task<List<RpaScriptInfo>> ListScriptsAsync()
        {
            var response = await _httpClient.GetAsync("/api/scripts");
            response.EnsureSuccessStatusCode();

            var scripts = await response.Content.ReadFromJsonAsync<List<RpaScriptInfo>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return scripts ?? [];
        }

        public async Task<ScriptRunResult> RunScriptAsync(string scriptName)
        {
            var url = $"/run/{scriptName}";

            if (!string.IsNullOrEmpty(_targetName))
            {
                url += $"?target={_targetName}";
            }

            var response = await _httpClient.PostAsync(url, null);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            return NormalizeRunResult(body, scriptName);
        }

        private static ScriptRunResult NormalizeRunResult(string body, string scriptName)
        {
            var fallback = new ScriptRunResult { Success = true, Message = scriptName };

            if (string.IsNullOrWhiteSpace(body))
            {
                return fallback;
            }

            RunResponseDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<RunResponseDto>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return fallback;
            }

            if (dto == null)
            {
                return fallback;
            }

            if (dto.Success.HasValue)
            {
                var success = dto.Success.Value;
                var message = dto.Message;
                if (success && string.IsNullOrEmpty(message))
                {
                    message = dto.ScriptName ?? scriptName;
                }
                return new ScriptRunResult { Success = success, Message = message };
            }

            if (!string.IsNullOrEmpty(dto.Status))
            {
                return new ScriptRunResult
                {
                    Success = true,
                    Message = dto.Message ?? dto.ScriptName ?? scriptName
                };
            }

            return fallback;
        }

        private sealed class RunResponseDto
        {
            [JsonPropertyName("success")]
            public bool? Success { get; init; }

            [JsonPropertyName("message")]
            public string? Message { get; init; }

            [JsonPropertyName("status")]
            public string? Status { get; init; }

            [JsonPropertyName("scriptName")]
            public string? ScriptName { get; init; }
        }
    }
}

namespace fuji_barcode.Models
{
    public sealed record class RpaScriptInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";
    }

    public sealed record class ScriptRunResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
