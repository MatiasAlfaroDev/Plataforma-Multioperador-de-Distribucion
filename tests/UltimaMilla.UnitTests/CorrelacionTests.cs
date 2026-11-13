using UltimaMilla.Infrastructure.Observabilidad;

namespace UltimaMilla.UnitTests;

/// <summary>RNF 6.12: el identificador de correlación llega del cliente y termina en los logs.</summary>
public class CorrelacionTests
{
    [Fact]
    public void Un_identificador_valido_se_respeta()
    {
        using var _ = Correlacion.Iniciar("sync-123_abc.9");

        Assert.Equal("sync-123_abc.9", Correlacion.Actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("con espacios")]
    [InlineData("salto\nde-linea")]
    public void Uno_ausente_o_con_caracteres_raros_se_reemplaza_por_uno_nuevo(string? recibido)
    {
        using var _ = Correlacion.Iniciar(recibido);

        Assert.NotEqual(recibido, Correlacion.Actual);
        Assert.Matches("^[0-9a-f]{32}$", Correlacion.Actual!);
    }

    [Fact]
    public void Uno_demasiado_largo_se_reemplaza()
    {
        using var _ = Correlacion.Iniciar(new string('a', 65));

        Assert.Equal(32, Correlacion.Actual!.Length);
    }
}
