using Backend.Auth;
using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.TimeRecords;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .AddEndpointFilter<RequireAuthFilter>();

        // GET /api/admin/time-records — список с username
        group.MapGet("/time-records", async (MariaDbContext db, HttpContext http) =>
        {
            var records = await db.TimeRecords
                .OrderByDescending(r => r.RecordedAt)
                .Take(200)
                .ToListAsync();

            // Собираем user_id → username
            var userIds = records
                .Where(r => r.CreatedBy != null)
                .Select(r => r.CreatedBy!.Value)
                .Distinct()
                .ToList();

            var users = await db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            var result = records.Select(r => new
            {
                id = r.Id.ToString(),
                recordedAt = r.RecordedAt,
                createdBy = r.CreatedBy?.ToString(),
                username = r.CreatedBy != null && users.TryGetValue(r.CreatedBy.Value, out var name)
                    ? name
                    : null,
                createdAt = r.CreatedAt
            });

            return Results.Ok(result);
        });

        // PUT /api/admin/time-records/{id} — редактирование
        group.MapPut("/time-records/{id}", async (
            string id,
            UpdateTimeRecordRequest request,
            MariaDbContext db) =>
        {
            if (!Guid.TryParse(id, out var guid))
            {
                return Results.BadRequest(new { error = "Invalid id" });
            }

            var record = await db.TimeRecords.FirstOrDefaultAsync(r => r.Id == guid);
            if (record == null)
            {
                return Results.NotFound(new { error = "Record not found" });
            }

            record.RecordedAt = request.RecordedAt;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                id = record.Id.ToString(),
                recordedAt = record.RecordedAt
            });
        });

        // DELETE /api/admin/time-records/{id} — удаление
        group.MapDelete("/time-records/{id}", async (string id, MariaDbContext db) =>
        {
            if (!Guid.TryParse(id, out var guid))
            {
                return Results.BadRequest(new { error = "Invalid id" });
            }

            var record = await db.TimeRecords.FirstOrDefaultAsync(r => r.Id == guid);
            if (record == null)
            {
                return Results.NotFound(new { error = "Record not found" });
            }

            db.TimeRecords.Remove(record);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Deleted" });
        });
    }
}

// === Фильтр авторизации ===
public class RequireAuthFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var userId = http.Items[SessionMiddleware.UserIdKey] as string;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}

// === DTO ===
public record UpdateTimeRecordRequest(DateTime RecordedAt);