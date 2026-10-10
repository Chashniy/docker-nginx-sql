using Backend.Data;

namespace Backend.Auth;

public class SessionMiddleware
{
    public const string CookieName = "session_id";
    public const string UserIdKey = "user_id";
    public const string UsernameKey = "username";

    private readonly RequestDelegate _next;

    public SessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SqliteContext sqlite)
    {
        var sessionId = context.Request.Cookies[CookieName];

        if (!string.IsNullOrEmpty(sessionId))
        {
            var session = sqlite.GetSession(sessionId);
            if (session != null)
            {
                context.Items[UserIdKey] = session.UserId;
                context.Items[UsernameKey] = session.Username;
            }
        }

        await _next(context);
    }
}