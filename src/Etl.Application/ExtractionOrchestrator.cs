using System.Diagnostics;
using System.Runtime.CompilerServices;
using Etl.Application.Models;
using Etl.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Etl.Application;

/// <summary>
/// Orquesta un ciclo de extracción: lanza todos los <see cref="IExtractor"/> en paralelo
/// (acotado por MaxDegreeOfParallelism), valida cada registro, lo envía a staging,
/// reintenta ante fallos y aísla los errores por fuente.
/// </summary>
public sealed class ExtractionOrchestrator
{
    private readonly IReadOnlyList<IExtractor> _extractors;
    private readonly IStagingWriter _writer;
    private readonly ExtractionOptions _options;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    public ExtractionOrchestrator(
        IEnumerable<IExtractor> extractors,
        IStagingWriter writer,
        IOptions<ExtractionOptions> options,
        ILogger<ExtractionOrchestrator> logger)
    {
        _extractors = extractors.ToList();
        _writer = writer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExtractionResult>> RunAsync(CancellationToken cancellationToken)
    {
        var batchId = Guid.NewGuid();
        var total = Stopwatch.StartNew();

        if (_extractors.Count == 0)
        {
            _logger.LogWarning("No hay fuentes habilitadas; no se extrae nada.");
            return Array.Empty<ExtractionResult>();
        }

        _logger.LogInformation("Ciclo ETL {BatchId} iniciado con {Count} fuente(s): {Sources}",
            batchId, _extractors.Count, string.Join(", ", _extractors.Select(e => e.Name)));

        using var gate = new SemaphoreSlim(Math.Max(1, _options.MaxDegreeOfParallelism));
        var results = await Task.WhenAll(
            _extractors.Select(e => RunSourceAsync(e, batchId, gate, cancellationToken)));

        foreach (var r in results)
        {
            if (r.Success)
                _logger.LogInformation(
                    "[{Source}] OK: leídos={Read} válidos={Valid} inválidos={Invalid} escritos={Written} en {Ms} ms",
                    r.Source, r.Read, r.Valid, r.Invalid, r.Written, (long)r.Elapsed.TotalMilliseconds);
            else
                _logger.LogError("[{Source}] FALLÓ en {Ms} ms: {Error}",
                    r.Source, (long)r.Elapsed.TotalMilliseconds, r.Error);
        }

        _logger.LogInformation("Ciclo ETL {BatchId} finalizado en {Ms} ms ({Ok}/{Total} fuentes OK)",
            batchId, total.ElapsedMilliseconds, results.Count(r => r.Success), results.Length);

        return results;
    }

    private async Task<ExtractionResult> RunSourceAsync(
        IExtractor extractor, Guid batchId, SemaphoreSlim gate, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        var sw = Stopwatch.StartNew();
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var stats = new RunStats();
                try
                {
                    _logger.LogInformation("[{Source}] Extracción iniciada (intento {Attempt})",
                        extractor.Name, attempt + 1);

                    var stream = ValidateAsync(extractor, stats, ct);
                    var written = await _writer.WriteAsync(batchId, extractor.Name, stream, ct);

                    if (stats.Invalid > 0)
                        _logger.LogWarning("[{Source}] {Invalid} registro(s) descartados por validación",
                            extractor.Name, stats.Invalid);

                    return new ExtractionResult(extractor.Name, true, stats.Read, stats.Valid,
                        stats.Invalid, written, sw.Elapsed);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < _options.MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(_options.RetryBaseDelaySeconds * Math.Pow(2, attempt));
                    _logger.LogWarning(ex, "[{Source}] Error en intento {Attempt}; reintentando en {Delay}s",
                        extractor.Name, attempt + 1, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }
                catch (Exception ex)
                {
                    // Un fallo en una fuente NO detiene a las demás.
                    _logger.LogError(ex, "[{Source}] Error definitivo tras {Attempts} intento(s)",
                        extractor.Name, attempt + 1);
                    return new ExtractionResult(extractor.Name, false, stats.Read, stats.Valid,
                        stats.Invalid, 0, sw.Elapsed, ex.Message);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async IAsyncEnumerable<Opinion> ValidateAsync(
        IExtractor extractor, RunStats stats, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in extractor.ExtractAsync(ct).WithCancellation(ct))
        {
            stats.Read++;
            if (!OpinionValidator.TryNormalize(item, out var clean, out var reason))
            {
                stats.Invalid++;
                _logger.LogDebug("[{Source}] Registro {Id} descartado: {Reason}",
                    extractor.Name, item.ExternalId, reason);
                continue;
            }

            stats.Valid++;
            yield return clean;
        }
    }

    private sealed class RunStats
    {
        public int Read;
        public int Valid;
        public int Invalid;
    }
}
