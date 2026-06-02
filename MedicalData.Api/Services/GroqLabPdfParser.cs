using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MedicalData.Api.Services;

public class GroqLabPdfParser : ILabPdfAgentParser
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    private const string SystemPrompt = """
        You are a medical lab report parser. The text was extracted from a PDF and may come from any laboratory format.
        Extract ONLY actual laboratory test results.

        Return a valid JSON object (no markdown, no code blocks) with this exact structure:
        {
          "date": "YYYY-MM-DD or null",
          "results": [
            {
              "testName": "canonical test name",
              "value": 0.0,
              "unit": "unit of measurement",
              "referenceRange": "reference range text",
              "isAbnormal": false
            }
          ]
        }

        STRICT RULES — WHAT TO EXCLUDE:
        - Order/registration numbers (Замовлення, Код ЄДРПОУ, Номер замовлення)
        - Patient info (ПІБ, дата народження, вік, стать, age, gender, patient name)
        - Clinic/lab info (address, phone, lab name, license, www, email)
        - Section headers: Ліпідограма, Загальний аналіз крові, Біохімічне дослідження, ЛЕЙКОЦИТАРНА ФОРМУЛА
        - Reference range sub-rows: Дорослі: до..., до 1 року:..., чол., жін., Умовний ризик, Первинна проба, Сироватка крові
        - Footer/disclaimer text
        - Dates used as metadata (дата реєстрації, дата валідації, дата отримання, дата видачі)

        DATE RULES:
        - date: use "Дата видачі" or "Дата валідації" as the report date, fall back to "Дата отримання" or "Дата реєстрації"
        - Return as YYYY-MM-DD

        TEST NAME NORMALIZATION — use canonical SHORT Ukrainian name + abbreviation:
        - Any "Холестерин...CHOL..." → "Холестерин (CHOL)"
        - Any "Тригліцериди...TG..." → "Тригліцериди (TG)"
        - Any "...HDL..." → "Холестерин ЛПВЩ (HDL)"
        - Any "...LDL..." → "Холестерин ЛПНЩ (LDL)"
        - Any "...VLDL..." → "Холестерин ЛПДНЩ (VLDL)"
        - Any "Non-HDL..." → "Холестерин не-ЛПВЩ (Non-HDL)"
        - Any "Індекс атерогенності..." → "Індекс атерогенності"
        - Any "Глюкоза...GLU..." or "Glucose" → "Глюкоза (GLU)"
        - Any "Інсулін..." → "Інсулін (INS)"
        - "АЛТ" or "Аланін..." → "АЛТ (ALT)"
        - "АСТ" or "Аспартат..." → "АСТ (AST)"
        - "ГГТ" or "Гама-глутаміл..." → "ГГТ (GGT)"
        - "Лужна фосфатаза..." → "Лужна фосфатаза (ALP)"
        - "Загальний білок..." → "Загальний білок (TP)"
        - "Білірубін загальний..." → "Білірубін загальний (TBIL)"
        - "Білірубін прямий..." → "Білірубін прямий (DBIL)"
        - "Білірубін непрямий..." → "Білірубін непрямий (IBIL)"
        - "Лейкоцити...WBC..." → "Лейкоцити (WBC)"
        - "Еритроцити...RBC..." → "Еритроцити (RBC)"
        - "Гемоглобін...HGB..." → "Гемоглобін (HGB)"
        - "Тромбоцити...PLT..." → "Тромбоцити (PLT)"
        - "Гематокрит...HCT..." → "Гематокрит (HCT)"
        - "ШОЕ..." or "ESR..." → "ШОЕ (ESR)"
        - "Тестостерон вільний..." → "Тестостерон вільний (Free T)"
        - Keep English test names as-is if no Ukrainian equivalent is clear

        VALUE RULES:
        - value: decimal number with DOT separator
        - Ukrainian reports use COMMA as decimal — always convert: "1,56" → 1.56
        - Strip ! or !!! markers — they indicate out-of-range, not part of the number
        - isAbnormal: true if value had ! or !!! marker, false otherwise

        UNIT RULES:
        - unit: measurement unit only (e.g., "ммоль/л", "г/л", "%", "х10^9/л", "pg/ml")
        - For ШОЕ: if unit is missing, use "мм/год"

        REFERENCE RANGE:
        - referenceRange: as it appears in the report
        - Prefer male-specific range if patient gender is male (чол.)
        - Use null or empty string if unknown
        """;

    public GroqLabPdfParser(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _apiKey = configuration["Groq:ApiKey"]
            ?? throw new InvalidOperationException(
                "Groq:ApiKey is not configured. Add it to appsettings.json or as environment variable.");

        _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
        _http = httpClientFactory.CreateClient("groq");
    }

    public async Task<ParsedLabPdfDocument> ParseAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        var text = ExtractText(pdfStream);

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"Parse this lab report and return JSON:\n\n{text}" },
            },
            response_format = new { type = "json_object" },
            temperature = 0,
            max_tokens = 8000,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey) },
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
        };

        var httpResponse = await _http.SendAsync(request, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        var (json, truncated) = ExtractGroqText(responseJson);

        // If the response was cut off mid-JSON (e.g. very large PDF), retry with a higher limit
        if (truncated && !string.IsNullOrWhiteSpace(json))
        {
            var retryBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = $"Parse this lab report and return JSON:\n\n{text}" },
                },
                response_format = new { type = "json_object" },
                temperature = 0,
                max_tokens = 32000,
            };
            var retryReq = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
            {
                Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey) },
                Content = new StringContent(JsonSerializer.Serialize(retryBody), Encoding.UTF8, "application/json"),
            };
            var retryResp = await _http.SendAsync(retryReq, cancellationToken);
            if (retryResp.IsSuccessStatusCode)
            {
                (json, _) = ExtractGroqText(await retryResp.Content.ReadAsStringAsync(cancellationToken));
            }
        }

        return ParseResponse(json);
    }

    private static string ExtractText(Stream pdfStream)
    {
        pdfStream.Position = 0;
        var sb = new StringBuilder();

        using var pdf = PdfDocument.Open(pdfStream);
        foreach (var page in pdf.GetPages())
        {
            var lines = BuildLinesFromPage(page);
            foreach (var line in lines)
                sb.AppendLine(line);
        }

        return sb.ToString().Trim();
    }

    // Groups words by visual row (Y coordinate with tolerance), then sorts by X.
    // This correctly reconstructs multi-column table rows.
    private static List<string> BuildLinesFromPage(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0)
            return page.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        // Tolerance of 5 points groups words at the same visual height.
        // Some clinics (e.g. Клініка Денис) have slight Y variation between columns.
        const double rowTolerance = 5.0;

        var rows = words
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / rowTolerance) * rowTolerance)
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return rows;
    }

    private static (string text, bool truncated) ExtractGroqText(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var choice = doc.RootElement.GetProperty("choices")[0];
            var text = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
            var finishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
            return (text, finishReason == "length");
        }
        catch
        {
            return ("", false);
        }
    }

    private static ParsedLabPdfDocument ParseResponse(string json)
    {
        var doc = new ParsedLabPdfDocument();
        if (string.IsNullOrWhiteSpace(json)) return doc;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<AiResponseDto>(json, options);
            if (dto == null) return doc;

            if (!string.IsNullOrWhiteSpace(dto.Date) && DateTime.TryParse(dto.Date, out var date))
                doc.Date = date.Date;

            foreach (var item in dto.Results ?? [])
            {
                if (string.IsNullOrWhiteSpace(item.TestName)) continue;
                doc.Results.Add(new ParsedLabMetric
                {
                    TestName = item.TestName.Trim(),
                    Value = item.Value,
                    Unit = item.Unit?.Trim() ?? string.Empty,
                    ReferenceRange = item.ReferenceRange?.Trim() ?? string.Empty,
                    IsAbnormal = item.IsAbnormal,
                });
            }
        }
        catch (JsonException) { }

        return doc;
    }

    private sealed class AiResponseDto
    {
        [JsonPropertyName("date")] public string? Date { get; set; }
        [JsonPropertyName("results")] public List<AiResultItem>? Results { get; set; }
    }

    private sealed class AiResultItem
    {
        [JsonPropertyName("testName")] public string? TestName { get; set; }
        [JsonPropertyName("value")] public double Value { get; set; }
        [JsonPropertyName("unit")] public string? Unit { get; set; }
        [JsonPropertyName("referenceRange")] public string? ReferenceRange { get; set; }
        [JsonPropertyName("isAbnormal")] public bool IsAbnormal { get; set; }
    }
}
