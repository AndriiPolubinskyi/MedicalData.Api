using MedicalData.Client;
using MedicalData.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── Auth services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthHttpHandler>();

// ── HTTP client (with Bearer token) ──────────────────────────────────────────
var apiBase = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services
    .AddHttpClient("api", c => c.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<AuthHttpHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"));

// ── Localization ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<LocalizationService>();

// ── Profile ───────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ProfileService>();

// ── UI ────────────────────────────────────────────────────────────────────────
builder.Services.AddFluentUIComponents();

builder.Logging.SetMinimumLevel(LogLevel.Information);

await builder.Build().RunAsync();
