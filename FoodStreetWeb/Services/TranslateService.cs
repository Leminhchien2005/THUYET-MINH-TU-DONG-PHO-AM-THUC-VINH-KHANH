using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;

public class TranslateService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    private static readonly ConcurrentDictionary<string, string> _cache = new();

    public TranslateService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["GoogleTranslate:ApiKey"];
    }

    public async Task<string> Translate(string text, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var key = $"{from}:{to}:{text}";

        // ✅ cache
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var url = $"https://translation.googleapis.com/language/translate/v2?key={_apiKey}";

            var body = new
            {
                q = text,
                source = from == "auto" ? null : from,
                target = to,
                format = "text"
            };

            var res = await _http.PostAsJsonAsync(url, body);

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ Google API fail: {res.StatusCode}");
                return text;
            }

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();

            var result = json
                .GetProperty("data")
                .GetProperty("translations")[0]
                .GetProperty("translatedText")
                .GetString();

            if (!string.IsNullOrWhiteSpace(result))
                _cache[key] = result;

            return result ?? text;
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ TRANSLATE FAIL: " + ex.Message);
            return text; // ❗ KHÔNG return null nữa
        }
    }
}