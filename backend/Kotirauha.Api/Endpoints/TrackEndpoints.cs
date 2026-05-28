using System.Security.Cryptography;
using System.Text;
using Kotirauha.Api.Common;
using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;

namespace Kotirauha.Api.Endpoints;

public record TrackRequest(string Path, string? Referrer, string? Language);

public static class TrackEndpoints
{
    public static RouteGroupBuilder MapTrackEndpoints(this RouteGroupBuilder api)
    {
        // Anonymous, fire-and-forget product analytics. Stores a coarse country
        // and a salted visitor hash, never a raw IP.
        api.MapPost("/track", async (TrackRequest req, HttpContext ctx, KotirauhaDbContext db, IGeoIpService geo) =>
        {
            var path = Trim(req.Path, 512);
            if (string.IsNullOrWhiteSpace(path)) return Results.Ok();

            var ip = ClientIp(ctx);
            db.VisitEvents.Add(new VisitEvent
            {
                Path = path,
                Referrer = Trim(req.Referrer, 512),
                Language = Trim(req.Language, 16),
                Country = geo.CountryCode(ip),
                VisitorHash = VisitorHash(ip, ctx.Request.Headers.UserAgent.ToString()),
                UserId = ctx.User.GetUserId(),
            });
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireRateLimiting("track");

        return api;
    }

    private static string? Trim(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length <= max ? s : s[..max];
    }

    private static string? ClientIp(HttpContext ctx)
    {
        var fwd = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(fwd))
            return fwd.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return ctx.Connection.RemoteIpAddress?.ToString();
    }

    private static readonly string Salt =
        Environment.GetEnvironmentVariable("JWT_SECRET") ?? "kotirauha-analytics-salt";

    private static string? VisitorHash(string? ip, string userAgent)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{Salt}|{ip}|{userAgent}"));
        return Convert.ToHexString(bytes)[..32];
    }
}
