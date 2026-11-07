using System.Text.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Eventos;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;

public sealed class RegistrarAvisosRetiroHandler(
    IRetiroRepository retiros,
    ISuscripcionAvisoRepository suscripciones,
    IEntregaAvisoRepository entregas,
    IPublicadorDeEventos publicador)
{
    private static readonly JsonSerializerOptions OpcionesJson =
        new(JsonSerializerDefaults.Web);

    // Para operaciones sobre retiros existentes.
    public async Task<int> PrepararAsync(
        Guid retiroId,
        string tipoEvento,
        string claveEvento,
        DateTimeOffset ocurridoEn,
        CancellationToken ct)
    {
        var retiro = await retiros.ObtenerAsync(retiroId, ct)
            ?? throw new NoEncontradoException(
                "El retiro no existe.");

        return await PrepararAsync(
            retiro,
            tipoEvento,
            claveEvento,
            ocurridoEn,
            ct);
    }

    // Permite preparar avisos antes de guardar un retiro nuevo.
    public async Task<int> PrepararAsync(
        Retiro retiro,
        string tipoEvento,
        string claveEvento,
        DateTimeOffset ocurridoEn,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(retiro);

        if (!TiposAviso.Todos.Contains(tipoEvento))
            throw new ArgumentException(
                "El tipo de aviso no es válido.");

        if (string.IsNullOrWhiteSpace(claveEvento))
            throw new ArgumentException(
                "La clave del evento es obligatoria.");

        var lista = await suscripciones.ListarAsync(
            retiro.CuentaComercialId,
            ct);

        var creados = 0;

        foreach (var suscripcion in lista.Where(s =>
            s.OperadorId == retiro.OperadorId &&
            s.CuentaComercialId == retiro.CuentaComercialId &&
            s.Escucha(tipoEvento)))
        {
            if (await entregas.ExisteAsync(
                suscripcion.Id,
                claveEvento,
                ct))
            {
                continue;
            }

            var cuerpo = JsonSerializer.Serialize(new
            {
                tipoOperacion = "Retiro",
                tipo = tipoEvento,
                retiroId = retiro.Id,
                estado = retiro.Estado.ToString(),
                ocurridoEn
            }, OpcionesJson);

            var entrega = EntregaAviso.Crear(
                suscripcion,
                tipoEvento,
                claveEvento,
                cuerpo);

            await entregas.AgregarAsync(entrega, ct);

            await publicador.PublicarAsync(
                new AvisoPendiente(
                    entrega.Id,
                    entrega.OperadorId),
                ct);

            creados++;
        }

        return creados;
    }
}