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

            Results are grouped by category. If patient data is provided (sex, age, cycle day) — use it actively:
            - Apply sex-specific and age-specific reference ranges where they differ (e.g. hormones, hemoglobin, ferritin, creatinine).
            - For women: if a cycle day is given, apply the appropriate phase-specific norms for hormonal indicators (FSH, LH, estradiol, progesterone, testosterone).
            - If a value looks abnormal by the general lab range but is within normal limits for this patient's sex/age/cycle phase, say so explicitly.
            - If patient data is absent, interpret using standard adult ranges.

            RESPONSE STRUCTURE:
            - Write ONLY the categories provided in the input. Do not invent new ones.
            - Start each block with the category name IN ALL CAPS on a new line.
            - 3–5 sentences per category.

            WHAT TO WRITE IN EACH BLOCK:
            - Explain what the key indicator measures and why it matters for health.
            - If abnormal: name the indicator, its value, the reference range, and what it means physiologically.
            - Where patient context changes the interpretation, state it explicitly (e.g. "For a 35-year-old woman on cycle day 14, this LH level is consistent with ovulation.").
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

            Если переданы данные пациента (пол, возраст, день цикла) — используй их активно:
            - Применяй половые и возрастные нормы там, где они отличаются (гормоны, гемоглобин, ферритин, креатинин и др.).
            - У женщин: если указан день цикла, применяй фазо-специфические нормы для гормональных показателей (ФСГ, ЛГ, эстрадиол, прогестерон, тестостерон).
            - Если значение выглядит отклонённым по общей лабораторной норме, но укладывается в норму для пола/возраста/фазы цикла пациента — скажи об этом прямо.
            - Если данные пациента не указаны — интерпретируй по стандартным взрослым нормам.

            СТРУКТУРА ОТВЕТА:
            - Пиши ТОЛЬКО те категории, которые переданы во входных данных.
            - Перед каждым блоком — название категории ЗАГЛАВНЫМИ БУКВАМИ с новой строки.
            - На каждую категорию — 3–5 предложений.

            ЧТО ПИСАТЬ:
            - Объясни, что измеряет ключевой показатель и зачем он важен.
            - При отклонении: назови показатель, значение, норму и физиологический смысл.
            - Если контекст пациента меняет интерпретацию — скажи об этом явно (например: «Для женщины 35 лет на 14-й день цикла такой уровень ЛГ соответствует норме овуляции.»).
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

            Якщо передані дані пацієнта (стать, вік, день циклу) — використовуй їх активно:
            - Застосовуй статево- та вікоспецифічні норми там, де вони відрізняються (гормони, гемоглобін, феритин, креатинін тощо).
            - У жінок: якщо вказаний день циклу, застосовуй фазоспецифічні норми для гормональних показників (ФСГ, ЛГ, естрадіол, прогестерон, тестостерон).
            - Якщо значення виглядає відхиленим за загальною лабораторною нормою, але вкладається в норму для статі/віку/фази циклу пацієнта — скажи про це прямо.
            - Якщо дані пацієнта не вказані — інтерпретуй за стандартними дорослими нормами.

            СТРУКТУРА ВІДПОВІДІ:
            - Пиши ТІЛЬКИ ті категорії, що передані у вхідних даних.
            - Перед кожним блоком — назва категорії ВЕЛИКИМИ ЛІТЕРАМИ з нового рядка.
            - На кожну категорію — 3–5 речень.

            ЩО ПИСАТИ:
            - Поясни, що вимірює ключовий показник і навіщо він важливий для здоров'я.
            - При відхиленні: назви показник, значення, норму та фізіологічний зміст.
            - Якщо контекст пацієнта змінює інтерпретацію — скажи про це явно (наприклад: «Для жінки 35 років на 14-й день циклу такий рівень ЛГ відповідає нормі овуляції.»).
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

    // Internal key (uk) → translated display labels per lang
    private static readonly (string Key, string[] Keywords)[] CategoryRules =
    [
        ("ЗАГАЛЬНИЙ АНАЛІЗ КРОВІ", ["гемоглобін", "еритроцит", "лейкоцит", "тромбоцит", "гематокрит", "шое", "лімфоцит", "моноцит", "базофіл", "еозинофіл", "сегментоядерн", "паличкоядерн", "метамієлоцит", "мієлоцит", "тромбокрит", "плазматичн", "hgb", "rbc", "wbc", "plt", "hct", "esr", "mcv", "mch", "rdw", "mpv", "pct", "pdw", "ly%", "mo%", "ba%", "eo%", "ne%"]),
        ("ЛІПІДОГРАМА",            ["холестерин", "тригліцерид", "ліпо", "лпвщ", "лпнщ", "лпднщ", "hdl", "ldl", "vldl", "chol", "атероген", "non-hdl", "не-лпвщ", "non–hdl"]),
        ("БІОХІМІЯ",               ["глюкоза", "білок", "білірубін", "алт", "аст", "лужна фосфатаза", "ггт", "лактатдегідрогеназа", "alt", "ast", "alp", "ggt", "ldh", "glu", "glucose", "tbil", "dbil", "ibil"]),
        ("ГОРМОНИ ТА ЕНДОКРИНОЛОГІЯ", ["інсулін", "глікозильован", "hba1c", "нома", "пептид", "тестостерон", "лютеїнізуючий", "глобулін", "fai", "testosterone", "insulin"]),
        ("ОНКОМАРКЕРИ",            ["пса", "psa", "простат"]),
        ("ІНФЕКЦІЙНА СЕРОЛОГІЯ",   ["хелікобактер", "helicobacter", "антитіл", "вірус", "інфекц"]),
    ];

    private static readonly Dictionary<string, Dictionary<string, string>> CategoryLabels = new()
    {
        ["en"] = new()
        {
            ["ЗАГАЛЬНИЙ АНАЛІЗ КРОВІ"]    = "COMPLETE BLOOD COUNT",
            ["ЛІПІДОГРАМА"]               = "LIPID PROFILE",
            ["БІОХІМІЯ"]                  = "BIOCHEMISTRY",
            ["ГОРМОНИ ТА ЕНДОКРИНОЛОГІЯ"] = "HORMONES & ENDOCRINOLOGY",
            ["ОНКОМАРКЕРИ"]               = "TUMOR MARKERS",
            ["ІНФЕКЦІЙНА СЕРОЛОГІЯ"]      = "SEROLOGY",
            ["ІНШЕ"]                      = "OTHER",
        },
        ["ru"] = new()
        {
            ["ЗАГАЛЬНИЙ АНАЛІЗ КРОВІ"]    = "ОБЩИЙ АНАЛИЗ КРОВИ",
            ["ЛІПІДОГРАМА"]               = "ЛИПИДОГРАММА",
            ["БІОХІМІЯ"]                  = "БИОХИМИЯ",
            ["ГОРМОНИ ТА ЕНДОКРИНОЛОГІЯ"] = "ГОРМОНЫ И ЭНДОКРИНОЛОГИЯ",
            ["ОНКОМАРКЕРИ"]               = "ОНКОМАРКЕРЫ",
            ["ІНФЕКЦІЙНА СЕРОЛОГІЯ"]      = "ИНФЕКЦИОННАЯ СЕРОЛОГИЯ",
            ["ІНШЕ"]                      = "ПРОЧЕЕ",
        },
    };

    private static string GetCategory(string testName)
    {
        var n = testName.ToLowerInvariant();
        foreach (var (key, keywords) in CategoryRules)
            if (keywords.Any(k => n.Contains(k)))
                return key;
        return "ІНШЕ";
    }

    private static string TranslateCategory(string key, string lang) =>
        CategoryLabels.TryGetValue(lang, out var map) && map.TryGetValue(key, out var v) ? v : key;

    public async Task<string> GetSummaryAsync(DateTime date, IReadOnlyList<AiSummaryItem> results, string lang = "uk", PatientContext? patient = null, CancellationToken ct = default)
    {
        var grouped = results
            .GroupBy(r => GetCategory(r.TestName))
            .OrderBy(g => Array.FindIndex(CategoryRules, r => r.Key == g.Key))
            .ToList();

        var (dateLabel, refLabel, abnLabel, patientLabel) = lang switch
        {
            "en" => ("Lab results from", "ref", "ABNORMAL", "Patient"),
            "ru" => ("Результаты анализов от", "норма", "ОТКЛОНЕНИЕ", "Пациент"),
            _    => ("Результати аналізів від", "норма", "ВІДХИЛЕННЯ", "Пацієнт"),
        };

        var sb = new StringBuilder();
        if (patient?.HasAny == true)
            sb.AppendLine($"{patientLabel}: {patient.Describe(lang)}");
        sb.AppendLine($"{dateLabel} {date:dd.MM.yyyy}:");
        sb.AppendLine();

        foreach (var group in grouped)
        {
            sb.AppendLine($"[{TranslateCategory(group.Key, lang)}]");
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

    public async Task<string> GetExplainAsync(string testName, double value, string unit, string referenceRange, string lang, PatientContext? patient = null, CancellationToken ct = default)
    {
        var isAbnormal = LabResultHelper.IsOutOfRange(value, referenceRange);

        var patientLine = patient?.HasAny == true
            ? lang switch
            {
                "en" => $"Patient: {patient.Describe(lang)}\n",
                "ru" => $"Пациент: {patient.Describe(lang)}\n",
                _    => $"Пацієнт: {patient.Describe(lang)}\n",
            }
            : "";

        var (systemPrompt, userMessage) = lang switch
        {
            "en" => (
                "You are a medical information assistant. Explain a single lab result to a patient in plain English. " +
                "If patient data is provided (sex, age, cycle day), use it actively: apply sex- and age-specific reference ranges, " +
                "and for women apply cycle-phase norms for hormonal indicators (FSH, LH, estradiol, progesterone, testosterone). " +
                "If the value looks abnormal by the general range but is within normal limits for this patient's profile, say so explicitly. " +
                "2-3 sentences: what this test measures and what the current value means for this specific patient. " +
                "No bullet points, no markdown, plain text only. " +
                "Always end with: \"This is general information only and is not a substitute for professional medical advice.\"",
                $"{patientLine}Test: {testName}\nValue: {value} {unit}\nReference range: {referenceRange}\nStatus: {(isAbnormal ? "ABNORMAL" : "normal")}"
            ),
            "ru" => (
                "Ты информационный медицинский ассистент. Объясни один показатель анализа пациенту простым языком. " +
                "Если переданы данные пациента (пол, возраст, день цикла) — используй их активно: применяй половые и возрастные нормы, " +
                "а у женщин при наличии дня цикла — фазо-специфические нормы для гормональных показателей (ФСГ, ЛГ, эстрадиол, прогестерон, тестостерон). " +
                "Если значение выглядит отклонённым по общей норме, но вписывается в норму для профиля этого пациента — скажи об этом прямо. " +
                "2-3 предложения: что измеряет показатель и что текущее значение означает именно для этого пациента. " +
                "Без списков, без markdown, только обычный текст. " +
                "Всегда заканчивай словами: «Ця інформація є загальноосвітньою і не замінює консультацію лікаря.»",
                $"{patientLine}Показатель: {testName}\nЗначение: {value} {unit}\nНорма: {referenceRange}\nСтатус: {(isAbnormal ? "ОТКЛОНЕНИЕ" : "в норме")}"
            ),
            _ => (
                "Ти інформаційний медичний асистент. Поясни один показник аналізу пацієнту простою мовою. " +
                "Якщо передані дані пацієнта (стать, вік, день циклу) — використовуй їх активно: застосовуй статево- та вікоспецифічні норми, " +
                "а у жінок при наявності дня циклу — фазоспецифічні норми для гормональних показників (ФСГ, ЛГ, естрадіол, прогестерон, тестостерон). " +
                "Якщо значення виглядає відхиленим за загальною нормою, але вкладається в норму для профілю цього пацієнта — скажи про це прямо. " +
                "2-3 речення: що вимірює показник і що поточне значення означає саме для цього пацієнта. " +
                "Без списків, без markdown, тільки звичайний текст. " +
                "Завжди закінчуй словами: «Ця інформація є загальноосвітньою і не замінює консультацію лікаря.»",
                $"{patientLine}Показник: {testName}\nЗначення: {value} {unit}\nНорма: {referenceRange}\nСтатус: {(isAbnormal ? "ВІДХИЛЕННЯ" : "в нормі")}"
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
