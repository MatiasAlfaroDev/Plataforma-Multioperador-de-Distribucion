using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

/// <summary>RF 31 a 34: estado por bulto, envío derivado de los bultos y destino de los pendientes.</summary>
public class EntregaParcialValidacionTests
{
    private static readonly DateTimeOffset Alta = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(3, 0, DisparadorEnvio.RegistrarIntentoFallido)]
    [InlineData(3, 2, DisparadorEnvio.RegistrarEntregaParcial)]
    [InlineData(3, 3, DisparadorEnvio.Entregar)]
    [InlineData(1, 1, DisparadorEnvio.Entregar)]
    public void El_resultado_de_la_visita_se_deriva_de_los_bultos(int pendientes, int entregados, DisparadorEnvio esperado) =>
        Assert.Equal(esperado, MaquinaEstadosBulto.ResultadoDeVisita(pendientes, entregados));

    [Fact]
    public void Cada_estado_de_bulto_tiene_una_salida_salvo_los_finales()
    {
        // La máquina es declarativa (Stateless) y se puede recorrer: Entregado y Devuelto no tienen salida.
        var estado = EstadoBulto.Pendiente;
        var info = MaquinaEstadosBulto.Crear(() => estado, e => estado = e).GetInfo();

        var conSalida = info.States
            .Where(s => s.Transitions.Any())
            .Select(s => (EstadoBulto)s.UnderlyingState)
            .ToHashSet();

        Assert.Equal(
            new HashSet<EstadoBulto> { EstadoBulto.Pendiente, EstadoBulto.EnDevolucion, EstadoBulto.Extraviado },
            conSalida);
    }

    [Fact]
    public void Entrega_parcial_deja_historial_por_bulto_con_motivo()
    {
        var (envio, reglas, motivo) = EnvioEnTransito(admiteParcial: true);
        var bultos = envio.Bultos.ToList();

        EntregarParcial(envio, reglas, motivo, bultos[0].Id, bultos[1].Id);

        Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.Estado);
        Assert.Equal(2, envio.BultosEntregados);
        Assert.Equal(1, envio.BultosPendientes);

        Assert.Equal(2, envio.EventosBulto.Count(e => e.Resultado == ResultadoBulto.Entregado));
        var noEntregado = Assert.Single(envio.EventosBulto, e => e.Resultado == ResultadoBulto.NoEntregado);
        Assert.Equal(bultos[2].Identificacion, noEntregado.Identificacion);
        Assert.Equal(motivo.Descripcion, noEntregado.Motivo);
        Assert.Equal(EstadoBulto.Pendiente, noEntregado.EstadoNuevo);
    }

    [Fact]
    public void Un_bulto_que_no_esta_pendiente_es_conflicto_y_se_lo_nombra()
    {
        var (envio, _, _) = EnvioEnTransito(admiteParcial: true);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            envio.ValidarBultosEntregaParcial([Guid.NewGuid()]));

        Assert.Contains("ya no están pendientes", ex.Message);
        Assert.Contains("no pertenece al envío", ex.Message);
    }

    [Fact]
    public void Extravio_y_reaparicion_se_trasladan_a_los_bultos_pendientes()
    {
        var (envio, reglas, motivo) = EnvioEnTransito(admiteParcial: true);
        var bultos = envio.Bultos.ToList();
        EntregarParcial(envio, reglas, motivo, bultos[0].Id);

        envio.Aplicar(DisparadorEnvio.DeclararExtraviado, reglas, Alta.AddHours(3), "Backoffice", "Despachador",
            justificacion: "No aparece en el depósito");

        Assert.Equal(EstadoBulto.Entregado, bultos[0].Estado);
        Assert.All(bultos.Skip(1), b => Assert.Equal(EstadoBulto.Extraviado, b.Estado));

        envio.Aplicar(DisparadorEnvio.Reaparecer, reglas, Alta.AddHours(4), "Backoffice", "Despachador");

        Assert.All(bultos.Skip(1), b => Assert.Equal(EstadoBulto.Pendiente, b.Estado));
        Assert.Equal(EstadoBulto.Entregado, bultos[0].Estado);
    }

    [Fact]
    public void Tras_la_entrega_parcial_los_pendientes_pueden_volver_a_una_hoja_de_ruta()
    {
        // RF 34: EntregadoParcialmente → depósito → Reprogramado → AsignadoARuta.
        var (envio, reglas, motivo) = EnvioEnTransito(admiteParcial: true);
        EntregarParcial(envio, reglas, motivo, envio.Bultos.First().Id);

        envio.Aplicar(DisparadorEnvio.RetornarADeposito, reglas, Alta.AddHours(3), "Deposito", "Operario");
        Assert.True(envio.AdmiteNuevaVisita(reglas));

        envio.Aplicar(DisparadorEnvio.Reprogramar, reglas, Alta.AddHours(3), "Sistema", "Reglas operativas");
        envio.AgregarRecargoNuevaVisita(150m);
        envio.Aplicar(DisparadorEnvio.AsignarARuta, reglas, Alta.AddHours(4), "Backoffice", "Despachador");

        Assert.Equal(EstadoEnvio.AsignadoARuta, envio.Estado);
        Assert.Equal(150m, envio.RecargoNuevasVisitas);
        Assert.Equal(2, envio.BultosPendientes);
    }

    [Fact]
    public void Con_devolucion_al_comercio_los_pendientes_pasan_a_devolucion()
    {
        var (envio, reglas, motivo) = EnvioEnTransito(admiteParcial: true, AccionBultosPendientes.DevolverAlComercio);
        EntregarParcial(envio, reglas, motivo, envio.Bultos.First().Id);
        envio.Aplicar(DisparadorEnvio.RetornarADeposito, reglas, Alta.AddHours(3), "Deposito", "Operario");

        Assert.True(envio.PuedeIniciarDevolucion(reglas));
        envio.Aplicar(DisparadorEnvio.IniciarDevolucion, reglas, Alta.AddHours(3), "Sistema", "Reglas operativas");

        Assert.Equal(2, envio.BultosEnDevolucion);
        Assert.Equal(1, envio.BultosEntregados);
    }

    private static void EntregarParcial(Envio envio, VersionReglasOperativas reglas, MotivoNoEntrega motivo, params Guid[] entregados)
    {
        var noEntregados = envio.Bultos
            .Where(b => b.Estado == EstadoBulto.Pendiente && !entregados.Contains(b.Id))
            .Select(b => (b.Id, motivo, "foto-bulto"))
            .ToList();

        envio.Aplicar(
            DisparadorEnvio.RegistrarEntregaParcial,
            reglas,
            Alta.AddHours(2),
            "Mobile",
            "Mobile",
            pruebaEntrega: PruebaEntrega.Crear("Ana", null, "firma", null, Alta.AddHours(2), -34.9m, -56.1m),
            bultosEntregados: entregados,
            bultosNoEntregados: noEntregados);
    }

    private static (Envio, VersionReglasOperativas, MotivoNoEntrega) EnvioEnTransito(
        bool admiteParcial,
        AccionBultosPendientes accion = AccionBultosPendientes.NuevaVisita)
    {
        var operadorId = Guid.NewGuid();
        var cuenta = CuentaComercial.Crear(Guid.NewGuid(), operadorId, Guid.NewGuid());
        var reglas = VersionReglasOperativas.Crear(operadorId, 3, 0, true, 5, Alta.AddDays(-1),
            admiteEntregaParcial: admiteParcial, accionBultosPendientes: accion);
        var motivo = MotivoNoEntrega.Crear(operadorId, reglas.Id, "DANADO", "Bulto dañado", requiereDevolucion: false);

        var envio = Envio.Crear(cuenta, "PED-31", Modalidad.Estandar, new Destinatario("Ana", null),
            new Direccion("Av. Italia", "1234", "Montevideo", "Montevideo"),
            [new NuevoBulto(1m, 10, 10, 10), new NuevoBulto(2m, 10, 10, 10), new NuevoBulto(3m, 10, 10, 10)],
            Alta);
        envio.AsignarReglasOperativas(reglas.Id);

        envio.Aplicar(DisparadorEnvio.RecibirEnDeposito, reglas, Alta.AddMinutes(10), "Backoffice", "Operario");
        envio.Aplicar(DisparadorEnvio.AsignarARuta, reglas, Alta.AddMinutes(20), "Backoffice", "Despachador");
        envio.Aplicar(DisparadorEnvio.Despachar, reglas, Alta.AddMinutes(30), "Mobile", "Repartidor");

        return (envio, reglas, motivo);
    }
}
