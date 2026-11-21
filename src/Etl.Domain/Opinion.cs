namespace Etl.Domain;

/// <summary>
/// Modelo común al que se normalizan las opiniones de todas las fuentes
/// (encuestas CSV, reseñas de BD relacional y comentarios de API REST).
/// </summary>
public sealed record Opinion
{
    /// <summary>Fuente de origen: CSV, DB o API.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Identificador en el sistema de origen (opcional).</summary>
    public string? ExternalId { get; init; }

    /// <summary>Texto del comentario / reseña.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Calificación de 1 a 5 (si la fuente la provee).</summary>
    public int? Rating { get; init; }

    /// <summary>Fecha de la opinión en el origen (si existe).</summary>
    public DateTime? Date { get; init; }

    /// <summary>Momento (UTC) en que el ETL extrajo el registro.</summary>
    public DateTime ExtractedAt { get; init; } = DateTime.UtcNow;
}
