using Etl.Application;
using Etl.Infrastructure.Extractors;
using Etl.Infrastructure.Options;
using Etl.Infrastructure.Staging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Etl.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra las fuentes habilitadas en configuración. Agregar una fuente nueva =
    /// una clase IExtractor + una sección de configuración + una línea aquí.
    /// </summary>
    public static IServiceCollection AddEtlInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // ---- Opciones validadas al arrancar (fail-fast con mensajes claros) ----
        services.AddOptions<ExtractionOptions>()
            .Bind(config.GetSection(ExtractionOptions.SectionName))
            .Validate(o => o.IntervalMinutes > 0, "Extraction:IntervalMinutes debe ser > 0.")
            .ValidateOnStart();

        services.AddOptions<CsvOptions>()
            .Bind(config.GetSection(CsvOptions.SectionName))
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.FilePath),
                "Sources:Csv:FilePath es obligatorio cuando la fuente CSV está habilitada.")
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .Bind(config.GetSection(DatabaseOptions.SectionName))
            .Validate(o => !o.Enabled || (!string.IsNullOrWhiteSpace(o.ConnectionString)
                                          && !string.IsNullOrWhiteSpace(o.Query)),
                "Sources:Database:ConnectionString y Query son obligatorios cuando la fuente BD está habilitada. " +
                "Configure la cadena con User Secrets o la variable de entorno Sources__Database__ConnectionString.")
            .ValidateOnStart();

        services.AddOptions<ApiOptions>()
            .Bind(config.GetSection(ApiOptions.SectionName))
            .Validate(o => !o.Enabled || (Uri.IsWellFormedUriString(o.BaseUrl, UriKind.Absolute)
                                          && !string.IsNullOrWhiteSpace(o.Endpoint)),
                "Sources:Api:BaseUrl (URL absoluta) y Endpoint son obligatorios cuando la fuente API está habilitada.")
            .ValidateOnStart();

        services.AddOptions<StagingOptions>()
            .Bind(config.GetSection(StagingOptions.SectionName))
            .Validate(o => o.Mode is "Json" or "Sql", "Staging:Mode debe ser 'Json' o 'Sql'.")
            .Validate(o => o.Mode != "Sql" || !string.IsNullOrWhiteSpace(o.ConnectionString),
                "Staging:ConnectionString es obligatorio cuando Staging:Mode = Sql.")
            .ValidateOnStart();

        // ---- Fuentes (solo las habilitadas) ----
        var csv = config.GetSection(CsvOptions.SectionName).Get<CsvOptions>() ?? new CsvOptions();
        var db = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var api = config.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();

        if (csv.Enabled)
            services.AddTransient<IExtractor, CsvExtractor>();

        if (db.Enabled)
            services.AddTransient<IExtractor, DatabaseExtractor>();

        if (api.Enabled)
        {
            // HttpClient tipado vía IHttpClientFactory: reutiliza conexiones y respeta cambios de DNS.
            services.AddHttpClient<ApiExtractor>((sp, client) =>
            {
                var o = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
                client.BaseAddress = new Uri(o.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
                if (!string.IsNullOrWhiteSpace(o.ApiKey))
                    client.DefaultRequestHeaders.Add(o.ApiKeyHeader, o.ApiKey);
            });
            services.AddTransient<IExtractor>(sp => sp.GetRequiredService<ApiExtractor>());
        }

        // ---- Destino staging ----
        var staging = config.GetSection(StagingOptions.SectionName).Get<StagingOptions>() ?? new StagingOptions();
        if (staging.Mode == "Sql")
            services.AddSingleton<IStagingWriter, SqlStagingWriter>();
        else
            services.AddSingleton<IStagingWriter, JsonFileStagingWriter>();

        // Orquestador con ciclo de vida "scoped": el Worker crea un scope por ciclo.
        services.AddScoped<ExtractionOrchestrator>();

        return services;
    }
}
