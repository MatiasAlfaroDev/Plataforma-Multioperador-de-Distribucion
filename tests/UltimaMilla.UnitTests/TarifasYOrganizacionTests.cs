using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

public class TarifasYOrganizacionTests
{
    private static readonly Guid IdOperador = Guid.NewGuid();
    private static readonly Guid Cuadro = Guid.NewGuid();
    private static readonly Guid Zona = Guid.NewGuid();
    private static readonly DateTimeOffset Desde = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static VersionTarifaria Version(
        decimal precioBase = 100m,
        decimal porKg = 10m,
        decimal porM3 = 50m,
        decimal recargo = 20m,
        decimal bonificacion = 10m) =>
        VersionTarifaria.Crear(IdOperador, Cuadro, Zona, precioBase, porKg, porM3, recargo, bonificacion, Desde);

    // ----- VersionTarifaria -----

    [Fact]
    public void Tarifa_estandar_suma_base_peso_y_volumen_y_aplica_bonificacion()
    {
        // 100 + 2*10 + 0,5*50 = 145; menos 10 % = 130,5
        Assert.Equal(130.5m, Version().Calcular(2m, 0.5m, Modalidad.Estandar));
    }

    [Fact]
    public void Tarifa_urgente_aplica_recargo_antes_de_la_bonificacion()
    {
        // 145 + 20 % = 174; menos 10 % = 156,6
        Assert.Equal(156.6m, Version().Calcular(2m, 0.5m, Modalidad.Urgente));
    }

    [Fact]
    public void Sin_bonificacion_no_se_descuenta_nada()
    {
        Assert.Equal(145m, Version(bonificacion: 0m).Calcular(2m, 0.5m, Modalidad.Estandar));
    }

    [Fact]
    public void La_tarifa_se_redondea_a_dos_decimales()
    {
        var version = Version(precioBase: 0m, porKg: 1m, porM3: 0m, recargo: 0m, bonificacion: 0m);
        Assert.Equal(0.33m, version.Calcular(0.3333m, 0m, Modalidad.Estandar));
    }

    [Fact]
    public void Una_version_nueva_conserva_los_datos_y_no_tiene_fin_de_vigencia()
    {
        var version = Version();

        Assert.NotEqual(Guid.Empty, version.Id);
        Assert.Equal(IdOperador, version.OperadorId);
        Assert.Equal(Cuadro, version.CuadroTarifarioId);
        Assert.Equal(Zona, version.ZonaCoberturaId);
        Assert.Equal(TipoServicioTarifario.Entrega, version.TipoServicio);
        Assert.Equal(Desde, version.VigenciaDesde);
        Assert.Null(version.VigenciaHasta);
    }

    [Fact]
    public void Vigencia_abierta_cubre_desde_el_inicio_en_adelante()
    {
        var version = Version();

        Assert.False(version.EstaVigente(Desde.AddTicks(-1)));
        Assert.True(version.EstaVigente(Desde));
        Assert.True(version.EstaVigente(Desde.AddYears(5)));
    }

    [Fact]
    public void Al_finalizar_la_vigencia_el_fin_queda_excluido()
    {
        var version = Version();
        var fin = Desde.AddDays(30);

        version.FinalizarVigencia(fin);

        Assert.Equal(fin, version.VigenciaHasta);
        Assert.True(version.EstaVigente(fin.AddTicks(-1)));
        Assert.False(version.EstaVigente(fin));
    }

    [Fact]
    public void No_se_puede_finalizar_una_vigencia_en_o_antes_de_su_inicio()
    {
        var version = Version();

        Assert.Throws<ArgumentException>(() => version.FinalizarVigencia(Desde));
        Assert.Throws<ArgumentException>(() => version.FinalizarVigencia(Desde.AddDays(-1)));
    }

    [Fact]
    public void La_version_exige_operador_cuadro_y_zona()
    {
        Assert.Throws<ArgumentException>(() =>
            VersionTarifaria.Crear(Guid.Empty, Cuadro, Zona, 1, 1, 1, 0, 0, Desde));
        Assert.Throws<ArgumentException>(() =>
            VersionTarifaria.Crear(IdOperador, Guid.Empty, Zona, 1, 1, 1, 0, 0, Desde));
        Assert.Throws<ArgumentException>(() =>
            VersionTarifaria.Crear(IdOperador, Cuadro, Guid.Empty, 1, 1, 1, 0, 0, Desde));
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(0, -1, 0, 0, 0)]
    [InlineData(0, 0, -1, 0, 0)]
    [InlineData(0, 0, 0, -1, 0)]
    [InlineData(0, 0, 0, 0, -1)]
    [InlineData(0, 0, 0, 0, 101)]
    public void La_version_rechaza_precios_recargos_o_bonificaciones_invalidos(
        decimal precioBase, decimal porKg, decimal porM3, decimal recargo, decimal bonificacion)
    {
        Assert.Throws<ArgumentException>(() =>
            VersionTarifaria.Crear(IdOperador, Cuadro, Zona, precioBase, porKg, porM3, recargo, bonificacion, Desde));
    }

    [Fact]
    public void La_version_rechaza_un_tipo_de_servicio_inexistente()
    {
        Assert.Throws<ArgumentException>(() =>
            VersionTarifaria.Crear(IdOperador, Cuadro, Zona, 1, 1, 1, 0, 0, Desde, (TipoServicioTarifario)999));
    }

    // ----- CuadroTarifario -----

    [Fact]
    public void El_cuadro_nuevo_queda_activo_y_con_el_nombre_recortado()
    {
        var cuadro = CuadroTarifario.Crear(IdOperador, "  Cuadro 2026  ");

        Assert.Equal("Cuadro 2026", cuadro.Nombre);
        Assert.Equal(IdOperador, cuadro.OperadorId);
        Assert.True(cuadro.Activo);
        Assert.NotEqual(Guid.Empty, cuadro.Id);
    }

