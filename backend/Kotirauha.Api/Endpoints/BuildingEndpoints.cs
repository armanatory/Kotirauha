using System.Security.Cryptography;
using Kotirauha.Api.Common;
using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Kotirauha.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record CreateBuildingRequest(string Name, string? Address, string? SharedLanguage, string? ApartmentNumber);
public record JoinBuildingRequest(string JoinCode, string? ApartmentNumber);
public record SetJoinCodeRequest(string? Code);
public record BuildingDto(Guid Id, string Name, string? Address, string SharedLanguage, string Role, string? JoinCode);
public record MemberDto(Guid UserId, string DisplayName, string Role, string? ApartmentNumber);
public record BrowseBuildingDto(Guid Id, string Name, string? Address, bool Requested);
public record CreateJoinRequest(string? ApartmentNumber);
public record MyJoinRequestDto(Guid BuildingId, string BuildingName);
public record JoinRequestDto(Guid Id, string RequesterName, string RequesterEmail, string? ApartmentNumber, DateTimeOffset CreatedAt);

public static class BuildingEndpoints
{
    public static RouteGroupBuilder MapBuildingEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/buildings").RequireAuthorization();

        group.MapPost("/", async (CreateBuildingRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            if (await db.Memberships.AnyAsync(m => m.UserId == userId))
                return Results.Problem("You already belong to a building.", statusCode: 409);
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.Problem("Building name is required.", statusCode: 400);

            var building = new Building
            {
                Name = req.Name.Trim(),
                Address = req.Address?.Trim(),
                SharedLanguage = string.IsNullOrWhiteSpace(req.SharedLanguage) ? "fi" : req.SharedLanguage!,
                JoinCode = await GenerateUniqueJoinCodeAsync(db),
            };
            db.Buildings.Add(building);
            db.Memberships.Add(new BuildingMembership
            {
                UserId = userId.Value,
                BuildingId = building.Id,
                Role = MembershipRole.Board,
                ApartmentNumber = req.ApartmentNumber?.Trim(),
            });
            await db.SaveChangesAsync();

            return Results.Ok(new BuildingDto(
                building.Id, building.Name, building.Address, building.SharedLanguage, "board", building.JoinCode));
        });

        group.MapPost("/join", async (JoinBuildingRequest req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            if (await db.Memberships.AnyAsync(m => m.UserId == userId))
                return Results.Problem("You already belong to a building.", statusCode: 409);

            var code = NormalizeCode(req.JoinCode);
            var building = await db.Buildings.FirstOrDefaultAsync(b => b.JoinCode == code);
            if (building is null) return Results.Problem("Invalid join code.", statusCode: 404);

            db.Memberships.Add(new BuildingMembership
            {
                UserId = userId.Value,
                BuildingId = building.Id,
                Role = MembershipRole.Resident,
                ApartmentNumber = req.ApartmentNumber?.Trim(),
            });
            await db.SaveChangesAsync();

            return Results.Ok(new BuildingDto(
                building.Id, building.Name, building.Address, building.SharedLanguage, "resident", null));
        });

        group.MapGet("/me", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null) return Results.NoContent();

