using Kotirauha.Core.Abstractions;
using Kotirauha.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kotirauha.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddKotirauhaInfrastructure(this IServiceCollection services)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Database=kotirauha;Username=kotirauha;Password=kotirauha";

        services.AddDbContext<KotirauhaDbContext>(options => options.UseNpgsql(connectionString));
        services.AddHttpClient();

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
        RegisterSuggestionProvider(services);
        RegisterEmailSender(services);

        services.AddScoped<EntryTranslationService>();

        return services;
    }

    private static void RegisterTranslationProvider(IServiceCollection services)
    {
        var explicitProvider = Environment.GetEnvironmentVariable("TRANSLATION_PROVIDER")?.ToLowerInvariant();
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        // Priority: explicit choice, then whichever key is present, then offline stub.
        var provider = explicitProvider
            ?? (!string.IsNullOrWhiteSpace(openAiKey) ? "openai"
                : !string.IsNullOrWhiteSpace(anthropicKey) ? "anthropic"
                : "stub");

        if (provider == "openai" && !string.IsNullOrWhiteSpace(openAiKey))
        {
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
            services.AddSingleton<ITranslationProvider>(sp =>
                new OpenAiTranslationProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), openAiKey!, model));
        }
        else if (provider == "anthropic" && !string.IsNullOrWhiteSpace(anthropicKey))
        {
            var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-6";
            services.AddSingleton<ITranslationProvider>(sp =>
                new AnthropicTranslationProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), anthropicKey!, model));
        }
        else
        {
            services.AddSingleton<ITranslationProvider, StubTranslationProvider>();
        }
    }

    private static void RegisterSuggestionProvider(IServiceCollection services)
    {
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
            services.AddSingleton<ISuggestionProvider>(sp =>
                new OpenAiSuggestionProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), openAiKey!, model));
        }
        else
        {
            services.AddSingleton<ISuggestionProvider, StubSuggestionProvider>();
        }
    }

    private static void RegisterEmailSender(IServiceCollection services)
    {
        var key = Environment.GetEnvironmentVariable("MAILJET_API_KEY");
        var secret = Environment.GetEnvironmentVariable("MAILJET_API_SECRET");
        var fromEmail = Environment.GetEnvironmentVariable("MAILJET_FROM_EMAIL");
        var fromName = Environment.GetEnvironmentVariable("MAILJET_FROM_NAME") ?? "Kotirauha";

        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(secret) && !string.IsNullOrWhiteSpace(fromEmail))
        {
            services.AddSingleton<IEmailSender>(sp => new MailjetEmailSender(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
                key!, secret!, fromEmail!, fromName,
                sp.GetRequiredService<ILogger<MailjetEmailSender>>()));
        }
        else
        {
            services.AddSingleton<IEmailSender, NoOpEmailSender>();
        }
    }
}
