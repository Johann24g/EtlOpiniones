using Etl.Domain;

namespace Etl.Application;

/// <summary>Persiste lo extraído en archivos temporales o tablas staging.</summary>
public interface IStagingWriter
{
    /// <summary>
    /// Escribe el flujo de opiniones de una fuente de forma atómica
    /// (todo o nada) y devuelve la cantidad de registros escritos.
    /// </summary>
    Task<int> WriteAsync(Guid batchId, string sourceName, IAsyncEnumerable<Opinion> data,
        CancellationToken cancellationToken);
}
