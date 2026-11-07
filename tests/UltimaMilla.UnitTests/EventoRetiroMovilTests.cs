using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using Xunit;

namespace UltimaMilla.UnitTests;

public sealed class EventoRetiroMovilTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 11, 7, 15, 0, 0, TimeSpan.Zero);

    private static Retiro CrearRetiro()
    {
        return Retiro.Crear(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Devolución de mercadería",
            new Direccion(
                "Artigas", "123", "San José de Mayo", "San José"),
            new Direccion(
                "Sarandí", "456", "San José de Mayo", "San José"),
            new DateOnly(2026, 11, 7),
            new TimeOnly(14, 0),
            new TimeOnly(18, 0),
            [
                BultoRetiro.Crear("BULTO-001", 2m, 20, 20, 20)
            ],
            Ahora);
    }

    [Fact]
    public void Retiro_exitoso_guarda_evidencias_y_bultos()
    {
        var retiro = CrearRetiro();
        var idEvento = Guid.NewGuid();

        var evento = EventoRetiroMovil.Crear(
            idEvento,
            retiro,
            "Retirado",
            Ahora,
            Ahora.AddMinutes(10),
            ["BULTO-001"],
            "foto-001.jpg",
            "firma-001.png",
            null);

        Assert.Equal(idEvento, evento.Id);
        Assert.Equal(retiro.Id, evento.RetiroId);
        Assert.Equal(retiro.OperadorId, evento.OperadorId);
        Assert.Equal("Retirado", evento.Tipo);
        Assert.Equal("foto-001.jpg", evento.ReferenciaFotografia);
        Assert.Equal("firma-001.png", evento.ReferenciaFirma);
        Assert.Contains("BULTO-001", evento.IdentificacionesBultos);
    }

    [Fact]
    public void Retiro_exitoso_sin_fotografia_es_rechazado()
    {
        var retiro = CrearRetiro();

        Assert.Throws<ArgumentException>(() =>
            EventoRetiroMovil.Crear(
                Guid.NewGuid(),
                retiro,
                "Retirado",
                Ahora,
                Ahora,
                ["BULTO-001"],
                null,
                "firma-001.png",
                null));
    }

    [Fact]
    public void Retiro_exitoso_sin_firma_es_rechazado()
    {
        var retiro = CrearRetiro();

        Assert.Throws<ArgumentException>(() =>
            EventoRetiroMovil.Crear(
                Guid.NewGuid(),
                retiro,
                "Retirado",
                Ahora,
                Ahora,
                ["BULTO-001"],
                "foto-001.jpg",
                null,
                null));
    }

    [Fact]
    public void Retiro_fallido_requiere_motivo()
    {
        var retiro = CrearRetiro();

        Assert.Throws<ArgumentException>(() =>
            EventoRetiroMovil.Crear(
                Guid.NewGuid(),
                retiro,
                "NoRetirado",
                Ahora,
                Ahora,
                null,
                null,
                null,
                null));
    }

    [Fact]
    public void Evento_recibido_tarde_conserva_fecha_original()
    {
        var retiro = CrearRetiro();

        var evento = EventoRetiroMovil.Crear(
            Guid.NewGuid(),
            retiro,
            "Retirado",
            Ahora,
            Ahora.AddHours(3),
            ["BULTO-001"],
            "foto.jpg",
            "firma.png",
            null);

        Assert.Equal(Ahora, evento.OcurridoEn);
        Assert.Equal(Ahora.AddHours(3), evento.RecibidoEn);
    }
}