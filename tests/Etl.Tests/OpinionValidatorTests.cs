using Etl.Application;
using Etl.Domain;
using Xunit;

namespace Etl.Tests;

public class OpinionValidatorTests
{
    [Fact]
    public void Rechaza_texto_vacio()
    {
        var ok = OpinionValidator.TryNormalize(new Opinion { Source = "CSV", Text = "   " }, out _, out var reason);
        Assert.False(ok);
        Assert.NotNull(reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Rechaza_calificacion_fuera_de_rango(int rating)
    {
        var ok = OpinionValidator.TryNormalize(new Opinion { Source = "DB", Text = "hola", Rating = rating }, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void Acepta_y_normaliza_registro_valido()
    {
        var ok = OpinionValidator.TryNormalize(
            new Opinion { Source = "API", Text = "  buen servicio  ", Rating = 5, ExternalId = " 7 " },
            out var clean, out var reason);

        Assert.True(ok);
        Assert.Null(reason);
        Assert.Equal("buen servicio", clean.Text);
        Assert.Equal("7", clean.ExternalId);
    }

    [Fact]
    public void Acepta_calificacion_nula()
    {
        var ok = OpinionValidator.TryNormalize(new Opinion { Source = "API", Text = "ok" }, out _, out _);
        Assert.True(ok);
    }
}
