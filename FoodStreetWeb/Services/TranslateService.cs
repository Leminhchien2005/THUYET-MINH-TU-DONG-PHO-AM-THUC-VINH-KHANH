using System.Net.Http.Json;

public class TranslateService
{
    private readonly HttpClient _http;

    public TranslateService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> Translate(string text, string from, string to)
    {
        var res = await _http.PostAsJsonAsync("http://localhost:5000/translate", new
        {
            q = text,
            source = from,
            target = to,
            format = "text"
        });

        var data = await res.Content.ReadFromJsonAsync<TranslateResponse>();
        return data?.TranslatedText ?? text;
    }
}

public class TranslateResponse
{
    public string TranslatedText { get; set; }
}