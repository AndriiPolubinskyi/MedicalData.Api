using Microsoft.Extensions.Logging.Abstractions;
using MedicalData.Api.Services;

namespace MedicalData.Tests;

public class PdfParsingOutputTests
{
    private static readonly LabPdfAgentParser Parser = new(NullLogger<LabPdfAgentParser>.Instance);

    public static readonly string OutputFile = "/tmp/pdf_parse_results.txt";

    [Fact]
    public async Task PrintAllPdfResults()
    {
        var lines = new List<string>();
        void W(string s) => lines.Add(s);

        var pdfs = Directory.GetFiles(TestConfig.ExamplesPath, "*.pdf")
            .OrderBy(Path.GetFileName)
            .ToList();

        foreach (var pdfPath in pdfs)
        {
            var fileName = Path.GetFileNameWithoutExtension(pdfPath);
            await using var stream = File.OpenRead(pdfPath);
            var result = await Parser.ParseAsync(stream);

            var date = result.Date?.ToString("dd.MM.yyyy") ?? "—";
            W("");
            W($"┌─ {fileName}");
            W($"│  Дата: {date}   ({result.Results.Count} показателей)");
            W("│");
            W($"│  {"Дата",-12} {"Показатель",-55} {"Значение",10}  Ед.");
            W("│  " + new string('─', 85));

            foreach (var r in result.Results.OrderBy(x => x.TestName))
            {
                var flag = r.IsAbnormal ? " ⚠" : "";
                W($"│  {date,-12} {r.TestName,-55} {r.Value,10}  {r.Unit}{flag}");
            }

            W("└" + new string('─', 87));
        }

        await File.WriteAllLinesAsync(OutputFile, lines);
    }
}
