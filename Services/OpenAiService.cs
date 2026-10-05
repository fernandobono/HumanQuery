using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HumanQuery.Services
{
    public class OpenAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public OpenAiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _httpClient.BaseAddress = new Uri(_configuration["OpenAI:BaseUrl"]);
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _configuration["OpenAI:ApiKey"]);
        }

        public async Task<string> SendPromptAsync(string prompt, string? model = null)
        {
            var defaultModel = _configuration["OpenAI:Model"] ?? "gpt-4";

            var selectedModel = string.IsNullOrWhiteSpace(model) ? defaultModel : model;

            // Modelos de respaldo (separados por coma) por si el principal está saturado
            var models = new List<string> { selectedModel };
            models.AddRange((_configuration["OpenAI:FallbackModels"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(m => m != selectedModel));

            string? lastError = null;
            foreach (var currentModel in models)
            {
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    var requestBody = new
                    {
                        model = currentModel,
                        messages = new[]
                        {
                            new { role = "user", content = prompt }
                        }
                    };

                    var content = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        Encoding.UTF8,
                        "application/json"
                    );

                    HttpResponseMessage response;
                    try
                    {
                        response = await _httpClient.PostAsync("chat/completions", content);
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                    {
                        lastError = $"{currentModel}: {ex.Message}";
                        Console.WriteLine($"Fallo de red con {currentModel} (intento {attempt}): {ex.Message}");
                        await Task.Delay(1000 * attempt);
                        continue;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        return ExtractCompletion(responseContent);
                    }

                    var errorContent = await response.Content.ReadAsStringAsync();
                    var status = (int)response.StatusCode;
                    lastError = $"{currentModel}: {errorContent}";

                    // Modelo retirado o inexistente: no tiene sentido reintentarlo, se pasa al siguiente
                    if (status == 404)
                    {
                        Console.WriteLine($"{currentModel} no existe (404), probando el siguiente modelo...");
                        break;
                    }

                    // Solo se reintenta lo transitorio: saturación (429) o caída del proveedor (5xx)
                    if (status != 429 && status < 500)
                    {
                        throw new Exception($"Error calling OpenAI: {errorContent}");
                    }

                    Console.WriteLine($"{currentModel} respondió {status} (intento {attempt}), reintentando...");
                    await Task.Delay(1000 * attempt);
                }
            }

            throw new Exception($"Error calling OpenAI (ningún modelo respondió): {lastError}");
        }

        private static string ExtractCompletion(string responseContent)
        {
            using var doc = JsonDocument.Parse(responseContent);

            var completion = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return completion;
        }

    }
}
