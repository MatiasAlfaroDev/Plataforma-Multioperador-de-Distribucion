
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Domain.Rutas;

public sealed class HojaDeRuta : AuditableEntity, ITenantEntity
{
    private readonly List<ParadaRuta> _paradas = [];

    public Guid OperadorId { get; private set; }
    public Guid RepartidorId { get; private set; }
    public Guid VehiculoId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public TimeOnly? HoraSalida { get; private set; }
    public int MaximoParadas { get; private set; }
    public decimal CapacidadPesoKg { get; private set; }
    public decimal CapacidadVolumenM3 { get; private set; }

    public decimal PesoAsignadoKg { get; private set; }
    public decimal VolumenAsignadoM3 { get; private set; }
    public EstadoHojaDeRuta Estado { get; private set; }
    public DateTimeOffset? DespachadaEn { get; private set; }
    public IReadOnlyCollection<ParadaRuta> Paradas => _paradas;

    private HojaDeRuta() { }


    public static HojaDeRuta Crear(
        Guid operadorId,
        Repartidor repartidor,
        Vehiculo vehiculo,
        DateOnly fecha,
        int maximoParadas)
    {
        ArgumentNullException.ThrowIfNull(repartidor);
        ArgumentNullException.ThrowIfNull(vehiculo);

        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        if (repartidor.OperadorId != operadorId)
            throw new InvalidOperationException(
                "El repartidor pertenece a otro operador.");

        if (vehiculo.OperadorId != operadorId)
            throw new InvalidOperationException(
                "El vehículo pertenece a otro operador.");

        if (!repartidor.Activo)
            throw new InvalidOperationException(
                "El repartidor está inactivo.");

        if (!vehiculo.Activo)
            throw new InvalidOperationException(
                "El vehículo está inactivo.");

        if (maximoParadas <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximoParadas));


