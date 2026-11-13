using System.Text.Json;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisos;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

/// <summary>RF 36: el contrato de los avisos se versiona; la v1 no puede cambiar.</summary>
public class ContratoAvisosTests
{
    private static readonly DateTimeOffset Momento = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void El_cuerpo_v1_conserva_exactamente_sus_campos()
    {
        var envio = NuevoEnvio();
        var comando = new RegistrarAvisosCommand(envio.Id, TiposAviso.EnvioCreado, "clave", Momento);

        var json = JsonDocument.Parse(RegistrarAvisosHandler.CuerpoDelAviso(1, comando, envio)).RootElement;

        // Cualquier campo nuevo rompería a los comercios que ya consumen la v1.
        Assert.Equal(
            ["tipo", "envioId", "referenciaExterna", "estado", "ocurridoEn"],
            json.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("envio.creado", json.GetProperty("tipo").GetString());
        Assert.Equal("PED-36", json.GetProperty("referenciaExterna").GetString());
    }

    [Fact]
    public void El_cuerpo_v2_agrega_version_y_bultos()
    {
        var envio = NuevoEnvio();
        var comando = new RegistrarAvisosCommand(envio.Id, TiposAviso.BultoNoEntregado, "clave", Momento,
            new BultoDelAviso("PED-36-2", "Pendiente", "Pendiente", "Bulto dañado"));

        var json = JsonDocument.Parse(RegistrarAvisosHandler.CuerpoDelAviso(2, comando, envio)).RootElement;

        Assert.Equal(2, json.GetProperty("version").GetInt32());
        Assert.Equal(2, json.GetProperty("bultos").GetProperty("total").GetInt32());
        Assert.Equal("PED-36-2", json.GetProperty("bulto").GetProperty("identificacion").GetString());
        Assert.Equal("Bulto dañado", json.GetProperty("bulto").GetProperty("motivo").GetString());
    }

    [Fact]
    public void Una_suscripcion_v1_no_escucha_avisos_de_bulto()
    {
        // Aunque se suscriba a "todos" los tipos, la v1 no recibe los de bulto.
        var v1 = SuscripcionAviso.Crear(Guid.NewGuid(), Guid.NewGuid(), "https://tienda.uy/avisos", "s",
            TiposAviso.Todos);
        var v2 = SuscripcionAviso.Crear(Guid.NewGuid(), Guid.NewGuid(), "https://tienda.uy/avisos", "s",
            [TiposAviso.EnvioEntregado, TiposAviso.BultoEntregado], versionContrato: 2);

        Assert.False(v1.Escucha(TiposAviso.BultoEntregado));
        Assert.True(v1.Escucha(TiposAviso.EnvioEntregado));
        Assert.True(v2.Escucha(TiposAviso.BultoEntregado));
    }

    private static Envio NuevoEnvio()
    {
        var cuenta = CuentaComercial.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        return Envio.Crear(cuenta, "PED-36", Modalidad.Estandar, new Destinatario("Ana", null),
            new Direccion("Av. Italia", "1234", "Montevideo", "Montevideo"),
            [new NuevoBulto(1m, 10, 10, 10), new NuevoBulto(1m, 10, 10, 10)],
            Momento);
    }
}
