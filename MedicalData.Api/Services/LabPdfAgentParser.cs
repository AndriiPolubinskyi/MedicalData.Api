using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MedicalData.Api.Services;

public interface ILabPdfAgentParser
{
    Task<ParsedLabPdfDocument> ParseAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}

public class ParsedLabPdfDocument
{
    public DateTime? Date { get; set; }
    public List<ParsedLabMetric> Results { get; set; } = new();
}

public class ParsedLabMetric
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public bool IsAbnormal { get; set; }
}

public class LabPdfAgentParser : ILabPdfAgentParser
{
    private static readonly Regex MultiSpaceRegex = new("\\s{2,}", RegexOptions.Compiled);
    private static readonly Regex AbnormalMarkerRegex = new("\\s*!+\\s*", RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(
        "\\b(?<date>(?:\\d{2}[./-]\\d{2}[./-]\\d{4})|(?:\\d{4}[./-]\\d{2}[./-]\\d{2}))\\b",
        RegexOptions.Compiled);
    private static readonly Regex ResultLineRegex = new(
        "^(?<name>.+?)\\s+(?<value>-?\\d+(?:[.,]\\d+)?)\\s*(?<unit>[A-Za-zА-Яа-яІіЇїЄєЁё%/\\-\\.µμ]*)\\s*(?<range>(?:\\d+(?:[.,]\\d+)?\\s*[-–]\\s*\\d+(?:[.,]\\d+)?).*)?$",
        RegexOptions.Compiled);

    public Task<ParsedLabPdfDocument> ParseAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        if (!pdfStream.CanSeek)
        {
            throw new InvalidOperationException("PDF stream must be seekable.");
        }

        pdfStream.Position = 0;

        var parsed = new ParsedLabPdfDocument();
        var textLines = new List<string>();

        using (var pdf = PdfDocument.Open(pdfStream))
        {
            foreach (var page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lines = ExtractPageLines(page);
                textLines.AddRange(lines);
            }
        }

        parsed.Date = ExtractDate(textLines);
        parsed.Results = ExtractMetrics(textLines);

        return Task.FromResult(parsed);
    }

