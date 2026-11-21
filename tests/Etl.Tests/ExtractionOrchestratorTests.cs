using System.Runtime.CompilerServices;
using Etl.Application;
using Etl.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Etl.Tests;

public class ExtractionOrchestratorTests
{
    private static ExtractionOrchestrator Create(IEnumerable<IExtractor> extractors, InMemoryWriter writer, int retries = 0)
        => new(extractors, writer,
            Options.Create(new ExtractionOptions { MaxRetries = retries, RetryBaseDelaySeconds = 0, MaxDegreeOfParallelism = 3 }),
            NullLogger<ExtractionOrchestrator>.Instance);

    private static Opinion Op(string source, string text, int? rating = 4)
        => new() { Source = source, Text = text, Rating = rating };

    [Fact]
    public async Task Extrae_todas_las_fuentes_y_filtra_invalidos()
    {
        var writer = new InMemoryWriter();
        var sut = Create(new IExtractor[]
        {
            new FakeExtractor("CSV", new[] { Op("CSV", "a"), Op("CSV", ""), Op("CSV", "c", 9) }),
            new FakeExtractor("API", new[] { Op("API", "x"), Op("API", "y") })
        }, writer);

        var results = await sut.RunAsync(CancellationToken.None);

        Assert.All(results, r => Assert.True(r.Success));
        var csv = results.Single(r => r.Source == "CSV");
        Assert.Equal(3, csv.Read);
        Assert.Equal(1, csv.Valid);
        Assert.Equal(2, csv.Invalid);
        Assert.Equal(1, writer.Written["CSV"].Count);
        Assert.Equal(2, writer.Written["API"].Count);
    }

    [Fact]
    public async Task Un_fallo_en_una_fuente_no_detiene_las_demas()
    {
        var writer = new InMemoryWriter();
        var sut = Create(new IExtractor[]
        {
            new FakeExtractor("DB", Array.Empty<Opinion>(), new InvalidOperationException("BD caída")),
            new FakeExtractor("API", new[] { Op("API", "ok") })
        }, writer);

        var results = await sut.RunAsync(CancellationToken.None);

        Assert.False(results.Single(r => r.Source == "DB").Success);
        Assert.True(results.Single(r => r.Source == "API").Success);
        Assert.Single(writer.Written["API"]);
    }

    [Fact]
    public async Task Reintenta_ante_fallos_transitorios()
    {
        var writer = new InMemoryWriter();
        var flaky = new FlakyExtractor("DB", failuresBeforeSuccess: 2, Op("DB", "ok"));
        var sut = Create(new IExtractor[] { flaky }, writer, retries: 2);

        var results = await sut.RunAsync(CancellationToken.None);

        Assert.True(results.Single().Success);
        Assert.Equal(3, flaky.Attempts);
    }

    [Fact]
    public async Task Sin_fuentes_devuelve_lista_vacia()
    {
        var sut = Create(Array.Empty<IExtractor>(), new InMemoryWriter());
        var results = await sut.RunAsync(CancellationToken.None);
        Assert.Empty(results);
    }

    // ---------- dobles de prueba ----------

    private sealed class FakeExtractor(string name, IEnumerable<Opinion> items, Exception? fail = null) : IExtractor
    {
        public string Name => name;

        public async IAsyncEnumerable<Opinion> ExtractAsync([EnumeratorCancellation] CancellationToken ct)
        {
            if (fail is not null) throw fail;
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }
    }

    private sealed class FlakyExtractor(string name, int failuresBeforeSuccess, Opinion item) : IExtractor
    {
        public int Attempts { get; private set; }
        public string Name => name;

        public async IAsyncEnumerable<Opinion> ExtractAsync([EnumeratorCancellation] CancellationToken ct)
        {
            Attempts++;
            if (Attempts <= failuresBeforeSuccess) throw new IOException("fallo transitorio");
            await Task.Yield();
            yield return item;
        }
    }

    private sealed class InMemoryWriter : IStagingWriter
    {
        public Dictionary<string, List<Opinion>> Written { get; } = new();
        private readonly object _lock = new();

        public async Task<int> WriteAsync(Guid batchId, string sourceName, IAsyncEnumerable<Opinion> data, CancellationToken ct)
        {
            var list = new List<Opinion>();
            await foreach (var o in data.WithCancellation(ct)) list.Add(o);
            lock (_lock) Written[sourceName] = list;
            return list.Count;
        }
    }
}
