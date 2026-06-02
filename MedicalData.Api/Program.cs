using MedicalData.Api.Data;
using MedicalData.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("App", "MedicalData.Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");

var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL");
if (!string.IsNullOrWhiteSpace(seqUrl))
    logConfig.WriteTo.Seq(seqUrl, apiKey: Environment.GetEnvironmentVariable("SEQ_API_KEY"));

Log.Logger = logConfig.CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── Database ──────────────────────────────────────────────────────────────────
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();

    var cs = builder.Configuration.GetConnectionString("DefaultConnection")
             ?? $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
                $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
                $"Password={Environment.GetEnvironmentVariable("DB_PASS")};" +
                "SSL Mode=Require;Trust Server Certificate=true;";
    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// ── DataProtection (persist keys in DB so Render restarts don't break OAuth) ──
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// ── Auth ──────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<JwtService>();

var jwtService = new JwtService(builder.Configuration);

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o => o.TokenValidationParameters = jwtService.ValidationParameters)
    .AddCookie("TempCookie", o =>
    {
        o.ExpireTimeSpan      = TimeSpan.FromMinutes(10);
        o.Cookie.SameSite     = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    })
    .AddGoogle(o =>
    {
        o.ClientId     = builder.Configuration["Authentication:Google:ClientId"]!;
        o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        o.SignInScheme  = "TempCookie";
        o.CorrelationCookie.SameSite     = SameSiteMode.Lax;
        o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.Scope.Add("email");
        o.Scope.Add("profile");
    });

builder.Services.AddAuthorization();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── Other services ────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddHttpClient("groq");
builder.Services.AddHttpClient("gemini");

var groqKey     = builder.Configuration["Groq:ApiKey"];
var geminiKey   = builder.Configuration["Gemini:ApiKey"];
var anthropicKey = builder.Configuration["Anthropic:ApiKey"];

if (!string.IsNullOrWhiteSpace(groqKey))
    builder.Services.AddScoped<ILabPdfAgentParser, GroqLabPdfParser>();
else if (!string.IsNullOrWhiteSpace(geminiKey))
    builder.Services.AddScoped<ILabPdfAgentParser, GeminiLabPdfParser>();
else if (!string.IsNullOrWhiteSpace(anthropicKey))
    builder.Services.AddScoped<ILabPdfAgentParser, AiLabPdfParser>();
else
    builder.Services.AddScoped<ILabPdfAgentParser, LabPdfAgentParser>();

if (!string.IsNullOrWhiteSpace(groqKey))
    builder.Services.AddScoped<ILabAiService, GroqLabAiService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MedicalData API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseSerilogRequestLogging();

app.MapGet("/health", () => Results.Ok("ok")).AllowAnonymous();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
