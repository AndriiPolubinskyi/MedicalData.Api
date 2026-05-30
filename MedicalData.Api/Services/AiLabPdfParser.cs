using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Messages;

namespace MedicalData.Api.Services;

public class AiLabPdfParser : ILabPdfAgentParser
{
    private readonly AnthropicClient _client;
    private readonly string _model;

    private const string SystemPrompt = """
        You are a medical lab report parser specializing in Ukrainian lab reports.
        Extract ONLY actual laboratory test results from the provided PDF.

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
        - Patient info (ПІБ, дата народження, вік, стать)
        - Clinic/lab info (адреса, телефон, назва лабораторії, ліцензія, www, mail)
        - Section headers: Ліпідограма, Загальний аналіз крові, Біохімічне дослідження, ЛЕЙКОЦИТАРНА ФОРМУЛА, Імуноферментний аналіз
        - Reference range sub-rows: Дорослі: до..., до 1 року:..., чол., жін., Умовний ризик, Високий ризик, Первинна проба, Сироватка крові
        - Footer/disclaimer text (Результати досліджень не є..., Документ надруковано, Стор.)
        - Dates used as metadata (дата реєстрації, дата валідації, дата отримання, дата видачі)

        DATE RULES:
        - date: use "Дата видачі" (issue date) or "Дата валідації" (validation date) as the report date
        - Fall back to "Дата отримання матеріалу" or "Дата реєстрації" only if nothing else found
        - Return as YYYY-MM-DD

        TEST NAME NORMALIZATION — use a canonical SHORT Ukrainian name + standard abbreviation:
        - "Холестерин (Total Blood Cholesterol, ХС, CHOL)" → "Холестерин (CHOL)"
        - "Холестерин (Cholesterol, total)" → "Холестерин (CHOL)"
        - "Тригліцериди (Triglyceride, ТГ, TG)" → "Тригліцериди (TG)"
        - "Тригліцериди (Triglycerides)" → "Тригліцериди (TG)"
        - "Холестерин ліпопротеїдів високої щільності (...HDL...)" → "Холестерин ЛПВЩ (HDL)"
        - "Холестерин ХЛПВЩ (...HDL...)" → "Холестерин ЛПВЩ (HDL)"
        - "Холестерин ліпопротеїдів низької щільності (...LDL...)" → "Холестерин ЛПНЩ (LDL)"
        - "Холестерин ХЛПНЩ (...LDL...)" → "Холестерин ЛПНЩ (LDL)"
        - "Холестерин ліпопротеїдів дуже низької щільності (...VLDL...)" → "Холестерин ЛПДНЩ (VLDL)"
        - "Холестерин ХЛПДНЩ" → "Холестерин ЛПДНЩ (VLDL)"
        - "Холестерин не-ліпопротеїдів високої щільності (...Non–HDL-C...)" → "Холестерин не-ЛПВЩ (Non-HDL)"
        - "Індекс атерогенності (ІА, АІР)" → "Індекс атерогенності"
        - "Коефіцієнт атерогенності" → "Коефіцієнт атерогенності"
        - "Глюкоза крові (Glucose)" → "Глюкоза (GLU)"
        - "Глюкоза (Glucose)" → "Глюкоза (GLU)"
        - "Інсулін (INS)" → "Інсулін (INS)"
        - "Тестостерон вільний (TEST Free)" → "Тестостерон вільний (Free T)"
        - "Аланінамінотрансфераза (АLТ)" → "АЛТ (ALT)"
        - "Аспартатамінотрансфераза (АSТ)" → "АСТ (AST)"
        - "Гама-глутамілтрансфераза (GGT)" → "ГГТ (GGT)"
        - "Лужна фосфатаза (Alkaline phosphatase)" → "Лужна фосфатаза (ALP)"
        - "Загальний білок (Protein, total)" → "Загальний білок (TP)"
        - "Білірубін загальний (Bilirubin, total)" → "Білірубін загальний (TBIL)"
        - "Білірубін прямий (Bilirubin, direct)" → "Білірубін прямий (DBIL)"
        - "Білірубін непрямий" → "Білірубін непрямий (IBIL)"
        - "Лейкоцити (WBS)" → "Лейкоцити (WBC)"
        - "Еритроцити (RBC)" → "Еритроцити (RBC)"
        - "Гемоглобін (HGB)" → "Гемоглобін (HGB)"
        - "Тромбоцити (PLT)" → "Тромбоцити (PLT)"
        - "Гематокрит (HCT)" → "Гематокрит (HCT)"
        - "ШОЕ за Панченком" → "ШОЕ (ESR)"
        - For percentages in the leukocyte formula, keep names as-is but normalize: "Паличкоядерні нейтрофіли (%)" → "Нейтрофіли паличкоядерні (%)"

        VALUE RULES:
        - value: decimal number with DOT separator (e.g., 5.65 not "5,65")
        - Ukrainian reports use COMMA as decimal — always convert: "1,56" → 1.56
        - Strip ! or !!! markers from value — they indicate out-of-range, are not part of the number
        - isAbnormal: true if value had ! or !!! marker in the report, false otherwise

        UNIT RULES:
        - unit: measurement unit only (e.g., "ммоль/л", "г/л", "Од/л", "%", "х10^9/л", "pg/ml")
        - For ШОЕ: if unit is missing, use "мм/год"

        REFERENCE RANGE:
        - referenceRange: as it appears in the report
        - If patient gender is чол. (male), prefer the male-specific range when the range is gender-split
        - e.g., for HDL: "0,78 - 1,81" (male range), not the female one
        - If a field is unknown or missing, use null or empty string
        """;

    public AiLabPdfParser(IConfiguration configuration)
    {
        var apiKey = configuration["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException(
                "Anthropic:ApiKey is not configured. Add it to appsettings.json or as environment variable.");

        _model = configuration["Anthropic:Model"] ?? "claude-haiku-4-5";
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<ParsedLabPdfDocument> ParseAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        pdfStream.Position = 0;
        using var ms = new MemoryStream();
        await pdfStream.CopyToAsync(ms, cancellationToken);
        var base64Pdf = Convert.ToBase64String(ms.ToArray());

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 4096,
            System = SystemPrompt,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new DocumentBlockParam { Source = new Base64PdfSource { Data = base64Pdf } },
                        new TextBlockParam { Text = "Parse this lab report and return JSON." },
                    },
                },
            ],
        });

        var json = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text ?? "";
        return ParseResponse(json);
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

            if (!string.IsNullOrWhiteSpace(dto.Date)
                && DateTime.TryParse(dto.Date, out var date))
            {
                doc.Date = date.Date;
            }

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
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("results")]
        public List<AiResultItem>? Results { get; set; }
    }

    private sealed class AiResultItem
    {
        [JsonPropertyName("testName")]
        public string? TestName { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("referenceRange")]
        public string? ReferenceRange { get; set; }

        [JsonPropertyName("isAbnormal")]
        public bool IsAbnormal { get; set; }
    }
}
