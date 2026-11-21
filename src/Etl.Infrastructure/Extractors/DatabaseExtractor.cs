using System.Data;
using System.Runtime.CompilerServices;
using Etl.Application;
using Etl.Domain;
using Etl.Infrastructure.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Etl.Infrastructure.Extractors;

/// <summary>
/// Extrae reseñas de una BD relacional (SQL Server) con ADO.NET.
/// El query viene de configuración y debe ejecutarse con un usuario de solo lectura.
/// Columnas esperadas: ReviewId, Texto, Puntuacion, Fecha.
/// </summary>
public sealed class DatabaseExtractor : IExtractor
{
    private readonly DatabaseOptions _options;

    public DatabaseExtractor(IOptions<DatabaseOptions> options) => _options = options.Value;

    public string Name => SourceNames.Database;

    public async IAsyncEnumerable<Opinion> ExtractAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(_options.Query, conn)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

        // DataReader: lee fila a fila (streaming), memoria constante aunque haya millones de reseñas.
        await using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken);

        var iId = rd.GetOrdinal("ReviewId");
        var iText = rd.GetOrdinal("Texto");
        var iRating = rd.GetOrdinal("Puntuacion");
        var iDate = rd.GetOrdinal("Fecha");

        while (await rd.ReadAsync(cancellationToken))
        {
            var id = rd.IsDBNull(iId) ? null : Convert.ToString(rd.GetValue(iId));
            var text = rd.IsDBNull(iText) ? string.Empty : rd.GetString(iText);
            int? rating = rd.IsDBNull(iRating) ? null : Convert.ToInt32(rd.GetValue(iRating));
            DateTime? date = rd.IsDBNull(iDate) ? null : rd.GetDateTime(iDate);

            yield return new Opinion
            {
                Source = Name,
                ExternalId = id,
                Text = text,
                Rating = rating,
                Date = date
            };
        }
    }
}
