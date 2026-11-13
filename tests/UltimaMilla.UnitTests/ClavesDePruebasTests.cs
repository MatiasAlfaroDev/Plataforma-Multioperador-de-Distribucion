using UltimaMilla.Api.Seguridad;
using Xunit;

namespace UltimaMilla.UnitTests;

public class ClavesDePruebasTests
{
    private const string HashReal = "5a7dc4e3d6aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly Guid Vinculo = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Generar_arma_prefijo_vinculo_y_firma_de_largo_fijo()
    {
        var clave = ClavesDePruebas.Generar(Vinculo, HashReal);

        // "ulm_test_" (9) + id en formato N (32) + "_" (1) + firma SHA-256 en hexadecimal (64)
        Assert.Equal(106, clave.Length);
        Assert.StartsWith("ulm_test_" + Vinculo.ToString("N") + "_", clave);
        Assert.Matches("^[0-9a-f]{64}$", clave[^64..]);
    }

    [Fact]
    public void Generar_es_determinista()
    {
        Assert.Equal(
            ClavesDePruebas.Generar(Vinculo, HashReal),
            ClavesDePruebas.Generar(Vinculo, HashReal));
    }

    [Fact]
    public void Una_clave_real_nueva_invalida_la_de_pruebas_anterior()
    {
        var anterior = ClavesDePruebas.Generar(Vinculo, HashReal);
        var nueva = ClavesDePruebas.Generar(Vinculo, "otro-hash-de-clave-real");

        Assert.NotEqual(anterior, nueva);

        Assert.True(ClavesDePruebas.TryLeer(anterior, out var id, out var firma));
        Assert.False(ClavesDePruebas.EsValida(id, firma, "otro-hash-de-clave-real"));
    }

    [Fact]
    public void Vinculos_distintos_dan_claves_distintas()
    {
        var otro = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.NotEqual(
            ClavesDePruebas.Generar(Vinculo, HashReal),
            ClavesDePruebas.Generar(otro, HashReal));
    }

    [Fact]
    public void La_clave_no_contiene_el_hash_de_la_clave_real()
    {
        Assert.DoesNotContain(HashReal, ClavesDePruebas.Generar(Vinculo, HashReal));
    }

    [Theory]
    [InlineData("ulm_test_algo", true)]
    [InlineData("ulm_test_", true)]
    [InlineData("5a7dc4e3d6", false)]
    [InlineData("ULM_TEST_algo", false)]
    [InlineData("", false)]
    public void EsClaveDePruebas_distingue_por_prefijo(string clave, bool esperado)
    {
        Assert.Equal(esperado, ClavesDePruebas.EsClaveDePruebas(clave));
    }

    [Fact]
    public void TryLeer_recupera_el_vinculo_y_la_firma()
    {
        var clave = ClavesDePruebas.Generar(Vinculo, HashReal);

        Assert.True(ClavesDePruebas.TryLeer(clave, out var id, out var firma));
        Assert.Equal(Vinculo, id);
        Assert.Equal(clave[^64..], firma);
    }

    [Fact]
    public void TryLeer_rechaza_una_clave_real()
    {
        Assert.False(ClavesDePruebas.TryLeer(new string('a', 64), out var id, out var firma));
        Assert.Equal(Guid.Empty, id);
        Assert.Equal(string.Empty, firma);
    }

    [Fact]
    public void TryLeer_rechaza_un_largo_incorrecto()
    {
        var clave = ClavesDePruebas.Generar(Vinculo, HashReal);

        Assert.False(ClavesDePruebas.TryLeer(clave[..^1], out _, out _));
        Assert.False(ClavesDePruebas.TryLeer(clave + "0", out _, out _));
    }

    [Fact]
    public void TryLeer_rechaza_si_falta_el_separador()
    {
        var clave = ClavesDePruebas.Generar(Vinculo, HashReal);
        var sinSeparador = clave.Remove(9 + 32, 1).Insert(9 + 32, "-");

        Assert.False(ClavesDePruebas.TryLeer(sinSeparador, out _, out _));
    }

    [Fact]
    public void TryLeer_rechaza_un_vinculo_que_no_es_guid()
    {
        var clave = "ulm_test_" + new string('z', 32) + "_" + new string('a', 64);

        Assert.False(ClavesDePruebas.TryLeer(clave, out var id, out _));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void EsValida_acepta_la_firma_correcta()
    {
        var clave = ClavesDePruebas.Generar(Vinculo, HashReal);
        ClavesDePruebas.TryLeer(clave, out var id, out var firma);

        Assert.True(ClavesDePruebas.EsValida(id, firma, HashReal));
    }

    [Fact]
    public void EsValida_ignora_mayusculas_en_la_firma()
    {
        var clave = ClavesDePruebas.Generar(Vinculo, HashReal);
        ClavesDePruebas.TryLeer(clave, out var id, out var firma);

        Assert.True(ClavesDePruebas.EsValida(id, firma.ToUpperInvariant(), HashReal));
    }

    [Fact]
    public void EsValida_rechaza_una_firma_alterada()
    {
        var clave = ClavesDePruebas.Generar(Vinculo, HashReal);
        ClavesDePruebas.TryLeer(clave, out var id, out var firma);

        var alterada = (firma[0] == '0' ? '1' : '0') + firma[1..];

        Assert.False(ClavesDePruebas.EsValida(id, alterada, HashReal));
    }

    [Fact]
    public void EsValida_rechaza_la_firma_de_otro_vinculo()
    {
        var otro = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var clave = ClavesDePruebas.Generar(Vinculo, HashReal);
        ClavesDePruebas.TryLeer(clave, out _, out var firma);

        Assert.False(ClavesDePruebas.EsValida(otro, firma, HashReal));
    }

    [Fact]
    public void EsValida_rechaza_una_firma_de_largo_distinto()
    {
        Assert.False(ClavesDePruebas.EsValida(Vinculo, "abc", HashReal));
        Assert.False(ClavesDePruebas.EsValida(Vinculo, string.Empty, HashReal));
    }
}