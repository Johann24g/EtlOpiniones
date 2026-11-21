namespace Etl.Application;

/// <summary>Configuración del ciclo de extracción (sección "Extraction" del appsettings).</summary>
public sealed class ExtractionOptions
{
    public const string SectionName = "Extraction";

    /// <summary>Minutos entre ejecuciones del ciclo ETL.</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Si es true ejecuta un solo ciclo y detiene el servicio (útil para demos y pruebas).</summary>
    public bool RunOnce { get; set; }

    /// <summary>Número máximo de fuentes extrayéndose en paralelo.</summary>
    public int MaxDegreeOfParallelism { get; set; } = 3;

    /// <summary>Reintentos por fuente ante errores transitorios.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Base (segundos) del backoff exponencial entre reintentos.</summary>
    public int RetryBaseDelaySeconds { get; set; } = 2;
}
