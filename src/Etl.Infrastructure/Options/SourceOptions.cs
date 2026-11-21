namespace Etl.Infrastructure.Options;

/// <summary>Fuente CSV (sección Sources:Csv).</summary>
public sealed class CsvOptions
{
    public const string SectionName = "Sources:Csv";

    public bool Enabled { get; set; } = true;
    public string FilePath { get; set; } = string.Empty;
    public string Delimiter { get; set; } = ",";
}

/// <summary>Fuente BD relacional (sección Sources:Database). La cadena de conexión viene de User Secrets / variables de entorno.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Sources:Database";

    public bool Enabled { get; set; } = true;
    public string ConnectionString { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 60;
}

/// <summary>Fuente API REST (sección Sources:Api). La API key viene de User Secrets / variables de entorno.</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Sources:Api";

    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeader { get; set; } = "X-Api-Key";
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>Destino de la extracción (sección Staging).</summary>
public sealed class StagingOptions
{
    public const string SectionName = "Staging";

    /// <summary>"Json" (archivos temporales) o "Sql" (tablas staging en la BD analítica).</summary>
    public string Mode { get; set; } = "Json";
    public string Directory { get; set; } = "staging";
    public string ConnectionString { get; set; } = string.Empty;
    public string TableName { get; set; } = "staging.Opinion";
    public int BatchSize { get; set; } = 1000;
}
