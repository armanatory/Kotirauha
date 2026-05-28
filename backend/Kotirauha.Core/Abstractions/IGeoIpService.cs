namespace Kotirauha.Core.Abstractions;

// Resolves an IP address to an ISO 3166-1 alpha-2 country code, fully offline.
// Enabled is false when no GeoLite2 database file is present, in which case
// CountryCode always returns null (analytics still work, just without country).
public interface IGeoIpService
{
    bool Enabled { get; }
    string? CountryCode(string? ip);
}
