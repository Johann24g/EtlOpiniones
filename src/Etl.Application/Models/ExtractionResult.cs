namespace Etl.Application.Models;

/// <summary>Resultado de la extracción de una fuente en un ciclo.</summary>
public sealed record ExtractionResult(
    string Source,
    bool Success,
    int Read,
    int Valid,
    int Invalid,
    int Written,
    TimeSpan Elapsed,
    string? Error = null);