    [Fact]
    public void El_cuadro_se_puede_desactivar()
    {
        var cuadro = CuadroTarifario.Crear(IdOperador, "Cuadro");

        cuadro.Desactivar();

        Assert.False(cuadro.Activo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_cuadro_exige_nombre(string nombre)
    {
        Assert.Throws<ArgumentException>(() => CuadroTarifario.Crear(IdOperador, nombre));
    }

    [Fact]
    public void El_cuadro_exige_operador()
    {
        Assert.Throws<ArgumentException>(() => CuadroTarifario.Crear(Guid.Empty, "Cuadro"));
    }

    // ----- Operador y Comercio -----

    [Fact]
    public void El_operador_recorta_el_nombre_y_conserva_marca_y_contacto()
    {
        var id = Guid.NewGuid();

        var operador = CrearOperador(id, "  Operador Uno ");

        Assert.Equal(id, operador.Id);
        Assert.Equal("Operador Uno", operador.Nombre);
        Assert.Equal("#112233", operador.ColorPrimario);
        Assert.Equal("contacto@uno.test", operador.EmailContacto);
    }

    [Fact]
    public void El_operador_exige_nombre()
    {
        Assert.Throws<ArgumentException>(() =>
            Operador.Crear(Guid.NewGuid(), " ", "#000000", "a@b.test"));
    }

    [Fact]
    public void El_comercio_recorta_rut_y_nombre()
    {
        var comercio = Comercio.Crear(Guid.NewGuid(), " 211234560018 ", " Tienda ");

        Assert.Equal("211234560018", comercio.Rut);
        Assert.Equal("Tienda", comercio.Nombre);
    }

    [Fact]
    public void El_comercio_exige_rut_y_nombre()
    {
        Assert.Throws<ArgumentException>(() => Comercio.Crear(Guid.NewGuid(), "", "Tienda"));
        Assert.Throws<ArgumentException>(() => Comercio.Crear(Guid.NewGuid(), "211234560018", " "));
    }

    [Fact]
    public void La_auditoria_registra_creacion_y_actualizacion()
    {
        var comercio = Comercio.Crear(Guid.NewGuid(), "211234560018", "Tienda");
        var creado = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var actualizado = creado.AddHours(2);

        comercio.MarcarCreado(creado);
        comercio.MarcarActualizado(actualizado);

        Assert.Equal(creado, comercio.CreadoEn);
        Assert.Equal(actualizado, comercio.ActualizadoEn);
    }

    // ----- CuentaComercial -----

    [Fact]
    public void Crear_una_cuenta_la_deja_confirmada_y_activa()
    {
        var cuenta = CuentaComercial.Crear(Guid.NewGuid(), IdOperador, Guid.NewGuid());

        Assert.Equal(EstadoCuentaComercial.Confirmada, cuenta.Estado);
        Assert.True(cuenta.Activa);
    }

    [Fact]
    public void Solicitar_una_cuenta_la_deja_pendiente_e_inactiva()
    {
        var cuenta = CuentaComercial.Solicitar(Guid.NewGuid(), IdOperador, Guid.NewGuid());

        Assert.Equal(EstadoCuentaComercial.Pendiente, cuenta.Estado);
        Assert.False(cuenta.Activa);
    }

    [Fact]
    public void Confirmar_una_solicitud_pendiente_la_activa()
    {
        var cuenta = CuentaComercial.Solicitar(Guid.NewGuid(), IdOperador, Guid.NewGuid());

        cuenta.Confirmar();

        Assert.Equal(EstadoCuentaComercial.Confirmada, cuenta.Estado);
        Assert.True(cuenta.Activa);
    }

    [Fact]
    public void Rechazar_una_solicitud_pendiente_la_deja_inactiva()
    {
        var cuenta = CuentaComercial.Solicitar(Guid.NewGuid(), IdOperador, Guid.NewGuid());

        cuenta.Rechazar();

        Assert.Equal(EstadoCuentaComercial.Rechazada, cuenta.Estado);
        Assert.False(cuenta.Activa);
    }

    [Fact]
    public void Solo_se_confirma_o_rechaza_lo_pendiente()
    {
        var confirmada = CuentaComercial.Crear(Guid.NewGuid(), IdOperador, Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => confirmada.Confirmar());
        Assert.Throws<InvalidOperationException>(() => confirmada.Rechazar());
    }

    [Fact]
    public void Desactivar_deja_la_cuenta_inactiva_sin_cambiar_su_estado()
    {
        var cuenta = CuentaComercial.Crear(Guid.NewGuid(), IdOperador, Guid.NewGuid());

        cuenta.Desactivar();

        Assert.False(cuenta.Activa);
        Assert.Equal(EstadoCuentaComercial.Confirmada, cuenta.Estado);
    }

    [Fact]
    public void La_direccion_de_recepcion_se_configura_y_no_admite_nulos()
    {
        var cuenta = CuentaComercial.Crear(Guid.NewGuid(), IdOperador, Guid.NewGuid());
        var direccion = new Direccion("18 de Julio", "1234", "San José de Mayo", "San José");

        cuenta.ConfigurarDireccionRecepcion(direccion);

        Assert.Same(direccion, cuenta.DireccionRecepcion);
        Assert.Throws<ArgumentNullException>(() => cuenta.ConfigurarDireccionRecepcion(null!));
    }

    private static Operador CrearOperador(Guid id, string nombre) =>
        Operador.Crear(id, nombre, "#112233", "contacto@uno.test");
}
