using System.Security.Cryptography;
using Kotirauha.Api.Common;
using Kotirauha.Core.Domain;
using Kotirauha.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Api.Endpoints;

public record CreateBuildingRequest(string Name, string? Address, string? SharedLanguage, string? ApartmentNumber);
public record JoinBuildingRequest(string JoinCode, string? ApartmentNumber);
public record BuildingDto(Guid Id, string Name, string? Address, string SharedLanguage, string Role, string? JoinCode);
public record MemberDto(Guid UserId, string DisplayName, string Role, string? ApartmentNumber);

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

            var code = req.JoinCode.Trim().ToUpperInvariant();
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

        group.MapPost("/join-code", async (HttpContext ctx, KotirauhaDbContext db) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var m = await db.GetMembershipAsync(userId.Value);
            if (m is null || m.Role is MembershipRole.Resident)
                return Results.Problem("Board access required.", statusCode: 403);

            m.Building!.JoinCode = await GenerateUniqueJoinCodeAsync(db);
            await db.SaveChangesAsync();
            return Results.Ok(new { joinCode = m.Building!.JoinCode });
        });

        return api;
    }

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
