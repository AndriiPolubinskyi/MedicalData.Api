using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MedicalData.Client.Models;
using UglyToad.PdfPig;

namespace MedicalData.Client.Services;

public class PdfService
{
    public List<LabResult> Parse(byte[] pdfBytes)
    {
        var results = new List<LabResult>();
        var sb = new StringBuilder();

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));
        foreach (var page in document.GetPages())
        {
            var words = page.GetWords();
            sb.AppendLine(page.Text);
        }

        var text = sb.ToString();

        // Извлечение общей информации (если найдется)
        var dateMatch = Regex.Match(text, @"Дата реєстрації:\s*(\d{2}\.\d{2}\.\d{4})");
        var patientMatch = Regex.Match(text, @"Пацієнт:\s*([^\n\r]+)");
        var testDate = dateMatch.Success ? DateTime.ParseExact(dateMatch.Groups[1].Value, "dd.MM.yyyy", CultureInfo.InvariantCulture) : DateTime.Now;
        var patientName = patientMatch.Success ? patientMatch.Groups[1].Value.Trim() : "Невідомо";

        var start = text.IndexOf("РЕФЕРЕНТНІ ЗНАЧЕННЯ", StringComparison.OrdinalIgnoreCase);
        var end = text.IndexOf("ПРИМІТКИ", StringComparison.OrdinalIgnoreCase);

        string analysisBlock = "";
        if (start != -1 && end != -1 && end > start)
        {
            analysisBlock = text.Substring(start, end - start);
        }
        
        var lineRegex = new Regex(@"([\d.,]+\s+\S+\s+[^ХА-ЯA-Z\n\r]{4,})(?=\d|$)", RegexOptions.Multiline);
        var lines = lineRegex.Matches(analysisBlock).Select(m => m.Value.Trim()).ToList();
        
        
        // // Универсальный шаблон: строка, в которой есть название, значение (число), единица, референс
        // // Пример: "Глюкоза        5.1        ммоль/л      3.9 - 5.9"
        // var lineRegex = new Regex(
        //     @"(?<name>.+?)\s+(?<value>[\d.,]+)\s+(?<unit>\S+)\s+(?<ref>[\d\s\-,.<≥≤]+)",
        //     RegexOptions.Multiline);
        //
        // var matches = lineRegex.Matches(text);
        // Console.WriteLine($"Найдено совпадений: {matches.Count}");

        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"(?<value>[\d.,]+)\s+(?<unit>\S+)\s+(?<ref>.+?)[:;]\s*(?<name>.+)$");
            if (!match.Success) continue;

            var value = double.Parse(match.Groups["value"].Value.Replace(",", "."), CultureInfo.InvariantCulture);

            results.Add(new LabResult
            {
                TestName = match.Groups["name"].Value.Trim(),
                Value = value,
                Unit = match.Groups["unit"].Value.Trim(),
                ReferenceRange = match.Groups["ref"].Value.Trim(),
                TestDate = testDate,
                PatientName = patientName
            });
        }
        
        return results;
    }
    
    public List<LabResult> ParseLabResults(byte[] pdfBytes)
    {
        
        var results = new List<LabResult>();
        string text;

        using (var doc = PdfDocument.Open(new MemoryStream(pdfBytes)))
        {
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);
            text = sb.ToString();
        }

        var dateMatch = Regex.Match(text, @"Дата реєстрації: (\d{2}\.\d{2}\.\d{4})");
        var patientMatch = Regex.Match(text, @"Пацієнт:\s*(.+?),");

        DateTime testDate = dateMatch.Success ? DateTime.Parse(dateMatch.Groups[1].Value) : DateTime.Now;
        string patient = patientMatch.Success ? patientMatch.Groups[1].Value.Trim() : "Невідомо";

        var pattern = new Regex(@"(?<name>.+?)\s+(?<value>\d+[.,]?\d*)\s+ммоль/л\s+(?<ref>[\d<>\-., ]+)", RegexOptions.Multiline);

        foreach (Match match in pattern.Matches(text))
        {
            results.Add(new LabResult
            {
                TestName = match.Groups["name"].Value.Trim(),
                Value = double.Parse(match.Groups["value"].Value.Replace(",", "."), CultureInfo.InvariantCulture),
                Unit = "ммоль/л",
                ReferenceRange = match.Groups["ref"].Value.Trim(),
                TestDate = testDate,
                PatientName = patient
            });
        }

        return results;
    }
}