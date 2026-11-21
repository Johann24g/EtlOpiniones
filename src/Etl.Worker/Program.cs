using Etl.Infrastructure;
using Etl.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Logging estructurado con Serilog; sinks y niveles se configuran en appsettings.json.
// El Worker sigue usando la abstracción ILogger<T>.
builder.Services.AddSerilog((services, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Fuentes, staging y orquestador (todo guiado por configuración).
builder.Services.AddEtlInfrastructure(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
