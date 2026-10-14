using Backend.Auth;
using Backend.Data;
using Backend.Outbox;
using Backend.TimeRecords;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SQLite — сессии и outbox
builder.Services.AddSingleton<SqliteContext>();

// MariaDB — основное хранилище
var mariaConnectionString = builder.Configuration.GetConnectionString("MariaDB")
    ?? "Server=dns-mariadb;Port=3306;Database=clock_db;User=clock_user;Password=clock_pass;";
builder.Services.AddDbContext<MariaDbContext>(options =>
    options.UseMySql(mariaConnectionString, ServerVersion.AutoDetect(mariaConnectionString)));

// Auth
builder.Services.AddSingleton<PasswordHasher>();

// Outbox
builder.Services.AddSingleton<OutboxRepository>();

// Background service — ДО builder.Build()
builder.Services.AddHostedService<OutboxSyncService>();

var app = builder.Build();

// Явная инициализация SQLite при старте
app.Services.GetRequiredService<SqliteContext>();

// Middleware
app.UseMiddleware<SessionMiddleware>();

// Эндпоинты
app.MapGet("/", () => "Backend is up and running");
app.MapGet("/api/health", () => new { status = "ok", timestamp = DateTime.UtcNow });

app.MapAuthEndpoints();
app.MapTimeRecordsEndpoints();
app.MapAdminEndpoints();

app.Run();