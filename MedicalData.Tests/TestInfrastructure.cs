using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace MedicalData.Tests;

internal static class TestConfig
{
    private static readonly Lazy<IConfiguration> _config = new(BuildConfiguration);

    public static IConfiguration Instance => _config.Value;

    public static string? AnthropicKey => Instance["Anthropic:ApiKey"];
    public static string AnthropicModel => Instance["Anthropic:Model"] ?? "claude-haiku-4-5";

    private static IConfiguration BuildConfiguration()
    {
        var settingsPath = Path.Combine(SolutionRoot, "MedicalData.Tests", "appsettings.Test.json");

        return new ConfigurationBuilder()
            .AddJsonFile(settingsPath, optional: true)
            .AddEnvironmentVariables()                  // Anthropic__ApiKey
            .Build();
    }

    public static string ExamplesPath =>
        Path.Combine(SolutionRoot, "MedicalData.Api", "Examples");

    public static string SolutionRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !dir.GetFiles("*.sln").Any())
                dir = dir.Parent;
            return dir?.FullName
                   ?? throw new InvalidOperationException("Could not find solution root from " + AppContext.BaseDirectory);
        }
    }
}

/// <summary>
/// Skips the test if Anthropic API key is not configured.
/// Set Anthropic__ApiKey env var or fill MedicalData.Tests/appsettings.Test.json.
/// </summary>
public sealed class RequiresAnthropicFactAttribute : FactAttribute
{
    public RequiresAnthropicFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(TestConfig.AnthropicKey))
            Skip = "Anthropic API key not configured. Set Anthropic__ApiKey env var or fill MedicalData.Tests/appsettings.Test.json";
    }
}

public sealed class RequiresAnthropicTheoryAttribute : TheoryAttribute
{
    public RequiresAnthropicTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(TestConfig.AnthropicKey))
            Skip = "Anthropic API key not configured. Set Anthropic__ApiKey env var or fill MedicalData.Tests/appsettings.Test.json";
    }
}
