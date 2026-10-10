using Backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Регистрируем SqliteContext как синглтон
builder.Services.AddSingleton<SqliteContext>();

var app = builder.Build();

// Явно инициализируем SQLite при старте (создаёт файл и таблицы)
app.Services.GetRequiredService<SqliteContext>();

app.MapGet("/", () => "Backend is up and running");

app.MapGet("/api/health", () => new
{
    status = "ok",
    timestamp = DateTime.UtcNow
});

app.Run();