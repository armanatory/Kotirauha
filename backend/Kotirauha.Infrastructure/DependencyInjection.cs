using Kotirauha.Core.Abstractions;
using Kotirauha.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kotirauha.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddKotirauhaInfrastructure(this IServiceCollection services)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Database=kotirauha;Username=kotirauha;Password=kotirauha";

        services.AddDbContext<KotirauhaDbContext>(options => options.UseNpgsql(connectionString));

        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddSingleton(new JwtSettings
        {
            Secret = Environment.GetEnvironmentVariable("JWT_SECRET")
                     ?? "dev-only-insecure-secret-change-me-32chars!!",
            Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "kotirauha",
            Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "kotirauha-users",
        });
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        var uploadRoot = Environment.GetEnvironmentVariable("UPLOAD_ROOT")
                         ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        services.AddSingleton<IAttachmentStore>(new LocalAttachmentStore(uploadRoot));

        RegisterTranslationProvider(services);

        services.AddScoped<EntryTranslationService>();

        return services;
    }

    private static void RegisterTranslationProvider(IServiceCollection services)
    {
        var provider = (Environment.GetEnvironmentVariable("TRANSLATION_PROVIDER") ?? "stub").ToLowerInvariant();
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (provider == "anthropic" && !string.IsNullOrWhiteSpace(apiKey))
        {
            var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-6";
            services.AddHttpClient();
            services.AddSingleton<ITranslationProvider>(sp =>
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                return new AnthropicTranslationProvider(http, apiKey!, model);
            });
        }
        else
        {
            services.AddSingleton<ITranslationProvider, StubTranslationProvider>();
        }
    }
}
