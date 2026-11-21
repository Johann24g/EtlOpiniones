using Etl.Application;
using Microsoft.Extensions.Options;

namespace Etl.Worker;

/// <summary>
/// BackgroundService que ejecuta el ciclo de extracción periódicamente.
/// Crea un scope de DI por ciclo para resolver extractores y HttpClient frescos.
/// </summary>
public sealed class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<ExtractionOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opt = options.Value;
        logger.LogInformation("Worker ETL iniciado. Intervalo={Interval} min, RunOnce={RunOnce}",
            opt.IntervalMinutes, opt.RunOnce);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<ExtractionOrchestrator>();
                    await orchestrator.RunAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Nunca dejar que un error tumbe el servicio: se registra y se espera al siguiente ciclo.
                    logger.LogError(ex, "Error inesperado en el ciclo ETL");
                }

                if (opt.RunOnce) break;

                logger.LogInformation("Próxima ejecución en {Interval} min", opt.IntervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(opt.IntervalMinutes), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal del host.
        }

        if (opt.RunOnce)
            lifetime.StopApplication();
    }
}
