using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;

namespace UltimaMilla.Application.Configuracion.Commands.EditarZona;

/// <summary>
/// Caso de uso "editar una zona de cobertura" (RF 2): la configuración del operador se
/// cambia desde la interfaz, sin tocar código ni desplegar (RNF 6.6).
/// Mismas reglas que el alta: nombre único por operador, coberturas sin repetir,
/// franjas sin superponer y ninguna localidad cubierta por dos zonas activas.
/// </summary>
public sealed class EditarZonaHandler(
    IZonaCoberturaRepository zonas,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(
        EditarZonaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty || command.ZonaId == Guid.Empty)
            throw new ArgumentException(
                "El operador y la zona son obligatorios.");

        if (string.IsNullOrWhiteSpace(command.Nombre))
            throw new ArgumentException(
                "El nombre de la zona es obligatorio.");

        if (command.Coberturas is null || command.Coberturas.Any(c => c is null))
            throw new ArgumentException(
                "Hay una cobertura inválida.");

        if (command.Franjas is null || command.Franjas.Any(f => f is null))
            throw new ArgumentException(
                "Hay una franja inválida.");

        // El filtro por operador de EF Core hace que solo se encuentren zonas del operador de la sesión.
        var zona = await zonas.ObtenerAsync(command.OperadorId, command.ZonaId, ct)
            ?? throw new NoEncontradoException("La zona no existe.");

        var nombre = command.Nombre.Trim();
        if (await zonas.ExisteNombreEnOtraZonaAsync(command.OperadorId, nombre, zona.Id, ct))
            throw new InvalidOperationException(
                "Ya existe otra zona con ese nombre para el operador.");

        zona.ConfigurarTiempoEstimado(command.MinutosTraslado);

        zona.Editar(
            nombre,
            command.Departamento,
            command.Coberturas.Select(c => (c.Localidad, c.Sector)).ToList(),
            command.Franjas.Select(f => (f.HoraDesde, f.HoraHasta)).ToList());

        // Con los cambios, ninguna localidad puede quedar cubierta también por otra zona.
        var existentes = await zonas.ListarPorOperadorAsync(command.OperadorId, ct);
        ValidacionCoberturas.ValidarSinSuperposicion(zona, existentes);

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
