using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedicalData.Api.Services;

public class GroqLabAiService : ILabAiService
{
    private const string SystemPrompt =
        """
        Ты медицинский ассистент, интерпретирующий результаты лабораторных анализов для пациента.

        Тебе передают результаты, уже разбитые по категориям. Следуй строго этим правилам:

        СТРУКТУРА ОТВЕТА:
        - Пиши ТОЛЬКО те категории, которые переданы во входных данных. Не придумывай и не добавляй чужих.
        - Перед каждым блоком — название категории ЗАГЛАВНЫМИ БУКВАМИ с новой строки.
        - На каждую категорию — 3–5 предложений.

        ЧТО ПИСАТЬ В КАЖДОМ БЛОКЕ:
        - Объясни, что измеряет ключевой показатель этой категории и зачем он важен для здоровья.
        - При отклонении: назови показатель, его значение, норму и что это значит физиологически (что происходит в организме).
        - Если показатели внутри категории связаны (например, общий холестерин → ЛПНЩ → индекс атерогенности) — объясни связь одним предложением.

        ОБЯЗАТЕЛЬНО:
        - Никаких маркированных списков, никаких звёздочек — только сплошной текст абзацами.
        - Никаких латинских аббревиатур: не "LDL" — а "холестерин ЛПНЩ (плохой холестерин)".
        - Язык простой, понятный человеку без медицинского образования.
        - В конце — блок ОБЩИЙ ВЫВОД: 1–3 предложения о том, на что обратить внимание.
        - Последнее предложение ОБЩЕГО ВЫВОДА всегда: "Ця інформація є загальноосвітньою і не замінює консультацію лікаря."
        """;

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GroqLabAiService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _apiKey = configuration["Groq:ApiKey"]
            ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");
        _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
        _http = httpClientFactory.CreateClient("groq");
    }

    private static readonly (string Label, string[] Keywords)[] CategoryRules =
    [
        ("ЗАГАЛЬНИЙ АНАЛІЗ КРОВІ", ["гемоглобін", "еритроцит", "лейкоцит", "тромбоцит", "гематокрит", "шое", "лімфоцит", "моноцит", "базофіл", "еозинофіл", "сегментоядерн", "паличкоядерн", "метамієлоцит", "мієлоцит", "тромбокрит", "плазматичн", "hgb", "rbc", "wbc", "plt", "hct", "esr", "mcv", "mch", "rdw", "mpv", "pct", "pdw", "ly%", "mo%", "ba%", "eo%", "ne%"]),
        ("ЛІПІДОГРАМА",            ["холестерин", "тригліцерид", "ліпо", "лпвщ", "лпнщ", "лпднщ", "hdl", "ldl", "vldl", "chol", "атероген", "non-hdl", "не-лпвщ", "non–hdl"]),
        ("БІОХІМІЯ",               ["глюкоза", "білок", "білірубін", "алт", "аст", "лужна фосфатаза", "ггт", "лактатдегідрогеназа", "alt", "ast", "alp", "ggt", "ldh", "glu", "glucose", "tbil", "dbil", "ibil"]),
        ("ГОРМОНИ ТА ЕНДОКРИНОЛОГІЯ", ["інсулін", "глікозильован", "hba1c", "нома", "пептид", "тестостерон", "лютеїнізуючий", "глобулін", "fai", "testosterone", "insulin"]),
        ("ОНКОМАРКЕРИ",            ["пса", "psa", "простат"]),
        ("ІНФЕКЦІЙНА СЕРОЛОГІЯ",   ["хелікобактер", "helicobacter", "антитіл", "вірус", "інфекц"]),
    ];

    private static string GetCategory(string testName)
    {
        var n = testName.ToLowerInvariant();
        foreach (var (label, keywords) in CategoryRules)
            if (keywords.Any(k => n.Contains(k)))
                return label;
        return "ІНШЕ";
    }

    public async Task<string> GetSummaryAsync(DateTime date, IReadOnlyList<AiSummaryItem> results, CancellationToken ct = default)
    {
        var grouped = results
            .GroupBy(r => GetCategory(r.TestName))
            .OrderBy(g => Array.FindIndex(CategoryRules, r => r.Label == g.Key))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Результаты анализов от {date:dd.MM.yyyy}:");
        sb.AppendLine();

        foreach (var group in grouped)
        {
            sb.AppendLine($"[{group.Key}]");
            foreach (var item in group)
            {
                var line = $"  {item.TestName}: {item.Value} {item.Unit}";
                if (!string.IsNullOrWhiteSpace(item.ReferenceRange))
                    line += $" (норма: {item.ReferenceRange})";
                if (item.IsAbnormal)
                    line += " ← ОТКЛОНЕНИЕ";
                sb.AppendLine(line);
            }
            sb.AppendLine();
        }

        var userMessage = sb.ToString();

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userMessage }
            },
            temperature = 0.3
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to call Groq API: {ex.Message}", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Groq API returned {(int)response.StatusCode}: {responseBody}");

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content ?? throw new InvalidOperationException("Groq API returned an empty response.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to parse Groq API response: {ex.Message}", ex);
        }
    }

    public async Task<string> GetExplainAsync(string testName, double value, string unit, string referenceRange, string lang, CancellationToken ct = default)
    {
        var isAbnormal = LabResultHelper.IsOutOfRange(value, referenceRange);
        var status = isAbnormal ? "ВІДХИЛЕННЯ від норми" : "в нормі";

        var (systemPrompt, userMessage) = lang switch
        {
            "en" => (
                "You are a medical information assistant. Explain a single lab result to a patient in plain English. " +
                "2-3 sentences: what this test measures, and what the current value may suggest. " +
                "If abnormal, explain what it may be associated with. No bullet points, no markdown, plain text only. " +
                "Always end with: \"This is general information only and is not a substitute for professional medical advice.\"",
                $"Test: {testName}\nValue: {value} {unit}\nReference range: {referenceRange}\nStatus: {(isAbnormal ? "ABNORMAL" : "normal")}"
            ),
            "ru" => (
                "Ты информационный медицинский ассистент. Объясни один показатель анализа пациенту простым языком. " +
                "2-3 предложения: что измеряет этот показатель и о чём может говорить текущее значение. " +
                "Если отклонение — скажи с чем это может быть связано. Без списков, без markdown, только обычный текст. " +
                "Всегда заканчивай словами: «Ця інформація є загальноосвітньою і не замінює консультацію лікаря.»",
                $"Показатель: {testName}\nЗначение: {value} {unit}\nНорма: {referenceRange}\nСтатус: {(isAbnormal ? "ОТКЛОНЕНИЕ" : "в норме")}"
            ),
            _ => (
                "Ти інформаційний медичний асистент. Поясни один показник аналізу пацієнту простою мовою. " +
                "2-3 речення: що вимірює цей показник і про що може свідчити поточне значення. " +
                "Якщо відхилення — скажи з чим це може бути пов'язано. Без списків, без markdown, тільки звичайний текст. " +
                "Завжди закінчуй словами: «Ця інформація є загальноосвітньою і не замінює консультацію лікаря.»",
                $"Показник: {testName}\nЗначення: {value} {unit}\nНорма: {referenceRange}\nСтатус: {status}"
            )
        };

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage  }
            },
            temperature = 0.3,
            max_tokens  = 250
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Groq API returned {(int)response.StatusCode}: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? throw new InvalidOperationException("Empty response from Groq.");
    }
}