        return new HojaDeRuta
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            RepartidorId = repartidor.Id,
            VehiculoId = vehiculo.Id,
            Fecha = fecha,
            MaximoParadas = maximoParadas,
            CapacidadPesoKg = vehiculo.CapacidadPesoKg,
            CapacidadVolumenM3 = vehiculo.CapacidadVolumenM3,
            Estado = EstadoHojaDeRuta.EnPreparacion
        };
    }

    public void AgregarEnvio(Envio envio)
    {
        ArgumentNullException.ThrowIfNull(envio);

        VerificarEditable();

        if (envio.OperadorId != OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (envio.Estado != EstadoEnvio.EnDeposito &&
            envio.Estado != EstadoEnvio.Reprogramado)
            throw new InvalidOperationException(
                "El envío no está disponible para asignación.");

        if (envio.Estado == EstadoEnvio.Reprogramado)
        {
            if (envio.FechaEntregaComprometida != Fecha)
            {
                throw new InvalidOperationException(
                    "La fecha de la ruta no coincide con la reprogramación.");
            }
        }
        else if (envio.FechaEntregaComprometida.HasValue &&
                envio.FechaEntregaComprometida.Value != Fecha)
        {
            throw new InvalidOperationException(
                "La fecha de la ruta no coincide con la fecha comprometida.");
        }

        if (_paradas.Any(p => p.EnvioId == envio.Id && p.Activa))
            throw new InvalidOperationException(
                "El envío ya está incluido en esta hoja de ruta.");

        if (_paradas.Count(p => p.Activa) >= MaximoParadas)
            throw new InvalidOperationException(
                "Se alcanzó el máximo de paradas.");

        if (PesoAsignadoKg + envio.PesoPendienteKg > CapacidadPesoKg)
            throw new InvalidOperationException(
                "La carga supera la capacidad de peso.");

        if (VolumenAsignadoM3 +
            envio.VolumenPendienteMetrosCubicos > CapacidadVolumenM3)
            throw new InvalidOperationException(
                "La carga supera la capacidad de volumen.");

        var siguienteOrden = _paradas.Count == 0
            ? 1
            : _paradas.Max(p => p.Orden) + 1;

        var parada = ParadaRuta.Crear(
            Id,
            envio.Id,
            siguienteOrden);

                _paradas.Add(parada);

        PesoAsignadoKg += envio.PesoPendienteKg;
        VolumenAsignadoM3 += envio.VolumenPendienteMetrosCubicos;
    }

    public void AgregarRetiro(Retiro retiro)
    {
        ArgumentNullException.ThrowIfNull(retiro);

        VerificarEditable();

        if (retiro.OperadorId != OperadorId)
        {
            throw new InvalidOperationException(
                "El retiro pertenece a otro operador.");
        }

        if (retiro.Estado != EstadoRetiro.Solicitado)
        {
            throw new InvalidOperationException(
                "El retiro no está disponible para asignación.");
        }

        if (retiro.FechaSolicitada != Fecha)
        {
            throw new InvalidOperationException(
                "La fecha solicitada del retiro no coincide con la ruta.");
        }

        if (_paradas.Any(p =>
            p.RetiroId == retiro.Id && p.Activa))
        {
            throw new InvalidOperationException(
                "El retiro ya está incluido en esta hoja de ruta.");
        }

        if (_paradas.Count(p => p.Activa) >= MaximoParadas)
        {
            throw new InvalidOperationException(
                "Se alcanzó el máximo de paradas.");
        }

        var siguienteOrden = _paradas.Count == 0
            ? 1
            : _paradas.Max(p => p.Orden) + 1;

        var parada = ParadaRuta.CrearRetiro(
            Id,
            retiro.Id,
            siguienteOrden);

        _paradas.Add(parada);
    }

    public void AgregarDevolucionComercio(Envio envio)
    {
        ArgumentNullException.ThrowIfNull(envio);
        VerificarEditable();

        if (envio.OperadorId != OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (envio.Estado != EstadoEnvio.EnDevolucion)
            throw new InvalidOperationException(
                "El envío no está disponible para devolución al comercio.");

        if (_paradas.Any(p => p.EnvioId == envio.Id && p.Activa))
            throw new InvalidOperationException(
                "El envío ya tiene una parada activa en esta hoja.");

        if (_paradas.Count(p => p.Activa) >= MaximoParadas)
            throw new InvalidOperationException(
                "Se alcanzó el máximo de paradas.");

        var bultos = envio.Bultos
            .Where(b => b.Estado == EstadoBulto.EnDevolucion)
            .ToList();

        if (bultos.Count == 0)
            throw new InvalidOperationException(
                "El envío no tiene bultos para devolver.");

        var peso = bultos.Sum(b => b.PesoKg);
        var volumen = bultos.Sum(b => b.VolumenMetrosCubicos);

        if (PesoAsignadoKg + peso > CapacidadPesoKg ||
            VolumenAsignadoM3 + volumen > CapacidadVolumenM3)
            throw new InvalidOperationException(
                "La devolución supera la capacidad del vehículo.");

        var siguienteOrden = _paradas.Count == 0
            ? 1
            : _paradas.Max(p => p.Orden) + 1;

        _paradas.Add(
            ParadaRuta.CrearDevolucionComercio(
                Id, envio.Id, siguienteOrden));

        PesoAsignadoKg += peso;
        VolumenAsignadoM3 += volumen;
    }

    public void AgregarEntregaRetiroComercio(Retiro retiro)
    {
        ArgumentNullException.ThrowIfNull(retiro);
        VerificarEditable();

        if (retiro.OperadorId != OperadorId)
            throw new InvalidOperationException(
                "El retiro pertenece a otro operador.");

        if (retiro.Estado != EstadoRetiro.EnDeposito)
            throw new InvalidOperationException(
                "El retiro debe estar en depósito.");

        if (_paradas.Any(p => p.RetiroId == retiro.Id && p.Activa))
            throw new InvalidOperationException(
                "El retiro ya tiene una parada activa en esta hoja.");

        if (_paradas.Count(p => p.Activa) >= MaximoParadas)
            throw new InvalidOperationException(
                "Se alcanzó el máximo de paradas.");

        if (PesoAsignadoKg + retiro.PesoTotalKg > CapacidadPesoKg ||
            VolumenAsignadoM3 + retiro.VolumenTotalMetrosCubicos >
            CapacidadVolumenM3)
            throw new InvalidOperationException(
                "El retiro supera la capacidad del vehículo.");

        var siguienteOrden = _paradas.Count == 0
            ? 1
            : _paradas.Max(p => p.Orden) + 1;

        _paradas.Add(
            ParadaRuta.CrearEntregaRetiroComercio(
                Id, retiro.Id, siguienteOrden));

        PesoAsignadoKg += retiro.PesoTotalKg;
        VolumenAsignadoM3 += retiro.VolumenTotalMetrosCubicos;
    }


    public void FinalizarParadaRetiro(
        Retiro retiro,
        DateTimeOffset fecha)
    {
        ArgumentNullException.ThrowIfNull(retiro);

        if (retiro.OperadorId != OperadorId)
        {
            throw new InvalidOperationException(
                "El retiro pertenece a otro operador.");
        }

        if (retiro.HojaDeRutaId != Id)
        {
            throw new InvalidOperationException(
                "El retiro no pertenece a esta hoja de ruta.");
        }

        if (retiro.Estado != EstadoRetiro.Retirado &&
            retiro.Estado != EstadoRetiro.NoRetirado)
        {
            throw new InvalidOperationException(
                "El retiro todavía no tiene un resultado registrado.");
        }

        var parada = _paradas.SingleOrDefault(
            p => p.RetiroId == retiro.Id && p.Activa);

        if (parada is null)
        {
            throw new InvalidOperationException(
                "No existe una parada activa para este retiro.");
        }

        parada.Liberar(fecha);
    }

    public void ActualizarOrdenOperaciones(
        IReadOnlyList<HorarioOperacionRuta> horarios)
    {
        VerificarEditable();
        ArgumentNullException.ThrowIfNull(horarios);

        var activas = _paradas
            .Where(p => p.Activa)
            .ToList();

        if (horarios.Count != activas.Count)
            throw new InvalidOperationException(
                "La planificación no coincide con las paradas activas.");

        if (horarios
            .Select(h => (h.Tipo, h.ReferenciaId))
            .Distinct()
            .Count() != horarios.Count)
        {
            throw new InvalidOperationException(
                "La planificación contiene operaciones repetidas.");
        }

        foreach (var parada in activas)
        {
            var existe = horarios.Any(h =>
                h.Tipo == parada.TipoOperacion &&
                (parada.EnvioId == h.ReferenciaId ||
                parada.RetiroId == h.ReferenciaId));

            if (!existe)
                throw new InvalidOperationException(
                    "La planificación omite una parada activa.");
        }

        // Conservamos la renumeración en dos pasos
        // para evitar conflictos de orden entre paradas.
        PrepararRenumeracionParadas();

        for (var i = 0; i < horarios.Count; i++)
        {
            var horario = horarios[i];

            var parada = activas.Single(p =>
                p.TipoOperacion == horario.Tipo &&
                (p.EnvioId == horario.ReferenciaId ||
                p.RetiroId == horario.ReferenciaId));

            parada.CambiarOrden(i + 1);
        }
    }

    public void QuitarEnvio(Envio envio, DateTimeOffset fecha)
    {
        ArgumentNullException.ThrowIfNull(envio);

        var parada = _paradas.SingleOrDefault(
            p => p.EnvioId == envio.Id && p.Activa);

        if (parada is null)
            throw new InvalidOperationException(
                "El envío no tiene una asignación activa en esta ruta.");

        if (envio.OperadorId != OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (envio.Estado != EstadoEnvio.AsignadoARuta)
            throw new InvalidOperationException(
                "Solo se puede quitar un envío asignado a ruta.");

        parada.Liberar(fecha);

        PesoAsignadoKg -= envio.PesoPendienteKg;
        VolumenAsignadoM3 -= envio.VolumenPendienteMetrosCubicos;
    }

    public void PrepararRenumeracionParadas()
    {
        VerificarEditable();

        var activas = _paradas
            .Where(p => p.Activa)
            .OrderBy(p => p.Orden)
            .ToList();

        if (activas.Count == 0)
            return;

        var siguienteOrden = _paradas.Max(p => p.Orden) + 1;

        foreach (var parada in activas)
        {
            parada.CambiarOrden(siguienteOrden++);
        }
    }

    public void RenumerarParadasActivas()
    {
        VerificarEditable();

        var activas = _paradas
            .Where(p => p.Activa)
            .OrderBy(p => p.Orden)
            .ToList();

        for (var i = 0; i < activas.Count; i++)
        {
            activas[i].CambiarOrden(i + 1);
        }
    }

    public void ConfigurarHoraSalida(TimeOnly horaSalida)
    {
        HoraSalida = horaSalida;
    }
    public void FinalizarParadaEntregaRetiroComercio(Retiro retiro, DateTimeOffset fecha)
    {
        ArgumentNullException.ThrowIfNull(retiro);

        if (retiro.OperadorId != OperadorId)
            throw new InvalidOperationException(
                "El retiro pertenece a otro operador.");

        var parada = _paradas.SingleOrDefault(p =>
            p.RetiroId == retiro.Id &&
            p.TipoOperacion == TipoOperacionRuta.EntregaRetiroComercio &&
            p.Activa);

        if (parada is null)
            throw new InvalidOperationException(
                "No existe una entrega al comercio activa para este retiro.");

        if (retiro.Estado != EstadoRetiro.EntregadoAlComercio)
            throw new InvalidOperationException(
                "La entrega del retiro al comercio no fue confirmada.");

        parada.Liberar(fecha);
    }
    public void FinalizarParada(Envio envio, DateTimeOffset fecha)
    {
        ArgumentNullException.ThrowIfNull(envio);

        if (envio.OperadorId != OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        var parada = _paradas.SingleOrDefault(p =>
            p.EnvioId == envio.Id && p.Activa);

        if (parada is null)
            throw new InvalidOperationException(
                "No existe una parada activa para este envío.");

        switch (parada.TipoOperacion)
        {
            case TipoOperacionRuta.Entrega:
                if (envio.Estado != EstadoEnvio.Entregado &&
                    envio.Estado != EstadoEnvio.EntregadoParcialmente &&
                    envio.Estado != EstadoEnvio.NoEntregado)
                {
                    throw new InvalidOperationException(
                        "El envío no tiene un resultado de entrega registrado.");
                }
                break;

            case TipoOperacionRuta.DevolucionComercio:
                if (envio.Estado != EstadoEnvio.Devuelto)
                    throw new InvalidOperationException(
                        "La devolución no fue confirmada.");
                break;

            default:
                throw new InvalidOperationException(
                    "La parada no corresponde a una entrega de envío.");
        }

        parada.Liberar(fecha);
    }

    public void VerificarEditable()
    {
        if (Estado != EstadoHojaDeRuta.EnPreparacion)
            throw new InvalidOperationException(
                "No se puede modificar una hoja de ruta despachada.");
    }

    public void MarcarDespachada(DateTimeOffset fecha)
    {
        VerificarEditable();

        if (!_paradas.Any(p => p.Activa))
            throw new InvalidOperationException(
                "No se puede despachar una hoja sin paradas activas.");

        Estado = EstadoHojaDeRuta.Despachada;
        DespachadaEn = fecha.ToUniversalTime();
    }

    /// <summary>
    /// RF 22: cierra la jornada de la hoja. Solo una hoja despachada en la que todas las paradas
    /// ya tienen resultado (ninguna sigue activa) puede rendirse.
    /// </summary>
    public void MarcarRendida()
    {
        if (Estado != EstadoHojaDeRuta.Despachada)
            throw new InvalidOperationException(
                "Solo se puede rendir una hoja de ruta despachada.");

        if (_paradas.Any(p => p.Activa))
            throw new InvalidOperationException(
                "La hoja todavía tiene paradas sin resultado.");

        Estado = EstadoHojaDeRuta.Rendida;
    }

    public void ActualizarOrdenParadas(
        IReadOnlyList<HorarioParada> horarios)
    {
        VerificarEditable();
        ArgumentNullException.ThrowIfNull(horarios);

        var activas = _paradas
            .Where(p => p.Activa)
            .ToList();

        if (horarios.Count != activas.Count ||
            horarios.Select(h => h.EnvioId).Distinct().Count() != horarios.Count ||
            activas.Any(p => !horarios.Any(h => h.EnvioId == p.EnvioId)))
        {
            throw new InvalidOperationException(
                "La planificación no coincide con las paradas activas.");
        }

        for (var i = 0; i < horarios.Count; i++)
        {
            var parada = activas.Single(
                p => p.EnvioId == horarios[i].EnvioId);

            parada.CambiarOrden(i + 1);
        }
    }

    public void LiberarParadaPorExtravio(
        Envio envio,
        DateTimeOffset fecha)
    {
        ArgumentNullException.ThrowIfNull(envio);

        if (envio.OperadorId != OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (envio.Estado != EstadoEnvio.AsignadoARuta &&
            envio.Estado != EstadoEnvio.EnTransito)
        {
            throw new InvalidOperationException(
                "El envío no está en un estado válido para liberar su parada por extravío.");
        }

        var parada = _paradas.SingleOrDefault(
            p => p.EnvioId == envio.Id && p.Activa);

        if (parada is null)
            throw new InvalidOperationException(
                "No existe una parada activa para este envío.");

        parada.Liberar(fecha.ToUniversalTime());

        PesoAsignadoKg -= envio.PesoTotalKg;
        VolumenAsignadoM3 -= envio.VolumenTotalMetrosCubicos;
    }
}