    private static List<string> ExtractPageLines(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count > 0)
        {
            var rows = words
                .GroupBy(x => Math.Round(x.BoundingBox.Bottom, 1))
                .OrderByDescending(x => x.Key)
                .ToList();

            var wordLines = rows
                .Select(x => NormalizeLine(string.Join(" ", x.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text))))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (wordLines.Count > 0)
            {
                return wordLines;
            }
        }

        var letters = page.Letters.ToList();
        if (letters.Count == 0)
        {
            return page.Text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeLine)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        var groupedRows = letters
            .GroupBy(x => Math.Round(x.GlyphRectangle.Bottom, 1))
            .OrderByDescending(x => x.Key)
            .ToList();

        var result = new List<string>(groupedRows.Count);
        foreach (var row in groupedRows)
        {
            var rowLetters = row.OrderBy(x => x.GlyphRectangle.Left).ToList();
            var line = BuildTextFromLetters(rowLetters);
            if (!string.IsNullOrWhiteSpace(line))
            {
                result.Add(line);
            }
        }

        if (result.Count == 0)
        {
            return page.Text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeLine)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        return result;
    }

    private static string BuildTextFromLetters(IReadOnlyList<Letter> letters)
    {
        if (letters.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(letters.Count + 16);
        var avgWidth = letters.Select(x => x.GlyphRectangle.Width).DefaultIfEmpty(3d).Average();

        for (var i = 0; i < letters.Count; i++)
        {
            if (i > 0)
            {
                var prev = letters[i - 1];
                var curr = letters[i];
                var gap = curr.GlyphRectangle.Left - prev.GlyphRectangle.Right;
                if (gap > avgWidth * 0.9)
                {
                    builder.Append(' ');
                }
            }

            var ch = letters[i].Value;
            if (!string.Equals(ch, "\r", StringComparison.Ordinal) && !string.Equals(ch, "\n", StringComparison.Ordinal))
            {
                builder.Append(ch);
            }
        }

        return NormalizeLine(builder.ToString());
    }

    private static DateTime? ExtractDate(IEnumerable<string> lines)
    {
        var parsedDates = new List<DateTime>();

        foreach (var line in lines)
        {
            var matches = DateRegex.Matches(line);
            if (matches.Count == 0)
            {
                continue;
            }

            foreach (Match match in matches)
            {
                var rawDate = match.Groups["date"].Value;
                var normalized = rawDate.Replace('.', '-').Replace('/', '-');
                var formats = new[] { "dd-MM-yyyy", "yyyy-MM-dd" };

                if (DateTime.TryParseExact(
                        normalized,
                        formats,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out var parsedDate)
                    && parsedDate.Year >= 2018
                    && parsedDate.Year <= DateTime.UtcNow.Year + 1)
                {
                    parsedDates.Add(parsedDate.Date);
                }
            }
        }

        if (parsedDates.Count == 0)
        {
            return null;
        }

        return parsedDates.Max();
    }

    private static List<ParsedLabMetric> ExtractMetrics(IEnumerable<string> lines)
    {
        var results = new List<ParsedLabMetric>();
        var seenTestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedLines = lines.Select(NormalizeLine).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        foreach (var line in normalizedLines)
        {
            if (line.Length < 5 || IsHeaderLike(line))
            {
                continue;
            }

            var isAbnormal = AbnormalMarkerRegex.IsMatch(line);
            var cleanLine = isAbnormal ? AbnormalMarkerRegex.Replace(line, " ").Trim() : line;

            var match = ResultLineRegex.Match(cleanLine);
            if (!match.Success)
            {
                continue;
            }

            var testName = NormalizeLine(match.Groups["name"].Value);
            if (!IsLikelyLabMetricName(testName))
            {
                continue;
            }

            var valueRaw = match.Groups["value"].Value.Replace(',', '.');
            if (!double.TryParse(valueRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            if (!IsReasonableMedicalValue(value))
            {
                continue;
            }

            if (!seenTestNames.Add(testName))
            {
                continue;
            }

            results.Add(new ParsedLabMetric
            {
                TestName = testName,
                Value = value,
                Unit = match.Groups["unit"].Value.Trim(),
                ReferenceRange = match.Groups["range"].Value.Trim(),
                IsAbnormal = isAbnormal
            });
        }

        // Fallback: if no strict line matches, try parsing rows where unit/range columns are missing.
        if (results.Count == 0)
        {
            foreach (var line in normalizedLines)
            {
                if (line.Length < 5 || IsHeaderLike(line))
                {
                    continue;
                }

                var fallback = ExtractByLastNumericToken(line);
                if (fallback == null)
                {
                    continue;
                }

                if (!IsReasonableMedicalValue(fallback.Value))
                {
                    continue;
                }

                if (!IsLikelyLabMetricName(fallback.TestName))
                {
                    continue;
                }

                if (!seenTestNames.Add(fallback.TestName))
                {
                    continue;
                }

                results.Add(fallback);
            }
        }

        return results;
    }

    private static bool IsLikelyLabMetricName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !name.Any(char.IsLetter) || name.Length < 3)
        {
            return false;
        }

        var lowered = name.ToLowerInvariant();
        var blockedTokens = new[]
        {
            "вул.", "київ", "тел", "лаборатор", "відділен", "ліцензі", "адрес", "пацієнт", "років",
            "замовлення", "єдрпоу", "дорослі", "до 1 року", "1-19 р",
            "чол.", "жін.", "первинна", "сироватка", "лікар", "виконавець", "ніколаб",
            // units accidentally captured inside a test name mean the line is a mixed/broken row
            "ммоль/л", "мкмоль", "мг/дл", "г/л", "мг/л", "нмоль", "пг/мл", "мкг/л", "мл/хв",
            "www.", "mail@", "стор.", "ф 7."
        };

        if (blockedTokens.Any(lowered.Contains))
        {
            return false;
        }

        var letterCount = name.Count(char.IsLetter);
        var digitCount = name.Count(char.IsDigit);
        return letterCount >= 3 && digitCount <= letterCount;
    }

    // Medical lab values are always in a reasonable numeric range
    private static bool IsReasonableMedicalValue(double value) => value is > -1000 and < 10000;

    private static ParsedLabMetric? ExtractByLastNumericToken(string line)
    {
        var numberMatches = Regex.Matches(line, "-?\\d+(?:[.,]\\d+)?");
        if (numberMatches.Count == 0)
        {
            return null;
        }

        var valueToken = numberMatches[numberMatches.Count - 1];
        var name = NormalizeLine(line[..valueToken.Index]);
        if (string.IsNullOrWhiteSpace(name) || !name.Any(char.IsLetter))
        {
            return null;
        }

        var valueRaw = valueToken.Value.Replace(',', '.');
        if (!double.TryParse(valueRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        var tail = NormalizeLine(line[(valueToken.Index + valueToken.Length)..]);
        return new ParsedLabMetric
        {
            TestName = name,
            Value = value,
            Unit = tail
        };
    }

    private static string NormalizeLine(string value)
    {
        var noLineBreaks = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return MultiSpaceRegex.Replace(noLineBreaks, " ");
    }

    private static bool IsHeaderLike(string line)
    {
        var lowered = line.ToLowerInvariant();
        return lowered.Contains("рефер")
               || lowered.Contains("показник")
               || lowered.Contains("результ")
               || lowered.Contains("одиниц")
               || lowered.Contains("первинна проба")
               || lowered.Contains("сироватка")
               || lowered.Contains("замовлення")
               || lowered.Contains("єдрпоу")
               || lowered.Contains("ліцензі")
               || lowered.Contains("свідоцтво")
               || lowered.Contains("лікар-лаборант")
               || lowered.Contains("виконавець")
               || lowered.Contains("дата реєстрації")
               || lowered.Contains("дата валідації")
               || lowered.Contains("стор.")
               || lowered.Contains("www.")
               || lowered.Contains("mail@")
               || lowered.Contains("ліпідограма")
               || lowered.Contains("загальний аналіз")
               || lowered.Contains("біохімічне");
    }
}









