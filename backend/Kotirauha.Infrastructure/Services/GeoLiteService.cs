using System.Net;
using System.Net.Sockets;
using Kotirauha.Core.Abstractions;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Logging;

namespace Kotirauha.Infrastructure.Services;

// Offline country lookup backed by a MaxMind GeoLite2-Country database. If the
// .mmdb file is missing or unreadable, the service degrades to disabled and
// every lookup returns null, so analytics keep working without a country.
public sealed class GeoLiteService : IGeoIpService, IDisposable
{
    private readonly DatabaseReader? _reader;

    public bool Enabled => _reader is not null;

    public GeoLiteService(string? dbPath, ILogger<GeoLiteService> logger)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
        {
            logger.LogInformation("GeoLite2 database not found at '{Path}'; country analytics disabled.", dbPath);
            return;
        }
        try
        {
            _reader = new DatabaseReader(dbPath);
            logger.LogInformation("GeoLite2 database loaded from '{Path}'.", dbPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not open GeoLite2 database at '{Path}'; country analytics disabled.", dbPath);
        }
    }

    public string? CountryCode(string? ip)
    {
        if (_reader is null || string.IsNullOrWhiteSpace(ip)) return null;
        if (!IPAddress.TryParse(ip, out var addr)) return null;
        if (IPAddress.IsLoopback(addr) || IsPrivate(addr)) return null;

        try
        {
            return _reader.TryCountry(addr, out var resp) ? resp?.Country.IsoCode : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPrivate(IPAddress addr)
    {
        if (addr.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = addr.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 127);
    }

    public void Dispose() => _reader?.Dispose();
}
