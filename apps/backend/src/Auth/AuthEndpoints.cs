using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Auth;

public static class AuthEndpoints
{
    // Время жизни сессии — 7 дней
    private static readonly TimeSpan SessionTtl = TimeSpan.FromDays(7);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        // POST /api/auth/register
        group.MapPost("/register", async (
            RegisterRequest request,
            MariaDbContext db,
            PasswordHasher hasher,
            SqliteContext sqlite,
            HttpContext http) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            {
                return Results.BadRequest(new { error = "Username must be at least 3 characters" });
            }
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return Results.BadRequest(new { error = "Password must be at least 6 characters" });
            }

            var exists = await db.Users.AnyAsync(u => u.Username == request.Username);
            if (exists)
            {
                return Results.Conflict(new { error = "Username already taken" });
            }

            // Первый зарегистрированный пользователь становится админом
            var isFirstUser = !await db.Users.AnyAsync();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                PasswordHash = hasher.Hash(request.Password),
                Role = isFirstUser ? "admin" : "user",
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var sessionId = sqlite.CreateSession(user.Id.ToString(), user.Username, SessionTtl);

            http.Response.Cookies.Append(SessionMiddleware.CookieName, sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = false,
                Expires = DateTimeOffset.UtcNow.Add(SessionTtl)
            });

            return Results.Ok(new
            {
                id = user.Id.ToString(),
                username = user.Username,
                role = user.Role
            });
        });

        // POST /api/auth/login
        group.MapPost("/login", async (
            LoginRequest request,
            MariaDbContext db,
            PasswordHasher hasher,
            SqliteContext sqlite,
            HttpContext http) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null || !hasher.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var sessionId = sqlite.CreateSession(user.Id.ToString(), user.Username, SessionTtl);

            http.Response.Cookies.Append(SessionMiddleware.CookieName, sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = false,
                Expires = DateTimeOffset.UtcNow.Add(SessionTtl)
            });

            return Results.Ok(new
            {
                id = user.Id.ToString(),
                username = user.Username,
                role = user.Role
            });
        });

        // POST /api/auth/logout
        group.MapPost("/logout", (SqliteContext sqlite, HttpContext http) =>
        {
            var sessionId = http.Request.Cookies[SessionMiddleware.CookieName];
            if (!string.IsNullOrEmpty(sessionId))
            {
                sqlite.DeleteSession(sessionId);
            }

            http.Response.Cookies.Delete(SessionMiddleware.CookieName);
            return Results.Ok(new { message = "Logged out" });
        });

        // GET /api/auth/me
        group.MapGet("/me", async (HttpContext http, MariaDbContext db) =>
        {
            var userId = http.Items[SessionMiddleware.UserIdKey] as string;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var guid))
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == guid);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                id = user.Id.ToString(),
                username = user.Username,
                role = user.Role
            });
        });
    }
}

public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);