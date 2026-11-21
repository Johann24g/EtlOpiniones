using Etl.Infrastructure.Extractors;
using Etl.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Etl.Tests;

public class CsvExtractorTests
{
    [Fact]
    public async Task Lee_filas_validas_y_omite_las_corruptas()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path,
                "id,comentario,calificacion,fecha\n" +
                "1,\"Muy bueno\",5,2026-10-01\n" +
                "2,\"Fila mala\",abc,2026-10-02\n" +
                "3,\"Regular\",3,\n");

            var sut = new CsvExtractor(
                MsOptions.Create(new CsvOptions { FilePath = path }),
                NullLogger<CsvExtractor>.Instance);

            var items = new List<Etl.Domain.Opinion>();
            await foreach (var o in sut.ExtractAsync(CancellationToken.None))
                items.Add(o);

            Assert.Equal(2, items.Count);
            Assert.Equal("Muy bueno", items[0].Text);
            Assert.Null(items[1].Date);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Lanza_si_el_archivo_no_existe()
    {
        var sut = new CsvExtractor(
            MsOptions.Create(new CsvOptions { FilePath = "no-existe.csv" }),
            NullLogger<CsvExtractor>.Instance);

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await foreach (var _ in sut.ExtractAsync(CancellationToken.None)) { }
        });
    }
}
