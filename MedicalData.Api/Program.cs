using MedicalData.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
{
// Enable environment variable support
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();

    var dbUser = Environment.GetEnvironmentVariable("DB_USER");
    var dbPass = Environment.GetEnvironmentVariable("DB_PASS");
    var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
    var dbName = Environment.GetEnvironmentVariable("DB_NAME");

    var connectionString = $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPass};";

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else 
    builder.Services
        .AddDbContext<AppDbContext>(options => 
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()  
            .AllowAnyMethod()  
            .AllowAnyHeader(); 
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply pending migrations at startup (without dropping the database)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();  // Applies pending migrations to the database
}

app.UseSwagger();
app.UseSwaggerUI();


app.UseCors("AllowAll");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
