using MedicalData.Api.Data;
using MedicalData.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

// ── DataProtection (persist keys so restarts don't break OAuth cookies) ───────
var keysDir = Path.Combine(builder.Environment.ContentRootPath, ".dp-keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir));

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
        o.ExpireTimeSpan       = TimeSpan.FromMinutes(10);
        o.Cookie.SameSite      = SameSiteMode.Lax;
        o.Cookie.SecurePolicy  = CookieSecurePolicy.None;
    })
    .AddGoogle(o =>
    {
        o.ClientId     = builder.Configuration["Authentication:Google:ClientId"]!;
        o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        o.SignInScheme  = "TempCookie";
        o.CorrelationCookie.SameSite     = SameSiteMode.Lax;
        o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MedicalData API v1");
    c.RoutePrefix = string.Empty;
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
