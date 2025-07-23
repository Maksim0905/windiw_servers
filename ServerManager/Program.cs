using Microsoft.EntityFrameworkCore;
using Serilog;
using ServerManager.Data;
using ServerManager.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Добавляем поддержку Windows Services
builder.Services.AddWindowsService();

// Настройка логирования
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/server-manager-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Настройка базы данных
builder.Services.AddDbContext<ServerContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=servers.db"));

// Регистрация сервисов
builder.Services.AddScoped<WindowsServerService>();
builder.Services.AddHostedService<BackgroundMonitoringService>();

// Настройка API контроллеров
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Оставляем исходные имена свойств
    });

// Swagger для документации API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Server Manager API", Version = "v1" });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// CORS для подключения клиентского приложения
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Создание и миграция базы данных
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ServerContext>();
    await context.Database.EnsureCreatedAsync();
}

// Настройка pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Server Manager API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");
app.UseRouting();
app.MapControllers();

// Добавляем простую страницу статуса
app.MapGet("/", () => new
{
    Service = "Server Manager API",
    Version = "1.0.0",
    Status = "Running",
    Timestamp = DateTime.UtcNow,
    Endpoints = new[]
    {
        "/api/servers - Управление серверами",
        "/api/servers/statistics - Статистика серверов",
        "/swagger - Документация API"
    }
});

app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });

Log.Information("Server Manager API starting on {Urls}", string.Join(", ", builder.WebHost.GetSetting("urls")?.Split(';') ?? new[] { "http://localhost:5000" }));

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}