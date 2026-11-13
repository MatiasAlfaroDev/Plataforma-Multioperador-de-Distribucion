using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using Xunit;

namespace UltimaMilla.UnitTests;

/// <summary>Entrega parcial: un envío con varios bultos donde solo algunos llegan al destinatario.</summary>
public sealed class EntregaParcialTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly VersionReglasOperativas _reglas;
    private readonly Envio _envio;
    private readonly MotivoNoEntrega _motivo;
    private readonly List<Guid> _ids;

    public EntregaParcialTests()
    {
        var cuenta = CuentaComercial.Crear(
            Guid.NewGuid(),
            _operadorId,
            Guid.NewGuid());

        _reglas = VersionReglasOperativas.Crear(
            _operadorId,
            maximoIntentosEntrega: 3,
            horasMinimasEntreIntentos: 24,
            requierePruebaEntrega: true,
            diasMaximosParaDevolucion: 7,
            vigenciaDesde: Ahora.AddDays(-1),
            admiteEntregaParcial: true);

        _envio = Envio.Crear(
            cuenta,
            "PARC-1",
            Modalidad.Estandar,
            new Destinatario("Ana Pérez", "099123456"),
            new Direccion("Av. Italia", "1234", "Montevideo", "Montevideo"),
            [new NuevoBulto(1m, 10, 10, 10), new NuevoBulto(2m, 10, 10, 10), new NuevoBulto(3m, 10, 10, 10)],
            Ahora);

        _envio.AsignarReglasOperativas(_reglas.Id);

        _motivo = MotivoNoEntrega.Crear(
            _operadorId, _reglas.Id, "AUSENTE", "Destinatario ausente", requiereDevolucion: false);

        Aplicar(DisparadorEnvio.RecibirEnDeposito);
        Aplicar(DisparadorEnvio.AsignarARuta);
        Aplicar(DisparadorEnvio.Despachar);

        _ids = _envio.Bultos.Select(b => b.Id).ToList();
    }

    private void Aplicar(DisparadorEnvio disparador) =>
        _envio.Aplicar(disparador, _reglas, Ahora, "Prueba", "Operario");

    private static PruebaEntrega Prueba() =>
        PruebaEntrega.Crear("Receptor", null, "firma-1", null, Ahora, -34.9m, -56.16m);

    private (Guid BultoId, MotivoNoEntrega Motivo, string Evidencia) NoEntregado(
        Guid bultoId, string evidencia = "foto-1") =>
        (bultoId, _motivo, evidencia);

    private void EntregaParcial(
        IReadOnlyCollection<Guid>? entregados,
        IReadOnlyCollection<(Guid BultoId, MotivoNoEntrega Motivo, string Evidencia)>? noEntregados,
        PruebaEntrega? prueba) =>
        _envio.Aplicar(
            DisparadorEnvio.RegistrarEntregaParcial,
            _reglas,
            Ahora,
            "Prueba",
            "Repartidor",
            pruebaEntrega: prueba,
            bultosEntregados: entregados,
            bultosNoEntregados: noEntregados);

    private EstadoBulto EstadoDe(Guid bultoId) =>
        _envio.Bultos.Single(b => b.Id == bultoId).Estado;

    // ----- Envio.Aplicar(RegistrarEntregaParcial) -----

    [Fact]
    public void EntregaParcial_RegistraResultadosPorBultoYActualizaLosEstados()
    {
        EntregaParcial(
            [_ids[0]],
            [NoEntregado(_ids[1]), NoEntregado(_ids[2], "foto-2")],
            Prueba());

        Assert.Equal(EstadoEnvio.EntregadoParcialmente, _envio.Estado);
        Assert.Equal(1, _envio.IntentosRealizados);

        var intento = Assert.Single(_envio.Intentos);
        Assert.False(intento.Entregado);
        Assert.NotNull(intento.PruebaEntrega);
        Assert.Equal(3, intento.ResultadosBultos.Count);
        Assert.Single(intento.ResultadosBultos, r => r.Entregado);

        var fallidos = intento.ResultadosBultos.Where(r => !r.Entregado).ToList();
        Assert.Equal(2, fallidos.Count);
        Assert.All(fallidos, r => Assert.Equal(_motivo.Id, r.MotivoNoEntregaId));
        Assert.Contains(fallidos, r => r.ReferenciaEvidenciaFallo == "foto-2");

        Assert.Equal(EstadoBulto.Entregado, EstadoDe(_ids[0]));
        Assert.Equal(EstadoBulto.Pendiente, EstadoDe(_ids[1]));
        Assert.Equal(EstadoBulto.Pendiente, EstadoDe(_ids[2]));

        Assert.Equal(_motivo.Id, _envio.UltimoMotivoNoEntregaId);
        Assert.False(_envio.UltimoMotivoRequiereDevolucion);
        Assert.Equal(DisparadorEnvio.RegistrarEntregaParcial, _envio.Eventos.Last().Disparador);
    }

    [Fact]
    public void EntregaParcial_ConMotivosDistintos_NoGuardaUnUnicoMotivoYMarcaDevolucion()
    {
        var otro = MotivoNoEntrega.Crear(
            _operadorId, _reglas.Id, "DIRECCION", "Dirección incorrecta", requiereDevolucion: true);

        EntregaParcial(
            [_ids[0]],
            [NoEntregado(_ids[1]), (_ids[2], otro, "foto-2")],
            Prueba());

        Assert.Null(_envio.UltimoMotivoNoEntregaId);
        Assert.True(_envio.UltimoMotivoRequiereDevolucion);
    }

    [Fact]
    public void EntregaParcial_SinPrueba_Falla()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([_ids[0]], [NoEntregado(_ids[1]), NoEntregado(_ids[2])], null));
    }

    [Fact]
    public void EntregaParcial_ConPruebaSinFirmaNiFotografia_FallaSiLasReglasLaExigen()
    {
        var vacia = PruebaEntrega.Crear("Receptor", null, null, null, Ahora, -34.9m, -56.16m);

        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([_ids[0]], [NoEntregado(_ids[1]), NoEntregado(_ids[2])], vacia));
    }

    [Fact]
    public void EntregaParcial_SinListaDeNoEntregados_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            EntregaParcial([_ids[0]], null, Prueba()));
    }

    [Fact]
    public void EntregaParcial_SinBultosEntregados_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            EntregaParcial([], [NoEntregado(_ids[1])], Prueba()));
    }

    [Fact]
    public void EntregaParcial_ConBultosEntregadosRepetidos_Falla()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([_ids[0], _ids[0]], [NoEntregado(_ids[1])], Prueba()));
    }

    [Fact]
    public void EntregaParcial_SiSeEntreganTodosLosBultos_Falla()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([_ids[0], _ids[1], _ids[2]], [], Prueba()));
    }

    [Fact]
    public void EntregaParcial_ConUnBultoQueNoEsDelEnvio_Falla()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([Guid.NewGuid()], [NoEntregado(_ids[1]), NoEntregado(_ids[2])], Prueba()));
    }

    [Fact]
    public void EntregaParcial_SinBultosNoEntregados_Falla()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([_ids[0]], [], Prueba()));
    }

    [Fact]
    public void EntregaParcial_ConBultosNoEntregadosRepetidos_Falla()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([_ids[0]], [NoEntregado(_ids[1]), NoEntregado(_ids[1])], Prueba()));
    }

    [Fact]
    public void EntregaParcial_SiNoSeInformanTodosLosPendientes_Falla()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([_ids[0]], [NoEntregado(_ids[1])], Prueba()));
    }

    [Fact]
    public void EntregaParcial_ConMotivoDeOtroOperador_Falla()
    {
        var ajeno = MotivoNoEntrega.Crear(
            Guid.NewGuid(), _reglas.Id, "AJENO", "Motivo de otro operador", requiereDevolucion: false);

        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([_ids[0]], [(_ids[1], ajeno, "foto-1"), NoEntregado(_ids[2])], Prueba()));
    }

    [Fact]
    public void EntregaParcial_ConEvidenciaVacia_Falla()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EntregaParcial([_ids[0]], [NoEntregado(_ids[1], "  "), NoEntregado(_ids[2])], Prueba()));
    }

    [Fact]
    public void EntregaParcial_NoSeAplicaSiElEnvioNoEstaEnTransito()
    {
        var otro = Envio.Crear(
            CuentaComercial.Crear(Guid.NewGuid(), _operadorId, Guid.NewGuid()),
            "PARC-2",
            Modalidad.Estandar,
            new Destinatario("Ana Pérez", "099123456"),
            new Direccion("Av. Italia", "1234", "Montevideo", "Montevideo"),
            [new NuevoBulto(1m, 10, 10, 10), new NuevoBulto(2m, 10, 10, 10)],
            Ahora);
        otro.AsignarReglasOperativas(_reglas.Id);

        Assert.Throws<InvalidOperationException>(() =>
            otro.Aplicar(
                DisparadorEnvio.RegistrarEntregaParcial,
                _reglas,
                Ahora,
                "Prueba",
                "Repartidor",
                pruebaEntrega: Prueba(),
                bultosEntregados: [otro.Bultos.First().Id],
                bultosNoEntregados: [(otro.Bultos.Last().Id, _motivo, "foto-1")]));
    }

    // ----- IntentoEntrega -----

    [Fact]
    public void IntentoFallo_Valido_GuardaMotivoYEvidenciaSinEspacios()
    {
        var motivoId = Guid.NewGuid();

        var intento = IntentoEntrega.RegistrarFallo(
            _operadorId, Guid.NewGuid(), 1, motivoId, "  foto-1  ", Ahora);

        Assert.False(intento.Entregado);
        Assert.Equal(motivoId, intento.MotivoNoEntregaId);
        Assert.Equal("foto-1", intento.ReferenciaEvidenciaFallida);
        Assert.Equal(Ahora, intento.OcurridoEn);
        Assert.Equal(1, intento.Numero);
    }

    [Fact]
    public void IntentoFallo_ConMotivoVacio_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarFallo(_operadorId, Guid.NewGuid(), 1, Guid.Empty, "foto", Ahora));
    }

    [Fact]
    public void IntentoFallo_SinEvidencia_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarFallo(_operadorId, Guid.NewGuid(), 1, Guid.NewGuid(), " ", Ahora));
    }

    [Fact]
    public void IntentoFallo_ConNumeroNoPositivo_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarFallo(_operadorId, Guid.NewGuid(), 0, Guid.NewGuid(), "foto", Ahora));
    }

    [Fact]
    public void IntentoFallo_SinOperador_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarFallo(Guid.Empty, Guid.NewGuid(), 1, Guid.NewGuid(), "foto", Ahora));
    }

    [Fact]
    public void IntentoEntrega_SinPrueba_Falla()
    {
        Assert.Throws<ArgumentNullException>(() =>
            IntentoEntrega.RegistrarEntrega(_operadorId, Guid.NewGuid(), 1, null!));
    }

    [Fact]
    public void IntentoEntrega_Valido_QuedaEntregadoConLaPrueba()
    {
        var prueba = Prueba();

        var intento = IntentoEntrega.RegistrarEntrega(_operadorId, Guid.NewGuid(), 2, prueba);

        Assert.True(intento.Entregado);
        Assert.Same(prueba, intento.PruebaEntrega);
        Assert.Equal(prueba.OcurridoEn, intento.OcurridoEn);
        Assert.Empty(intento.ResultadosBultos);
    }

    [Fact]
    public void IntentoParcial_Valido_CreaUnResultadoPorBulto()
    {
        var intento = IntentoEntrega.RegistrarEntregaParcial(
            _operadorId,
            Guid.NewGuid(),
            1,
            [Guid.NewGuid()],
            [(Guid.NewGuid(), Guid.NewGuid(), "  foto  ")],
            Prueba());

        Assert.Equal(2, intento.ResultadosBultos.Count);
        var fallido = Assert.Single(intento.ResultadosBultos, r => !r.Entregado);
        Assert.Equal("foto", fallido.ReferenciaEvidenciaFallo);
        Assert.NotNull(fallido.MotivoNoEntregaId);
        Assert.All(intento.ResultadosBultos, r => Assert.Equal(intento.Id, r.IntentoEntregaId));
    }

    [Fact]
    public void IntentoParcial_SinBultosEntregados_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarEntregaParcial(
                _operadorId, Guid.NewGuid(), 1,
                [],
                [(Guid.NewGuid(), Guid.NewGuid(), "foto")],
                Prueba()));
    }

    [Fact]
    public void IntentoParcial_SinBultosNoEntregados_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarEntregaParcial(
                _operadorId, Guid.NewGuid(), 1,
                [Guid.NewGuid()],
                [],
                Prueba()));
    }

    [Fact]
    public void IntentoParcial_ConIdentificacionVacia_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarEntregaParcial(
                _operadorId, Guid.NewGuid(), 1,
                [Guid.Empty],
                [(Guid.NewGuid(), Guid.NewGuid(), "foto")],
                Prueba()));
    }

    [Fact]
    public void IntentoParcial_ConBultoRepetido_Falla()
    {
        var repetido = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarEntregaParcial(
                _operadorId, Guid.NewGuid(), 1,
                [repetido],
                [(repetido, Guid.NewGuid(), "foto")],
                Prueba()));
    }

    [Fact]
    public void IntentoParcial_ConMotivoVacio_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarEntregaParcial(
                _operadorId, Guid.NewGuid(), 1,
                [Guid.NewGuid()],
                [(Guid.NewGuid(), Guid.Empty, "foto")],
                Prueba()));
    }

    [Fact]
    public void IntentoParcial_ConEvidenciaVacia_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            IntentoEntrega.RegistrarEntregaParcial(
                _operadorId, Guid.NewGuid(), 1,
                [Guid.NewGuid()],
                [(Guid.NewGuid(), Guid.NewGuid(), " ")],
                Prueba()));
    }

    [Fact]
    public void IntentoParcial_SinPrueba_Falla()
    {
        Assert.Throws<ArgumentNullException>(() =>
            IntentoEntrega.RegistrarEntregaParcial(
                _operadorId, Guid.NewGuid(), 1,
                [Guid.NewGuid()],
                [(Guid.NewGuid(), Guid.NewGuid(), "foto")],
                null!));
    }

    // ----- PruebaEntrega -----

    [Fact]
    public void PruebaEntrega_LimpiaEspaciosYGuardaLosDatos()
    {
        var prueba = PruebaEntrega.Crear(
            "  Receptor  ", " 1234567-8 ", " firma ", " foto ", Ahora, -34.9m, -56.16m);

        Assert.Equal("Receptor", prueba.NombreReceptor);
        Assert.Equal("1234567-8", prueba.DocumentoReceptor);
        Assert.Equal("firma", prueba.ReferenciaFirma);
        Assert.Equal("foto", prueba.ReferenciaFotografia);
    }

    [Fact]
    public void PruebaEntrega_SinReceptor_Falla()
    {
        Assert.Throws<ArgumentException>(() =>
            PruebaEntrega.Crear(" ", null, "firma", null, Ahora, 0m, 0m));
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void PruebaEntrega_ConCoordenadasInvalidas_Falla(int latitud, int longitud)
    {
        Assert.Throws<ArgumentException>(() =>
            PruebaEntrega.Crear("Receptor", null, "firma", null, Ahora, latitud, longitud));
    }
}