            var isBoard = m.Role is MembershipRole.Board or MembershipRole.Admin;
            return Results.Ok(new BuildingDto(
                m.BuildingId, m.Building!.Name, m.Building!.Address, m.Building!.SharedLanguage,
                m.Role.ToString().ToLowerInvariant(), isBoard ? m.Building!.JoinCode : null));
        });

        group.MapGet("/members", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null || m.Role is MembershipRole.Resident)
                return Results.Problem("Board access required.", statusCode: 403);

            var members = await db.Memberships
                .Where(x => x.BuildingId == m.BuildingId)
                .Include(x => x.User)
                .Select(x => new MemberDto(
                    x.UserId, x.User!.DisplayName, x.Role.ToString().ToLowerInvariant(), x.ApartmentNumber))
                .ToListAsync();

            return Results.Ok(members);
        });

        // Set a custom join code (if `code` provided) or regenerate a random one.
        group.MapPost("/join-code", async (SetJoinCodeRequest? req, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null || m.Role is MembershipRole.Resident)
                return Results.Problem("Board access required.", statusCode: 403);

            if (!string.IsNullOrWhiteSpace(req?.Code))
            {
                var code = NormalizeCode(req.Code);
                if (code.Length is < 4 or > 32 || !code.All(c => char.IsLetterOrDigit(c) || c == '-'))
                    return Results.Problem("Code must be 4 to 32 letters, numbers or dashes.", statusCode: 400);
                if (await db.Buildings.AnyAsync(b => b.JoinCode == code && b.Id != m.BuildingId))
                    return Results.Problem("That code is already taken. Try another.", statusCode: 409);
                m.Building!.JoinCode = code;
            }
            else
            {
                m.Building!.JoinCode = await GenerateUniqueJoinCodeAsync(db);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { joinCode = m.Building!.JoinCode });
        });

        // ── Browse buildings to request to join ──
        group.MapGet("/browse", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var requestedIds = await db.JoinRequests
                .Where(r => r.UserId == userId && r.Status == JoinRequestStatus.Pending)
                .Select(r => r.BuildingId)
                .ToListAsync();

            var buildings = await db.Buildings
                .OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Name, b.Address })
                .ToListAsync();

            var rows = buildings.Select(b => new BrowseBuildingDto(b.Id, b.Name, b.Address, requestedIds.Contains(b.Id)));
            return Results.Ok(rows);
        });

        // ── Send a join request (notifies board members by email) ──
        group.MapPost("/{id:guid}/join-request", async (
            Guid id, CreateJoinRequest req, HttpContext ctx, KotirauhaDbContext db, IEmailSender email) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            if (await db.Memberships.AnyAsync(mm => mm.UserId == userId))
                return Results.Problem("You already belong to a building.", statusCode: 409);

            var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id);
            if (building is null) return Results.NotFound();

            var existing = await db.JoinRequests
                .FirstOrDefaultAsync(r => r.UserId == userId && r.BuildingId == id && r.Status == JoinRequestStatus.Pending);
            if (existing is null)
            {
                db.JoinRequests.Add(new BuildingJoinRequest
                {
                    BuildingId = id,
                    UserId = userId.Value,
                    ApartmentNumber = string.IsNullOrWhiteSpace(req.ApartmentNumber) ? null : req.ApartmentNumber.Trim(),
                });
                await db.SaveChangesAsync();
            }

            // Notify every board/admin member of the building.
            var requester = await db.Users.FirstAsync(u => u.Id == userId);
            var requesterLabel = string.IsNullOrWhiteSpace(requester.DisplayName) ? requester.Email : requester.DisplayName;
            var boardMembers = await db.Memberships
                .Where(mm => mm.BuildingId == id && mm.Role != MembershipRole.Resident)
                .Include(mm => mm.User)
                .Select(mm => mm.User!)
                .ToListAsync();
            var appBase = (Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5173").TrimEnd('/');
            var link = $"{appBase}/building";
            foreach (var bm in boardMembers)
            {
                var (subject, html, text) = EmailTemplates.RenderJoinRequest(building.Name, requesterLabel, link, bm.PreferredLanguage);
                try { await email.SendAsync(bm.Email, subject, html, text); } catch { /* never fail the request on email */ }
            }

            return Results.Ok(new { requested = true });
        });

        // ── The caller's own pending request (for the waiting screen) ──
        group.MapGet("/my-join-request", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var r = await db.JoinRequests
                .Where(x => x.UserId == userId && x.Status == JoinRequestStatus.Pending)
                .Include(x => x.Building)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
            if (r is null) return Results.NoContent();
            return Results.Ok(new MyJoinRequestDto(r.BuildingId, r.Building!.Name));
        });

        group.MapDelete("/my-join-request", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var reqs = await db.JoinRequests
                .Where(x => x.UserId == userId && x.Status == JoinRequestStatus.Pending)
                .ToListAsync();
            db.JoinRequests.RemoveRange(reqs);
            await db.SaveChangesAsync();
            return Results.Ok(new { cancelled = true });
        });

        // ── Board: list + approve/reject pending requests ──
        group.MapGet("/join-requests", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null || m.Role is MembershipRole.Resident)
                return Results.Problem("Board access required.", statusCode: 403);

            var rows = await db.JoinRequests
                .Where(r => r.BuildingId == m.BuildingId && r.Status == JoinRequestStatus.Pending)
                .Include(r => r.User)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new JoinRequestDto(r.Id, r.User!.DisplayName, r.User!.Email, r.ApartmentNumber, r.CreatedAt))
                .ToListAsync();
            return Results.Ok(rows);
        });

        group.MapPost("/join-requests/{requestId:guid}/approve", async (Guid requestId, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null || m.Role is MembershipRole.Resident)
                return Results.Problem("Board access required.", statusCode: 403);

            var r = await db.JoinRequests.FirstOrDefaultAsync(x => x.Id == requestId && x.BuildingId == m.BuildingId);
            if (r is null) return Results.NotFound();
            if (r.Status != JoinRequestStatus.Pending) return Results.Ok(new { ok = true });

            if (!await db.Memberships.AnyAsync(mm => mm.UserId == r.UserId && mm.BuildingId == r.BuildingId))
            {
                db.Memberships.Add(new BuildingMembership
                {
                    UserId = r.UserId,
                    BuildingId = r.BuildingId,
                    Role = MembershipRole.Resident,
                    ApartmentNumber = r.ApartmentNumber,
                });
            }
            r.Status = JoinRequestStatus.Approved;
            r.DecidedAt = DateTimeOffset.UtcNow;
            r.DecidedByUserId = userId;
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        group.MapPost("/join-requests/{requestId:guid}/reject", async (Guid requestId, HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null || m.Role is MembershipRole.Resident)
                return Results.Problem("Board access required.", statusCode: 403);

            var r = await db.JoinRequests.FirstOrDefaultAsync(x => x.Id == requestId && x.BuildingId == m.BuildingId);
            if (r is null) return Results.NotFound();
            r.Status = JoinRequestStatus.Rejected;
            r.DecidedAt = DateTimeOffset.UtcNow;
            r.DecidedByUserId = userId;
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        return api;
    }

    // Codes are matched case- and whitespace-insensitively so they are easy to
    // share and type. Stored normalized (uppercase, no spaces).
    private static string NormalizeCode(string raw) =>
        new string(raw.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToUpperInvariant();

    private static async Task<string> GenerateUniqueJoinCodeAsync(KotirauhaDbContext db)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[7];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            var code = new string(chars);
            if (!await db.Buildings.AnyAsync(b => b.JoinCode == code)) return code;
        }
        return Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();
    }
}
