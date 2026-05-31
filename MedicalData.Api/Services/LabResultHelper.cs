using System.Globalization;
using System.Text.RegularExpressions;

namespace MedicalData.Api.Services;

public static partial class LabResultHelper
{
    // Parses common reference range formats and returns true if value is outside
    public static bool IsOutOfRange(double value, string? referenceRange)
    {
        if (string.IsNullOrWhiteSpace(referenceRange)) return false;
        var r = referenceRange.Trim();

        // "< N" or "< N.N"
        if (r.StartsWith("<=", StringComparison.Ordinal))
        {
            if (TryParse(r[2..], out var t)) return value > t;
        }
        else if (r.StartsWith('<'))
        {
            if (TryParse(r[1..], out var t)) return value >= t;
        }
        // "> N"
        else if (r.StartsWith(">=", StringComparison.Ordinal))
        {
            if (TryParse(r[2..], out var t)) return value < t;
        }
        else if (r.StartsWith('>'))
        {
            if (TryParse(r[1..], out var t)) return value <= t;
        }
        // "до N" (Ukrainian)
        else if (r.StartsWith("до ", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParse(r[3..], out var t)) return value > t;
        }
        else
        {
            // Try range: "3.5 – 5.0", "3.5 - 5.0", "3.5–5.0"
            var m = RangeRegex().Match(r);
            if (m.Success &&
                TryParse(m.Groups[1].Value, out var lo) &&
                TryParse(m.Groups[2].Value, out var hi))
            {
                return value < lo || value > hi;
            }
        }
        return false;
    }

    private static bool TryParse(string s, out double result) =>
        double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);

    [GeneratedRegex(@"^(-?[\d.,]+)\s*[–\-]\s*(-?[\d.,]+)$")]
    private static partial Regex RangeRegex();
}
