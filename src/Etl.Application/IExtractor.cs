using Etl.Domain;

namespace Etl.Application;

/// <summary>
/// Contrato que debe cumplir toda fuente de datos. Para agregar una fuente nueva
/// basta con implementar esta interfaz y registrarla en la inyección de dependencias
/// (principio Open/Closed).
/// </summary>
public interface IExtractor
{
    /// <summary>Nombre de la fuente (CSV, DB, API...).</summary>
    string Name { get; }

    /// <summary>
    /// Extrae los registros en streaming (sin cargar todo en memoria),
    /// lo que permite procesar grandes volúmenes.
    /// </summary>
    IAsyncEnumerable<Opinion> ExtractAsync(CancellationToken cancellationToken);
}
