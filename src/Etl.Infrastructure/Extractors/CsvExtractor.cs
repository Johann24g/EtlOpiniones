using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Etl.Application;
using Etl.Domain;
using Etl.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Etl.Infrastructure.Extractors;

/// <summary>Lee encuestas internas desde un archivo CSV en streaming con CsvHelper.</summary>
public sealed class CsvExtractor : IExtractor
{
    private readonly CsvOptions _options;
    private readonly ILogger<CsvExtractor> _logger;

    public CsvExtractor(IOptions<CsvOptions> options, ILogger<CsvExtractor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string Name => SourceNames.Csv;

    public async IAsyncEnumerable<Opinion> ExtractAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.FilePath))
            throw new FileNotFoundException("Archivo CSV de encuestas no encontrado.", _options.FilePath);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = _options.Delimiter,
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = ctx =>
                _logger.LogWarning("[CSV] Dato mal formado en fila {Row}", ctx.Context?.Parser?.Row),
            // Una fila corrupta se registra y se omite; no detiene la lectura del archivo.
            ReadingExceptionOccurred = args =>
            {
                _logger.LogWarning("[CSV] Fila {Row} omitida: {Message}",
                    args.Exception.Context?.Parser?.Row, args.Exception.Message);
                return false;
            }
        };

        using var reader = new StreamReader(_options.FilePath);
        using var csv = new CsvReader(reader, config);

        await foreach (var row in csv.GetRecordsAsync<CsvOpinionRow>(cancellationToken))
        {
            yield return new Opinion
            {
                Source = Name,
                ExternalId = row.Id,
                Text = row.Comentario ?? string.Empty,
                Rating = row.Calificacion,
                Date = row.Fecha
            };
        }
    }

    /// <summary>Columnas esperadas: id, comentario, calificacion, fecha.</summary>
    public sealed class CsvOpinionRow
    {
        [Name("id")] public string? Id { get; set; }
        [Name("comentario")] public string? Comentario { get; set; }
        [Name("calificacion")] public int? Calificacion { get; set; }
        [Name("fecha")] public DateTime? Fecha { get; set; }
    }
}
