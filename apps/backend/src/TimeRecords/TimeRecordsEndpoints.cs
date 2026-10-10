using Backend.Auth;
using Backend.Outbox;

namespace Backend.TimeRecords;

public static class TimeRecordsEndpoints
{
    public static void MapTimeRecordsEndpoints(this WebApplication app)
    {
        // POST /api/time-records — публичный, но если залогинен — пишет created_by
        app.MapPost("/api/time-records", async (
            OutboxRepository outbox,
            HttpContext http) =>
        {
            // Если пользователь залогинен — берём его id из HttpContext.Items
            var userId = http.Items[SessionMiddleware.UserIdKey] as string;

            var recordedAt = DateTime.UtcNow;
            var id = await outbox.AddTimeRecordAsync(recordedAt, userId);

            return Results.Accepted($"/api/time-records/{id}", new
            {
                id,
                recordedAt,
                createdBy = userId,
                status = "pending"
            });
        });
    }
}