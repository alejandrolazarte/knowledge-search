using System.Text.Json;

namespace KnowledgeSearch;

static class EventsEndpoints
{
    public static void MapEventsRoutes(this WebApplication app)
    {
        app.MapGet("/log", (ILogService log) =>
            Results.Ok(log.ReadLast(100)));

        app.MapGet("/events", async (ILogService log, HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.Headers.Append("Content-Type",      "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control",     "no-cache");
            ctx.Response.Headers.Append("X-Accel-Buffering", "no");

            var ch = log.Subscribe();
            try
            {
                await foreach (var ev in ch.Reader.ReadAllAsync(ct))
                {
                    var json = JsonSerializer.Serialize(ev, AppJsonContext.Default.LogEvent);
                    await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            finally
            {
                log.Unsubscribe(ch.Writer);
            }
        });
    }
}
