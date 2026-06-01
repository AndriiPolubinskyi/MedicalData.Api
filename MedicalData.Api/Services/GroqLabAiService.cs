using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedicalData.Api.Services;

public class GroqLabAiService : ILabAiService
{
    private static string GetSystemPrompt(string lang) => lang switch
    {
        "en" =>
            """
            You are a medical information assistant interpreting lab results for a patient.

            Results are grouped by category. Follow these rules strictly:

            RESPONSE STRUCTURE:
            - Write ONLY the categories provided in the input. Do not invent new ones.
            - Start each block with the category name IN ALL CAPS on a new line.
            - 3–5 sentences per category.

            WHAT TO WRITE IN EACH BLOCK:
            - Explain what the key indicator measures and why it matters for health.
            - If abnormal: name the indicator, its value, the reference range, and what it means physiologically.
            - If indicators within a category are related (e.g. total cholesterol → LDL → atherogenic index) — explain the connection in one sentence.

            REQUIRED:
            - No bullet points, no asterisks — plain text paragraphs only.
            - Plain language, understandable without medical training.
            - End with a block SUMMARY: 1–3 sentences on what to pay attention to.
            - The very last sentence of SUMMARY must always be: "This is general information only and is not a substitute for professional medical advice."
            """,
        "ru" =>
            """
            Ты информационный медицинский ассистент, интерпретирующий результаты лабораторных анализов для пациента.

            Результаты уже разбиты по категориям. Следуй этим правилам строго:

            СТРУКТУРА ОТВЕТА:
            - Пиши ТОЛЬКО те категории, которые переданы во входных данных.
            - Перед каждым блоком — название категории ЗАГЛАВНЫМИ БУКВАМИ с новой строки.
            - На каждую категорию — 3–5 предложений.

            ЧТО ПИСАТЬ:
            - Объясни, что измеряет ключевой показатель и зачем он важен.
            - При отклонении: назови показатель, значение, норму и физиологический смысл.
            - Если показатели связаны — объясни связь одним предложением.

            ОБЯЗАТЕЛЬНО:
            - Никаких списков, никаких звёздочек — только текст абзацами.
            - Язык простой, без медицинского жаргона.
            - В конце — блок ОБЩИЙ ВЫВОД: 1–3 предложения.
            - Последнее предложение ОБЩЕГО ВЫВОДА всегда: «Ця інформація є загальноосвітньою і не замінює консультацію лікаря.»
            """,
        _ =>
            """
            Ти інформаційний медичний асистент, що інтерпретує результати лабораторних аналізів для пацієнта.

            Результати вже згруповані за категоріями. Дотримуйся цих правил суворо:

            СТРУКТУРА ВІДПОВІДІ:
            - Пиши ТІЛЬКИ ті категорії, що передані у вхідних даних.
            - Перед кожним блоком — назва категорії ВЕЛИКИМИ ЛІТЕРАМИ з нового рядка.
            - На кожну категорію — 3–5 речень.

            ЩО ПИСАТИ:
            - Поясни, що вимірює ключовий показник і навіщо він важливий для здоров'я.
            - При відхиленні: назви показник, значення, норму та фізіологічний зміст.
            - Якщо показники пов'язані — поясни зв'язок одним реченням.

            ОБОВ'ЯЗКОВО:
            - Жодних списків, жодних зірочок — лише суцільний текст абзацами.
            - Мова проста, зрозуміла без медичної освіти.
            - Наприкінці — блок ЗАГАЛЬНИЙ ВИСНОВОК: 1–3 речення.
            - Останнє речення ЗАГАЛЬНОГО ВИСНОВКУ завжди: «Ця інформація є загальноосвітньою і не замінює консультацію лікаря.»
            """
    };

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

    public async Task<string> GetSummaryAsync(DateTime date, IReadOnlyList<AiSummaryItem> results, string lang = "uk", CancellationToken ct = default)
    {
        var grouped = results
            .GroupBy(r => GetCategory(r.TestName))
            .OrderBy(g => Array.FindIndex(CategoryRules, r => r.Label == g.Key))
            .ToList();

        var (dateLabel, refLabel, abnLabel) = lang switch
        {
            "en" => ("Lab results from", "ref", "ABNORMAL"),
            "ru" => ("Результаты анализов от", "норма", "ОТКЛОНЕНИЕ"),
            _    => ("Результати аналізів від",  "норма", "ВІДХИЛЕННЯ"),
        };

        var sb = new StringBuilder();
        sb.AppendLine($"{dateLabel} {date:dd.MM.yyyy}:");
        sb.AppendLine();

        foreach (var group in grouped)
        {
            sb.AppendLine($"[{group.Key}]");
            foreach (var item in group)
            {
                var line = $"  {item.TestName}: {item.Value} {item.Unit}";
                if (!string.IsNullOrWhiteSpace(item.ReferenceRange))
                    line += $" ({refLabel}: {item.ReferenceRange})";
                if (item.IsAbnormal)
                    line += $" ← {abnLabel}";
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
                new { role = "system", content = GetSystemPrompt(lang) },
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
