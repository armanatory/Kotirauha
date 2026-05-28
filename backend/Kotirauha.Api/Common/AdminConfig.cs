using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Common;

// Which emails are platform admins. Configurable via ADMIN_EMAIL (comma-separated),
// with the project owner always included so the admin panel works out of the box.
public static class AdminConfig
{
    private const string DefaultAdmin = "armanatory@gmail.com";

    public static IReadOnlySet<string> Emails { get; } = Build();

    private static IReadOnlySet<string> Build()
    {
        var set = (Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();
        set.Add(DefaultAdmin);
        return set;
    }

    public static bool IsAdminEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && Emails.Contains(email.Trim().ToLowerInvariant());
}

public static class AdminGuard
{
    public static async Task<bool> IsAdminAsync(HttpContext ctx, KotirauhaDbContext db)
    {
        var userId = ctx.User.GetUserId();
        if (userId is null) return false;
        return await db.Users.AnyAsync(u => u.Id == userId && u.IsPlatformAdmin);
    }
}
